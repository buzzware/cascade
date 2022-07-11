using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using SQLite;

namespace Cascade
{
	public class CascadeDataLayer {
		
		private readonly ICascadeOrigin Origin;
		private readonly IEnumerable<ICascadeCache> CacheLayers;
		public readonly CascadeConfig Config;
		
		public CascadeDataLayer(
			ICascadeOrigin origin,
			IEnumerable<ICascadeCache> cacheLayers,
			CascadeConfig config
		) {
			Origin = origin;
			Origin.Cascade = this;
			CacheLayers = cacheLayers;
			foreach (var cache in cacheLayers)
				cache.Cascade = this;
			Config = config;
		}

		public long NowMs => Origin.NowMs;

		public Task<OpResponse> GetResponse<M>(int id, int? freshnessSeconds = null) where M : class {
			var req = RequestOp.GetOp<M>(
				id,
				NowMs,
				freshnessSeconds ?? Config.DefaultFreshnessSeconds
			);
			return ProcessRequest(req);
		}
		
		public async Task<M?> Get<M>(int id, int? freshnessSeconds = null) where M : class {
			return (await this.GetResponse<M>(id, freshnessSeconds)).Result as M;
		}

		public Task<OpResponse> GetResponse<M>(string id, int? freshnessSeconds = null) where M : class {
			var req = RequestOp.GetOp<M>(
				id,
				NowMs,
				freshnessSeconds ?? Config.DefaultFreshnessSeconds
			);
			return ProcessRequest(req);
		}

		public async Task<M?> Get<M>(string id, int? freshnessSeconds = null) where M : class {
			return (await this.GetResponse<M>(id, freshnessSeconds)).Result as M;
		}
		
		public Task<OpResponse> GetResponse<M>(long id, int? freshnessSeconds = null) where M : class {
			var req = RequestOp.GetOp<M>(
				id,
				NowMs,
				freshnessSeconds ?? Config.DefaultFreshnessSeconds
			);
			return ProcessRequest(req);
		}

		public async Task<M?> Get<M>(long id, int? freshnessSeconds = null) where M : class {
			return (await this.GetResponse<M>(id, freshnessSeconds)).Result as M;
		}
		
		public Task<OpResponse> QueryResponse<M>(string key, object criteria, int? freshnessSeconds = null) {
			var req = RequestOp.QueryOp<M>(
				key,
				criteria,
				NowMs,
				freshnessSeconds ?? Config.DefaultFreshnessSeconds
			);
			return ProcessRequest(req);
		}

		public async Task<IEnumerable<M>> Query<M>(string key, object criteria, int? freshnessSeconds = null) {
			var response = await QueryResponse<M>(key, criteria, freshnessSeconds);
			var results = response.Results.Cast<M>();
			//return Array.ConvertAll<object,M>(response.Results) ?? Array.Empty<M>();
			return results;
		}
		
		private Task<OpResponse> ProcessRequest(RequestOp req) {
			switch (req.Verb) {
				case RequestVerb.Get:
				case RequestVerb.Query:
					return ProcessReadOrQuery(req);
				default:
					throw new ArgumentException("Unsupported verb");
			}
		}
		
		private async Task<OpResponse> ProcessReadOrQuery(RequestOp requestOp) {
			object? value;
			ICascadeCache? layerFound = null;
			OpResponse? opResponse = null;
			
			// try each cache layer
			foreach (var layer in CacheLayers) {
				var res = await layer.Fetch(requestOp);
				if (res.PresentAndFresh()) {
					layerFound = layer;
					opResponse = res;
					break;
				}
			}
			// if still not found, try origin
			if (opResponse==null)
				opResponse = await Origin.ProcessRequest(requestOp);
			
			if (requestOp.Verb == RequestVerb.Query && opResponse.IsIdResults) {
				var modelResponses = await GetModelsForIds(requestOp.Type, requestOp.FreshnessSeconds, opResponse.ResultIds);
				// don't need to do this because the get above will achieve the same
				// foreach (var modelResponse in modelResponses) {
				// 	await StoreInPreviousCaches(opResponse, layerFound);		
				// }
				await StoreInPreviousCaches(opResponse, layerFound);					// just store ResultIds
				opResponse = opResponse.withChanges(result: modelResponses);	// modify the response with models instead of ids
			} else {
				await StoreInPreviousCaches(opResponse, layerFound);
			}
			return opResponse!;
		}

		private async Task<IEnumerable<OpResponse>> GetModelsForIds(Type type, int freshnessSeconds, IEnumerable<object> ids) {
			const int MaxParallelRequests = 10;

			OpResponse[] allResponses = new OpResponse[ids.Count()];
			for (var i = 0; i < ids.Count(); i += MaxParallelRequests) {
				var someIds = ids.Skip(i).Take(MaxParallelRequests);
				var someGetResponses = await Task.WhenAll(	// wait on all requests in parallel
					someIds.Select(id => ProcessRequest(				// map each id to a get request and process it
							new RequestOp(
								NowMs,
								type,
								RequestVerb.Get,
								id,
								freshnessSeconds
							)
						)
					)
				);
				for (int j = 0; j < someGetResponses.Length; j++)	// fill allResponses array from responses
					allResponses[i + j] = someGetResponses[j];
			}
			return allResponses;
		}

		private async Task StoreInPreviousCaches(OpResponse opResponse, ICascadeCache? layerFound) {
			var beforeLayer = layerFound == null;
			foreach (var layer in CacheLayers.Reverse()) {
				if (!beforeLayer && layer == layerFound)
					beforeLayer = true;
				if (!beforeLayer)
					continue;
				await layer.Store(opResponse);
			}
		}

		// 		public ICascadeStore localStore {
// 			get {
// 				return Layers?.LastOrDefault(l => l.Local) as ICascadeStore;
// 			}
// 		}
//
// 		public ICascadeStore originStore {
// 			get {
// 				return Layers?.FirstOrDefault(l => l.Origin) as ICascadeStore;
// 			}
// 		}
//
// 		public List<ICascadeStore> Layers { get; private set; } = new List<ICascadeStore>();
//
// 		public CascadeDataLayer()
// 		{
// 		}
//
// 		// see https://stackoverflow.com/questions/17480990/get-name-of-generic-class-without-tilde
// 		public string ResourceFromType<T>() {
// 			return typeof(T).Name.Split('`')[0];
// 		}
//
// 		private string GetConfigIntegrityError() {
// 			if (Layers.Count > 0) {
// 				if (!Layers[0].Origin)
// 					return "The first layer must be marked Origin = true";
// 				for (int i = 0; i < Layers.Count; i++) {
// 					if (i>0 && Layers[i].Origin)
// 						return "Only the first layer should be marked Origin = true";
// 					if (Layers[i].Cascade != this)
// 						return "All layers must have Cascade set to this CascadeDataLayer";
// 				}
// 			}
// 			return null;
// 		}
// 		
// 		//
// 		//	CORE METHODS
// 		//
// 		
// 		public async Task<M> Read<M>(RequestOp aRequestOp) //string aResourceId, bool aFresh = true, bool aFallback = true)
// 			where M : class, ICascadeModel, new() {
// 			var response = await ReadResponse<M>(aRequestOp);
// 			return response.ResultObject as M;
// //			CascadeUtils.EnsureIsResourceId(aRequestOp.Id);
// //			OpResponse localResponse = null;
// //			OpResponse remoteResponse = null;
// //			M result = default(M);
// //			//M remote = null;
// //			//M local = null;
// //			//bool remoteConnected = false;
// //			//bool remotePresent = false;
// //			//bool localPresent = false;
// //
// //			if (aRequestOp.Fresh)
// //			{
// //				try
// //				{
// //					remoteResponse = await originStore.Read(aRequestOp);
// //				}
// //				catch (Exception e)
// //				{
// //					//Log.Debug("Exception: " + e.Message);
// //					if (!aRequestOp.Fallback)
// //						throw;
// //				}
// //
// //				if (remoteResponse!=null && remoteResponse.Connected)
// //				{
// //					if (remoteResponse.Present)
// //					{
// //						await localStore.Replace(remoteResponse.ResultObject);
// //						result = remoteResponse.ResultObject;
// //					}
// //					else
// //					{
// //						localStore.Destroy<M>(aRequestOp.Id);
// //						result = default(M);
// //					}
// //				}
// //				else
// //				{
// //					if (aRequestOp.Fallback)
// //					{
// //						localResponse = await localStore.Read<M>(aRequestOp.Id);
// //						result = localResponse.ResultObject;
// //					}
// //					else
// //					{
// //						result = default(M); // remoteResponse.value;
// //					}
// //				}
// //			}
// //			else
// //			{
// //
// //				try
// //				{
// //					localResponse = await localStore.Read<M>(aRequestOp.Id);
// //				}
// //				catch (Exception e)
// //				{
// //					//Log.Debug("Exception: " + e.Message);
// //					if (!aRequestOp.Fallback)
// //						throw;
// //				}
// //
// //				if (localResponse!=null && localResponse.Present)
// //				{
// //					result = localResponse.ResultObject;
// //				}
// //				else
// //				{
// //					if (aRequestOp.Fallback)
// //					{
// //						remoteResponse = await originStore.Read<M>(aRequestOp.Id);
// //						result = remoteResponse.ResultObject;
// //					}
// //					else
// //					{
// //						result = default(M); // remoteResponse.value;
// //					}
// //				}
// //			}
// //			return result;
// 		}
//
// 		public async Task<OpResponse> ReadResponse<M>(RequestOp aRequestOp) where M : class, ICascadeModel, new() {
// 			CheckConfigIntegrity();
// 			aRequestOp.Verb = RequestOp.Verbs.Read;
// 			if (aRequestOp.Key == null)
// 				aRequestOp.Key = CascadeUtils.JoinKey(ResourceFromType<M>(), aRequestOp.Id);
// 			var gopher = new Gopher(this, aRequestOp);
// 			var response = await gopher.Run();
// 			var result = response.ResultObject;
// 			response.ResultKey = aRequestOp.ResultKey ?? GetKeyFrom(result);
// 			return response;
// 		}
//
// 		private void CheckConfigIntegrity() {
// 			var error = GetConfigIntegrityError();
// 			if (error==null)
// 				return;
// 			throw new ConfigurationException(error);
// 		}
//
//
// 		public async Task<List<M>> ReadAll<M>(RequestOp aRequestOp)
// 			where M : class,
// 			ICascadeModel, new()
// 		{
// 			CheckConfigIntegrity();
// 			aRequestOp.Verb = RequestOp.Verbs.ReadAll;			
// 			return null;
// //			OpResponse<List<M>> localResponse = null;
// //			OpResponse<List<M>> remoteResponse = null;
// //
// //			List<M> result = null;
// //
// //			if (aRequestOp.Fresh)
// //			{
// //				try
// //				{
// //					remoteResponse = await originStore.ReadAll<M>();
// //				}
// //				catch (Exception e)
// //				{
// //					//Log.Debug("Exception: " + e.Message);
// //					if (!aRequestOp.Fallback)
// //						throw;
// //				}
// //
// //				if (remoteResponse.Connected)
// //				{
// //					if (remoteResponse.Present)
// //					{
// //						var items = remoteResponse.ResultObject;
// //						foreach (var r in items)
// //						{
// //							await localStore.Replace(r);
// //						}
// //						if (aRequestOp.Exclusive)
// //						{
// //							IEnumerable<string> ids = items.Select(i => i.GetResourceId()).ToArray();
// //							await localStore.DestroyExcept<M>(ids);
// //						}
// //					}
// //					result = remoteResponse.ResultObject ?? new List<M>();
// //				}
// //				else
// //				{
// //					if (aRequestOp.Fallback)
// //					{
// //						localResponse = await localStore.ReadAll<M>();
// //						result = localResponse.ResultObject;
// //					}
// //					else
// //					{
// //						result = new List<M>();
// //					}
// //				}
// //			}
// //			else
// //			{
// //
// //				try
// //				{
// //					localResponse = await localStore.ReadAll<M>();
// //				}
// //				catch (Exception e)
// //				{
// //					//Log.Debug("Exception: " + e.Message);
// //					if (!aRequestOp.Fallback)
// //						throw;
// //				}
// //
// //				if (localResponse.Present)
// //				{
// //					result = localResponse.ResultObject;
// //				}
// //				else
// //				{
// //					if (aRequestOp.Fallback)
// //					{
// //						remoteResponse = await originStore.ReadAll<M>();
// //						result = remoteResponse.ResultObject;
// //					}
// //					else
// //					{
// //						result = null; // remoteResponse.value;
// //					}
// //				}
// //			}
// //			return result;
// 		}
//
// 		public async Task<M> Update<M>(RequestOp aRequestOp) where M : class, ICascadeModel, new() {
// 			aRequestOp.Verb = RequestOp.Verbs.Update;
//
// 			return null;
// //			M result = default(M);
// //			OpResponse<M> localResponse = null;
// //			OpResponse<M> remoteResponse = null;
// //			M value = aRequestOp.Value as M;
// //
// //			remoteResponse = await originStore.Update(value);
// //			if (remoteResponse.Connected) {
// //				if (remoteResponse.Present) {
// //					localStore.Replace(remoteResponse.ResultObject);
// //					result = remoteResponse.ResultObject;
// //				} else {
// //					localStore.Destroy<M>(value.GetResourceId());
// //					result = null;
// //				}
// //			} else {
// //				throw new NoNetworkException("Failed to reach remote store");
// //			}
// //			return result;
// 		}
// 		
// 		
// 		//
// 		//	VANITY METHODS
// 		//
// //		
// //		public Task<M> Read<M>(long aResourceId, bool aFresh = true, bool aFallback = true, RequestOp aRequestOp = null) where M : class, ICascadeModel, new()
// //		{
// //			var op = aRequestOp ?? new RequestOp(){
// //				Id = aResourceId.ToString(),
// //				Fresh = aFresh,
// //				Fallback = aFallback
// //			};
// //			return Read<M>(op);
// //		}
// //
// //		public Task<M> Read<M>(string aResourceId, bool aFresh = true, bool aFallback = true,
// //			RequestOp aRequestOp = null)
// //			where M : class, ICascadeModel, new() {
// //			var op = aRequestOp ?? new RequestOp(){
// //				Id = aResourceId,
// //				Fresh = aFresh,
// //				Fallback = aFallback
// //			};
// //			return Read<M>(op);
// //		}
// //		
// //		public Task<List<M>> ReadAll<M>(bool aFresh = true, bool aFallback = true, bool aExclusive = false,RequestOp aRequestOp=null)
// //			where M : class,
// //			ICascadeModel, new() {
// //			var op = aRequestOp ?? new RequestOp(){
// //				Fresh = aFresh,
// //				Fallback = aFallback,
// //				Exclusive = aExclusive
// //			};
// //			return ReadAll<M>(op);			
// //		}
// //		
// //		public Task<M> Write<M>(M value) where M : class, ICascadeModel, new() {
// //			var op = new RequestOp(){
// //				Value = value,
// //				Fallback = false,
// //			};
// //			return Write<M>(op);						
// //		}		
// 		
// 		public Task<OpResponse> Do(ICascadeStore aStore, RequestOp aRequestOp) {
// 			switch (aRequestOp.Verb) {
// 				case RequestOp.Verbs.Create:
// 					return aStore.Create(aRequestOp);
// 				case RequestOp.Verbs.Read:
// 					return aStore.Read(aRequestOp);
// 				case RequestOp.Verbs.ReadAll:
// 					return aStore.ReadAll(aRequestOp);
// 				case RequestOp.Verbs.Update:
// 					return aStore.Update(aRequestOp);
// 				case RequestOp.Verbs.Destroy:
// 					return aStore.Destroy(aRequestOp);
// 				case RequestOp.Verbs.Execute:
// 					return aStore.Execute(aRequestOp);
// 			}
// 			throw new StandardException("Unsupported Verb "+aRequestOp.Verb.ToString());
// 		}
//
// 		public string GetKeyFrom(object aModel) {
// 			return GetResourceFrom(aModel) + "__" + GetIdFrom(aModel);
// 		}
//
// 		private string GetIdFrom(object aModel) {
// 			ICascadeModel cm = aModel as ICascadeModel;
// 			if (cm==null)
// 				throw new StandardException("aModel is not a ICascadeModel");
// 			return cm.CascadeId();
// 		}
//
// 		public string GetResourceFrom(object aModel) {
// 			ICascadeModel cm = aModel as ICascadeModel;
// 			if (cm==null)
// 				throw new StandardException("aModel is not a ICascadeModel");
// 			return cm.CascadeResource();
// 		}
//
// 		public string JsonSerialize(object source) {
// 			return JsonConvert.SerializeObject(source);
// 		}

	}

}
