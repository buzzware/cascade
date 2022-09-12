using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Easy.Common.Extensions;
using Microsoft.CSharp.RuntimeBinder;
using SQLite;
using StandardExceptions;
using Binder = Microsoft.CSharp.RuntimeBinder.Binder;

namespace Cascade {
	
	public class CascadeDataLayer {
		
		private readonly ICascadeOrigin Origin;
		private readonly IEnumerable<ICascadeCache> CacheLayers;
		public readonly CascadeConfig Config;
		private readonly object lockObject;

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
			lockObject = new object();
		}

		public long NowMs => Origin.NowMs;

		public Task<OpResponse> GetResponse<M>(
			int id, 
			IEnumerable<string>? populate = null, 
			int? freshnessSeconds = null
		) where M : class {
			var req = RequestOp.GetOp<M>(
				id,
				NowMs,
				populate,
				freshnessSeconds ?? Config.DefaultFreshnessSeconds
			);
			return ProcessRequest(req);
		}
		
		public async Task<M?> Get<M>(
			int id, 
			IEnumerable<string>? populate = null, 
			int? freshnessSeconds = null
		) where M : class {
			return (await this.GetResponse<M>(id, populate, freshnessSeconds)).Result as M;
		}

		public Task<OpResponse> GetResponse<M>(
			string id, 
			IEnumerable<string>? populate = null, 
			int? freshnessSeconds = null
		) where M : class {
			var req = RequestOp.GetOp<M>(
				id,
				NowMs,
				populate: populate,
				freshnessSeconds ?? Config.DefaultFreshnessSeconds
			);
			return ProcessRequest(req);
		}

		public async Task<M?> Get<M>(
			string id,
			IEnumerable<string>? populate = null,
			int? freshnessSeconds = null
		) where M : class {
			return (await this.GetResponse<M>(
				id,
				populate,
				freshnessSeconds
			)).Result as M;
		}
		
		public Task<OpResponse> GetResponse<M>(
			long id, 
			IEnumerable<string>? populate = null, 
			int? freshnessSeconds = null
		) where M : class {
			var req = RequestOp.GetOp<M>(
				id,
				NowMs,
				populate,
				freshnessSeconds ?? Config.DefaultFreshnessSeconds
			);
			return ProcessRequest(req);
		}

		public async Task<M?> Get<M>(long id, IEnumerable<string>? populate = null, int? freshnessSeconds = null) where M : class {
			return (await this.GetResponse<M>(id, populate, freshnessSeconds)).Result as M;
		}
		
		public Task<OpResponse> QueryResponse<M>(
			string key, 
			object criteria, 
			IEnumerable<string>? populate = null, 
			int? freshnessSeconds = null
		) {
			var req = RequestOp.QueryOp<M>(
				key,
				criteria,
				NowMs,
				populate: populate,
				freshnessSeconds: freshnessSeconds ?? Config.DefaultFreshnessSeconds
			);
			return ProcessRequest(req);
		}

		public async Task<IEnumerable<M>> Query<M>(
			string key, 
			object? criteria=null, 
			IEnumerable<string>? populate = null, 
			int? freshnessSeconds = null
		) {
			var response = await QueryResponse<M>(key, criteria, populate, freshnessSeconds);
			var results = response.Results.Cast<M>().ToImmutableArray();
			//return Array.ConvertAll<object,M>(response.Results) ?? Array.Empty<M>();
			return results;
		}

		public async Task Populate(SuperModel model, string property) {
			var modelType = model.GetType();
			var propertyInfo = modelType.GetProperty(property);

			if (propertyInfo?.GetCustomAttributes(typeof(HasManyAttribute),true).FirstOrDefault() is HasManyAttribute hasMany) {
				await processHasMany(model, modelType, propertyInfo!, hasMany);
			} else if (propertyInfo?.GetCustomAttributes(typeof(BelongsToAttribute),true).FirstOrDefault() is BelongsToAttribute belongsTo) {
				await processBelongsTo(model, modelType, propertyInfo!, belongsTo);
			}
		}

		public async Task Populate(SuperModel model, string[] associations) {
			foreach (var association in associations) {
				await Populate(model, association);
			}
		}

		public async Task Populate(IEnumerable<SuperModel> models, string[] associations) {
			foreach (var model in models) {
				foreach (var association in associations) {
					await Populate(model, association);
				}
			}
		}
		
		public Task<OpResponse> CreateResponse<M>(M model) {
			var req = RequestOp.CreateOp(
				model!,
				NowMs
			);
			return ProcessRequest(req);
		}
		
		public async Task<M> Create<M>(M model) {
			var response = await CreateResponse<M>(model);
			if (response.Result is not M result)
				throw new AssumptionException($"Should be of type {typeof(M).Name}");
			return result;
		}
		
		public Task<OpResponse> ReplaceResponse<M>(M model) {
			var req = RequestOp.CreateOp(
				model!,
				NowMs
			);
			return ProcessRequest(req);
		}
		
		public async Task<M> Replace<M>(M model) {
			var response = await ReplaceResponse<M>(model);
			if (response.Result is not M result)
				throw new AssumptionException($"Should be of type {typeof(M).Name}");
			return result;
		}

		public async Task Destroy<M>(object id) {
			throw new NotImplementedException();
		}
		
		// =================== PRIVATE METHODS =========================


		public static object ConvertType(object aSource, Type singularType) {
			throw new NotImplementedException();
		}

		// CreateRange<T>(IEnumerable<T> items)

		
		
	// class ClassWithGenericStaticMethod
	// {
	// 	public static void PrintName<T>(string prefix) where T : class
	// 	{
	// 		Console.WriteLine(prefix + " " + typeof(T).FullName);
	// 	}
	// }
	//
	//
	// // Grabbing the type that has the static generic method
	// 		Type typeofClassWithGenericStaticMethod = typeof(ClassWithGenericStaticMethod);
	//
	// // Grabbing the specific static method
	// 		MethodInfo methodInfo = typeofClassWithGenericStaticMethod.GetMethod("PrintName", System.Reflection.BindingFlags.Static | BindingFlags.Public);
	//
	// // Binding the method info to generic arguments
	// 		Type[] genericArguments = new Type[] { typeof(Program) };
	// 		MethodInfo genericMethodInfo = methodInfo.MakeGenericMethod(genericArguments);
	//
	// // Simply invoking the method and passing parameters
	// // The null parameter is the object to call the method from. Since the method is
	// // static, pass null.
	// 		object returnValue = genericMethodInfo.Invoke(null, new object[] { "hello" });


	private void SetModelProperty(object model, PropertyInfo propertyInfo, object? value) {
			propertyInfo.SetValue(model,value);
		}
		
			
			//
			//
			// // special case for enums
			// if (targetType.IsEnum) {
			// 	// we could be going from an int -> enum so specifically let
			// 	// the Enum object take care of this conversion
			// 	if (value != null) {
			// 		value = Enum.ToObject(targetType, value);
			// 	}
			// }
			// else {
			// 	// returns an System.Object with the specified System.Type and whose value is
			// 	// equivalent to the specified object.
			// 	value = Convert.ChangeType(value, targetType);
			// }

			// set the value of the property
		// 	propertyInfo.SetValue(target, value, null);
		// }



		private async Task processHasMany(SuperModel model, Type modelType, PropertyInfo propertyInfo, HasManyAttribute attribute) {
			var propertyType = CascadeTypeUtils.DeNullType(propertyInfo.PropertyType);
			var isEnumerable = (propertyType?.Implements<IEnumerable>() ?? false) && propertyType != typeof(string);
			var foreignType = isEnumerable ? CascadeTypeUtils.InnerType(propertyType!) : null;
			foreignType = foreignType != null ? CascadeTypeUtils.DeNullType(foreignType) : null;
			if (foreignType == null)
				throw new ArgumentException("Unable to get foreign model type. Property should be of type ImmutableArray<ChildModel>");
			
			object modelId = CascadeTypeUtils.GetCascadeId(model);
			var key = CascadeUtils.WhereCollectionName(foreignType.Name, attribute.ForeignIdProperty, modelId.ToString());
			var requestOp = new RequestOp(
				NowMs,
				foreignType,
				RequestVerb.Query,
				null,
				value: null,
				freshnessSeconds: 0,
				criteria: new Dictionary<string,object>() { [attribute.ForeignIdProperty] = modelId },
				key: key
			);
			var opResponse = await ProcessRequest(requestOp);
			CascadeTypeUtils.SetModelCollectionProperty(model, propertyInfo, opResponse.Results);
			//propertyInfo.SetValue(model,opResponse.Results);
		}
		
		private Task<OpResponse> ProcessRequest(RequestOp req) {
			lock (lockObject) {
				switch (req.Verb) {
					case RequestVerb.Get:
					case RequestVerb.Query:
						return ProcessReadOrQuery(req);
					case RequestVerb.Create:
						return ProcessCreate(req);
					case RequestVerb.Replace:
						return ProcessReplace(req);
					default:
						throw new ArgumentException("Unsupported verb");
				}
			}
		}


		private async Task processBelongsTo(object model, Type modelType, PropertyInfo propertyInfo, BelongsToAttribute attribute) {
			var foreignModelType = CascadeTypeUtils.DeNullType(propertyInfo.PropertyType);
			var idProperty = modelType.GetProperty(attribute.IdProperty);
			var id = idProperty.GetValue(model);
			if (id == null)
				return;
			
			var requestOp = new RequestOp(
				NowMs,
				foreignModelType,
				RequestVerb.Get,
				id,
				freshnessSeconds: Config.DefaultFreshnessSeconds
			);
			var opResponse = await ProcessRequest(requestOp);
			SetModelProperty(model, propertyInfo, opResponse.Result);
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
			
			var populate = requestOp.Populate?.ToArray() ?? new string[]{};
			
			if (requestOp.Verb == RequestVerb.Query && opResponse.IsIdResults) {
				var modelResponses = await GetModelsForIds(requestOp.Type, requestOp.FreshnessSeconds ?? Config.DefaultFreshnessSeconds, opResponse.ResultIds);
				IEnumerable<SuperModel> models = modelResponses.Select(r => (SuperModel) r.Result).ToImmutableArray();
				if (populate.Any()) {
					await Populate(models, populate);
				}
				// don't need to do this because the get above will achieve the same
				// foreach (var modelResponse in modelResponses) {
				// 	await StoreInPreviousCaches(opResponse, layerFound);		
				// }
				await StoreInPreviousCaches(opResponse, layerFound);					// just store ResultIds
				opResponse = opResponse.withChanges(result: models);	// modify the response with models instead of ids
			} else {
				if (populate.Any()) {
					IEnumerable<SuperModel> results = opResponse.Results.Cast<SuperModel>();
					await Populate(results, populate);
				}
				await StoreInPreviousCaches(opResponse, layerFound);
			}
			return opResponse!;
		}
		
		private async Task<OpResponse> ProcessCreate(RequestOp req) {
			OpResponse? opResponse = await Origin.ProcessRequest(req);
			await StoreInPreviousCaches(opResponse);
			return opResponse!;
		}

		private async Task<OpResponse> ProcessReplace(RequestOp req) {
			OpResponse? opResponse = await Origin.ProcessRequest(req);
			await StoreInPreviousCaches(opResponse);
			return opResponse!;
		}
		
		private async Task<IEnumerable<OpResponse>> GetModelsForIds(Type type, int freshnessSeconds, IEnumerable iids) {
			const int MaxParallelRequests = 10;
			var ids = iids.Cast<object>();

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
								freshnessSeconds: freshnessSeconds
							)
						)
					)
				);
				for (int j = 0; j < someGetResponses.Length; j++)	// fill allResponses array from responses
					allResponses[i + j] = someGetResponses[j];
			}
			return allResponses.ToImmutableArray();
		}

		private async Task StoreInPreviousCaches(OpResponse opResponse, ICascadeCache? layerFound=null) {
			var beforeLayer = layerFound == null;
			foreach (var layer in CacheLayers.Reverse()) {
				if (!beforeLayer && layer == layerFound)
					beforeLayer = true;
				if (!beforeLayer)
					continue;
				await layer.Store(opResponse);
			}
		}

		public async Task ClearCollection(string key) {
			throw new NotImplementedException();
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
// //							IEnumerable<string> ids = items.Select(i => i.GetResourceId()).ToImmutableArray();
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
