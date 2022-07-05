using System;
using System.CodeDom;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Cascade;
using NUnit.Framework;
using Test;

namespace CascadeCacheRnD {
	
	public class MockOrigin : ICascadeOrigin {
		private Func<RequestOp,Task<OpResponse>>? HandleRequest;

		public MockOrigin(long aNowMs = 1000) {
			NowMs = aNowMs;
		}

		public CascadeDataLayer Cascade { get; set; } 
		
		public long NowMs { get; set; }

		public long IncNowMs(long incMs=1000) {
			return NowMs += incMs;
		}

		public Task<OpResponse> ProcessRequest(RequestOp request) {
			if (HandleRequest != null)
				return HandleRequest(request);
			
			var nowMs = NowMs;
			var thing = new Thing(request.IdAsInt ?? 0);
			thing.UpdatedAtMs = request.TimeMs;
			return Task.FromResult(new OpResponse(
				requestOp: request,
				nowMs,
				connected: true,
				present: true,
				result: thing,
				arrivedAtMs: nowMs
			));
		}
	}

	
	public interface IModelTypeStore {
		CascadeDataLayer Cascade { get; set; }
		Task<OpResponse> Fetch(RequestOp requestOp);
		Task Store(OpResponse opResponse);

		Task Store(object id, object model, long arrivedAt);
		Task Remove(object id);
		Task Clear();
	}

	public class ModelTypeStore<Model, IdType> : IModelTypeStore 
		where Model : class {
		private readonly Dictionary<IdType, Tuple<Model, long>> models = new Dictionary<IdType, Tuple<Model, long>>();

		public CascadeDataLayer Cascade { get; set; }

		public ModelTypeStore() {
			
		}
		
		public async Task<OpResponse> Fetch(RequestOp requestOp) {
			if (requestOp.Type != typeof(Model))
				throw new Exception("requestOp.Type != typeof(Model)");
			var id = (IdType) CascadeUtils.ConvertTo(typeof(IdType), requestOp.Id); //  ((IdType)requestOp.Id)!;
			if (id == null)
				throw new Exception("Unable to get right value for Id");

			if (models.ContainsKey(id)) {
				return new OpResponse(
					requestOp,
					Cascade.NowMs,
					connected: true,
					present: true,
					result: models[id].Item1,
					arrivedAtMs: models[id].Item2
				);
			}
			else {
				return new OpResponse(
					requestOp,
					Cascade.NowMs,
					connected: true,
					present: false,
					result: null,
					arrivedAtMs: null
				);
			}
			
		}

		public Task Store(object id, object model, long arrivedAt) {
			return Store((IdType)id, (Model)model, arrivedAt);
		}

		public Task Remove(object id) {
			return Remove((IdType)id);
		}

		public async Task Clear() {
			models.Clear();
		}

		public async Task Store(IdType id, Model model, long arrivedAt) {
			models[id] = new Tuple<Model, long>(model, arrivedAt);
		}

		public async Task Remove(IdType id) {
			models.Remove(id);
		}

		public async Task Store(OpResponse opResponse) {
			if (!opResponse.Connected)
				throw new Exception("Don't attempt to store responses from a disconnected store");
			IdType id = (IdType) CascadeUtils.ConvertTo(typeof(IdType), opResponse.RequestOp.Id);
			if (id == null)
				throw new Exception("Unable to get right value for Id");
			long arrivedAt = opResponse.ArrivedAtMs ?? Cascade.NowMs;
			if (!opResponse.Present) {
				await Remove(id);
			} else {
				if (opResponse.Result is null)
					throw new Exception("When Present is true, Result cannot be null");
				Model model = (opResponse.Result as Model)!;
				await Store(id, model, arrivedAt);
			}
		}

	}
	
	public class ModelCache : ICascadeCache {
		private Dictionary<Type, IModelTypeStore> typeStores;
		
		private CascadeDataLayer _cascade;
		public CascadeDataLayer Cascade {
			get => _cascade;
			set {
				_cascade = value;
				foreach (var ts in typeStores) {
					ts.Value.Cascade = _cascade;
				}
			}
		}

		public ModelCache(Dictionary<Type, IModelTypeStore> typeStores) {
			this.typeStores = typeStores;
		}
		
		public Task<OpResponse> Fetch(RequestOp requestOp) {
			if (requestOp.Type is null)
				throw new Exception("Type cannot be null");
			if (!typeStores.ContainsKey(requestOp.Type))
				throw new Exception("No type store for that type");
			
			var store = typeStores[requestOp.Type];
			return store.Fetch(requestOp);
		}

		public Task Store(Type type, object id, object model, long arrivedAt) {
			if (type is null)
				throw new Exception("Type cannot be null");
			if (!typeStores.ContainsKey(type))
				throw new Exception("No type store for that type");
			var store = typeStores[type];
			return store.Store(id,model,arrivedAt);
		}


		public Task Store(OpResponse opResponse) {
			if (opResponse.RequestOp.Type is null)
				throw new Exception("Type cannot be null");
			if (!typeStores.ContainsKey(opResponse.RequestOp.Type))
				throw new Exception("No type store for that type");
			var store = typeStores[opResponse.RequestOp.Type];
			return store.Store(opResponse);
		}

		public async Task Clear() {
			foreach (var kv in typeStores) {
				kv.Value.Clear();
			}
		}
	}
	
	
	[TestFixture]
	public class SimpleRead {
		[Test]
		public async Task ReadWithoutCache() {
			var origin = new MockOrigin();
			var cascade = new CascadeDataLayer(origin,new ICascadeCache[] {}, new CascadeConfig());
			var thing = await cascade.Read<Thing>(5);
			Assert.AreEqual(5,thing!.Id);
		}
		
		[Test]
		public async Task ReadWithCacheMultitest() {
			var origin = new MockOrigin();
			var thingModelStore1 = new ModelTypeStore<Thing, long>();
			var cache1 = new ModelCache(typeStores: new Dictionary<Type, IModelTypeStore>() {
				{typeof(Thing), thingModelStore1}
			});
			var thingModelStore2 = new ModelTypeStore<Thing, long>();
			var cache2 = new ModelCache(typeStores: new Dictionary<Type, IModelTypeStore>() {
				{typeof(Thing), thingModelStore2}
			});
			
			// read from origin
			var cascade = new CascadeDataLayer(origin,new ICascadeCache[] {cache1,cache2}, new CascadeConfig() {DefaultFreshnessSeconds = 1});
			var thing1 = await cascade.Read<Thing>(5);
			
			Assert.AreEqual(5,thing1!.Id);
			Assert.AreEqual(cascade.NowMs,thing1.UpdatedAtMs);
			
			// should also be in both caches
			var store1ThingResponse = await thingModelStore1.Fetch(RequestOp.ReadOp<Thing>(5,cascade.NowMs));
			Assert.AreEqual((store1ThingResponse.Result as Thing)!.Id,5);
			Assert.AreEqual(cascade.NowMs,(store1ThingResponse.Result as Thing)!.UpdatedAtMs);
			var store2ThingResponse = await thingModelStore2.Fetch(RequestOp.ReadOp<Thing>(5,cascade.NowMs));
			Assert.AreEqual((store2ThingResponse.Result as Thing)!.Id,5);
			Assert.AreEqual(cascade.NowMs,(store2ThingResponse.Result as Thing)!.UpdatedAtMs);

			origin.IncNowMs();
			
			// freshness=5 allows for cached version 
			var thing2 = (await cascade.Read<Thing>(5,freshnessSeconds: 5))!;
			Assert.AreEqual(thing1.UpdatedAtMs,thing2.UpdatedAtMs);
			
			// freshness=0 doesn't allow for cached version 
			var thing3 = (await cascade.Read<Thing>(5,freshnessSeconds: 0))!;
			Assert.AreEqual(origin.NowMs,thing3.UpdatedAtMs);

			// caches should also be updated
			store1ThingResponse = await thingModelStore1.Fetch(RequestOp.ReadOp<Thing>(5,cascade.NowMs));
			Assert.AreEqual(origin.NowMs,(store1ThingResponse.Result as Thing)!.UpdatedAtMs);
			store2ThingResponse = await thingModelStore2.Fetch(RequestOp.ReadOp<Thing>(5,cascade.NowMs));
			Assert.AreEqual(origin.NowMs,(store2ThingResponse.Result as Thing)!.UpdatedAtMs);
			
			origin.IncNowMs(2000);
			
			// freshness=2 should allow for cached version 
			var thing4 = (await cascade.Read<Thing>(5,freshnessSeconds: 2))!;
			Assert.AreEqual(thing3.UpdatedAtMs,thing4.UpdatedAtMs);

			// freshness=1 should get fresh version 
			var thing5 = (await cascade.Read<Thing>(5,freshnessSeconds: 1))!;
			Assert.AreEqual(origin.NowMs,thing5.UpdatedAtMs);
			
			origin.IncNowMs(1000);
			
			// clear cache1, freshnessSeconds=1 should return value from cache2 and update cache1
			await cache1.Clear();
			var thing6 = (await cascade.Read<Thing>(thing4.Id,freshnessSeconds: 1))!;			// should get cache2 version
			Assert.AreEqual(thing6.UpdatedAtMs,thing5.UpdatedAtMs);
			store1ThingResponse = await thingModelStore1.Fetch(RequestOp.ReadOp<Thing>(5,cascade.NowMs));
			Assert.AreEqual(thing6.UpdatedAtMs,(store1ThingResponse.Result as Thing)!.UpdatedAtMs);
		}
	}

}
