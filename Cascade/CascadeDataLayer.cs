using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StandardExceptions;

namespace Cascade
{
	public class CascadeDataLayer {

		public ICascadeStore localStore {
			get {
				return Layers?.LastOrDefault(l => l.Local) as ICascadeStore;
			}
		}

		public ICascadeStore originStore {
			get {
				return Layers?.FirstOrDefault(l => l.Origin) as ICascadeStore;
			}
		}

		public List<ICascadeStore> Layers { get; private set; } = new List<ICascadeStore>();

		public CascadeDataLayer()
		{
		}

		// see https://stackoverflow.com/questions/17480990/get-name-of-generic-class-without-tilde
		public string ResourceFromType<T>() {
			return typeof(T).Name.Split('`')[0];
		}

		private string GetConfigIntegrityError() {
			if (Layers.Count > 0) {
				if (!Layers[0].Origin)
					return "The first layer must be marked Origin = true";
				for (int i = 0; i < Layers.Count; i++) {
					if (i==0)
						continue;
					if (Layers[i].Origin)
						return "Only the first layer should be marked Origin = true";
				}
			}
			return null;
		}
		
		//
		//	CORE METHODS
		//
		
		public async Task<M> Read<M>(RequestOp aRequestOp) //string aResourceId, bool aFresh = true, bool aFallback = true)
			where M : class, ICascadeModel, new() {
			CheckConfigIntegrity();
			aRequestOp.Verb = RequestOp.Verbs.Read;
			if (aRequestOp.Key == null)
				aRequestOp.Key = CascadeUtils.JoinKey(ResourceFromType<M>(),aRequestOp.Id);
			var gopher = new Gopher(this,aRequestOp);
			var response = await gopher.Run();
			var result = response.ResultObject;
			response.ResultKey = aRequestOp.ResultKey ?? GetKeyFrom(result);
			return result as M;
//			CascadeUtils.EnsureIsResourceId(aRequestOp.Id);
//			OpResponse localResponse = null;
//			OpResponse remoteResponse = null;
//			M result = default(M);
//			//M remote = null;
//			//M local = null;
//			//bool remoteConnected = false;
//			//bool remotePresent = false;
//			//bool localPresent = false;
//
//			if (aRequestOp.Fresh)
//			{
//				try
//				{
//					remoteResponse = await originStore.Read(aRequestOp);
//				}
//				catch (Exception e)
//				{
//					//Log.Debug("Exception: " + e.Message);
//					if (!aRequestOp.Fallback)
//						throw;
//				}
//
//				if (remoteResponse!=null && remoteResponse.Connected)
//				{
//					if (remoteResponse.Present)
//					{
//						await localStore.Replace(remoteResponse.ResultObject);
//						result = remoteResponse.ResultObject;
//					}
//					else
//					{
//						localStore.Destroy<M>(aRequestOp.Id);
//						result = default(M);
//					}
//				}
//				else
//				{
//					if (aRequestOp.Fallback)
//					{
//						localResponse = await localStore.Read<M>(aRequestOp.Id);
//						result = localResponse.ResultObject;
//					}
//					else
//					{
//						result = default(M); // remoteResponse.value;
//					}
//				}
//			}
//			else
//			{
//
//				try
//				{
//					localResponse = await localStore.Read<M>(aRequestOp.Id);
//				}
//				catch (Exception e)
//				{
//					//Log.Debug("Exception: " + e.Message);
//					if (!aRequestOp.Fallback)
//						throw;
//				}
//
//				if (localResponse!=null && localResponse.Present)
//				{
//					result = localResponse.ResultObject;
//				}
//				else
//				{
//					if (aRequestOp.Fallback)
//					{
//						remoteResponse = await originStore.Read<M>(aRequestOp.Id);
//						result = remoteResponse.ResultObject;
//					}
//					else
//					{
//						result = default(M); // remoteResponse.value;
//					}
//				}
//			}
//			return result;
		}

		private void CheckConfigIntegrity() {
			var error = GetConfigIntegrityError();
			if (error==null)
				return;
			throw new ConfigurationException(error);
		}


		public async Task<List<M>> ReadAll<M>(RequestOp aRequestOp)
			where M : class,
			ICascadeModel, new()
		{
			CheckConfigIntegrity();
			aRequestOp.Verb = RequestOp.Verbs.ReadAll;			
			return null;
//			OpResponse<List<M>> localResponse = null;
//			OpResponse<List<M>> remoteResponse = null;
//
//			List<M> result = null;
//
//			if (aRequestOp.Fresh)
//			{
//				try
//				{
//					remoteResponse = await originStore.ReadAll<M>();
//				}
//				catch (Exception e)
//				{
//					//Log.Debug("Exception: " + e.Message);
//					if (!aRequestOp.Fallback)
//						throw;
//				}
//
//				if (remoteResponse.Connected)
//				{
//					if (remoteResponse.Present)
//					{
//						var items = remoteResponse.ResultObject;
//						foreach (var r in items)
//						{
//							await localStore.Replace(r);
//						}
//						if (aRequestOp.Exclusive)
//						{
//							IEnumerable<string> ids = items.Select(i => i.GetResourceId()).ToArray();
//							await localStore.DestroyExcept<M>(ids);
//						}
//					}
//					result = remoteResponse.ResultObject ?? new List<M>();
//				}
//				else
//				{
//					if (aRequestOp.Fallback)
//					{
//						localResponse = await localStore.ReadAll<M>();
//						result = localResponse.ResultObject;
//					}
//					else
//					{
//						result = new List<M>();
//					}
//				}
//			}
//			else
//			{
//
//				try
//				{
//					localResponse = await localStore.ReadAll<M>();
//				}
//				catch (Exception e)
//				{
//					//Log.Debug("Exception: " + e.Message);
//					if (!aRequestOp.Fallback)
//						throw;
//				}
//
//				if (localResponse.Present)
//				{
//					result = localResponse.ResultObject;
//				}
//				else
//				{
//					if (aRequestOp.Fallback)
//					{
//						remoteResponse = await originStore.ReadAll<M>();
//						result = remoteResponse.ResultObject;
//					}
//					else
//					{
//						result = null; // remoteResponse.value;
//					}
//				}
//			}
//			return result;
		}

		public async Task<M> Update<M>(RequestOp aRequestOp) where M : class, ICascadeModel, new() {
			aRequestOp.Verb = RequestOp.Verbs.Update;

			return null;
//			M result = default(M);
//			OpResponse<M> localResponse = null;
//			OpResponse<M> remoteResponse = null;
//			M value = aRequestOp.Value as M;
//
//			remoteResponse = await originStore.Update(value);
//			if (remoteResponse.Connected) {
//				if (remoteResponse.Present) {
//					localStore.Replace(remoteResponse.ResultObject);
//					result = remoteResponse.ResultObject;
//				} else {
//					localStore.Destroy<M>(value.GetResourceId());
//					result = null;
//				}
//			} else {
//				throw new NoNetworkException("Failed to reach remote store");
//			}
//			return result;
		}
		
		
		//
		//	VANITY METHODS
		//
//		
//		public Task<M> Read<M>(long aResourceId, bool aFresh = true, bool aFallback = true, RequestOp aRequestOp = null) where M : class, ICascadeModel, new()
//		{
//			var op = aRequestOp ?? new RequestOp(){
//				Id = aResourceId.ToString(),
//				Fresh = aFresh,
//				Fallback = aFallback
//			};
//			return Read<M>(op);
//		}
//
//		public Task<M> Read<M>(string aResourceId, bool aFresh = true, bool aFallback = true,
//			RequestOp aRequestOp = null)
//			where M : class, ICascadeModel, new() {
//			var op = aRequestOp ?? new RequestOp(){
//				Id = aResourceId,
//				Fresh = aFresh,
//				Fallback = aFallback
//			};
//			return Read<M>(op);
//		}
//		
//		public Task<List<M>> ReadAll<M>(bool aFresh = true, bool aFallback = true, bool aExclusive = false,RequestOp aRequestOp=null)
//			where M : class,
//			ICascadeModel, new() {
//			var op = aRequestOp ?? new RequestOp(){
//				Fresh = aFresh,
//				Fallback = aFallback,
//				Exclusive = aExclusive
//			};
//			return ReadAll<M>(op);			
//		}
//		
//		public Task<M> Write<M>(M value) where M : class, ICascadeModel, new() {
//			var op = new RequestOp(){
//				Value = value,
//				Fallback = false,
//			};
//			return Write<M>(op);						
//		}		
		
		public Task<OpResponse> Do(ICascadeStore aStore, RequestOp aRequestOp) {
			switch (aRequestOp.Verb) {
				case RequestOp.Verbs.Create:
					return aStore.Create(aRequestOp);
				case RequestOp.Verbs.Read:
					return aStore.Read(aRequestOp);
				case RequestOp.Verbs.ReadAll:
					return aStore.ReadAll(aRequestOp);
				case RequestOp.Verbs.Update:
					return aStore.Update(aRequestOp);
				case RequestOp.Verbs.Destroy:
					return aStore.Destroy(aRequestOp);
				case RequestOp.Verbs.Execute:
					return aStore.Execute(aRequestOp);
			}
			throw new StandardException("Unsupported Verb "+aRequestOp.Verb.ToString());
		}

		public string GetKeyFrom(object aModel) {
			return GetResourceFrom(aModel) + "__" + GetIdFrom(aModel);
		}

		private string GetIdFrom(object aModel) {
			ICascadeModel cm = aModel as ICascadeModel;
			if (cm==null)
				throw new StandardException("aModel is not a ICascadeModel");
			return cm.CascadeId();
		}

		public string GetResourceFrom(object aModel) {
			ICascadeModel cm = aModel as ICascadeModel;
			if (cm==null)
				throw new StandardException("aModel is not a ICascadeModel");
			return cm.CascadeResource();
		}
	}
}
