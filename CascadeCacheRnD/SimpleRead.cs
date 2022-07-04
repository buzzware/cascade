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
		public async Task<OpResponse> ProcessRequest(RequestOp request) {
			var now = CascadeUtils.NowMs;
			var thing = new Thing();
			thing.Id = request.IdAsInt ?? 0; 
			return new OpResponse(
				requestOp: request,
				now,
				connected: true,
				present: true,
				result: thing,
				arrivedAtMs: now
			);
		}
	}

	
	public interface IModelTypeStore {
		Task<OpResponse> Fetch(RequestOp requestOp);
		Task Store(OpResponse opResponse);
	}

	public class ModelTypeStore<Model, IdType> : IModelTypeStore 
		where Model : class {
		private readonly Dictionary<IdType, Tuple<Model, long>> models = new Dictionary<IdType, Tuple<Model, long>>();

		public async Task<OpResponse> Fetch(RequestOp requestOp) {
			if (requestOp.Type != typeof(Model))
				throw new Exception("requestOp.Type != typeof(Model)");
			var id = (IdType) CascadeUtils.ConvertTo(typeof(IdType), requestOp.Id); //  ((IdType)requestOp.Id)!;
			if (id == null)
				throw new Exception("Unable to get right value for Id");

			if (models.ContainsKey(id)) {
				return new OpResponse(
					requestOp,
					CascadeUtils.NowMs,
					connected: true,
					present: true,
					result: models[id].Item1,
					arrivedAtMs: models[id].Item2
				);
			}
			else {
				return new OpResponse(
					requestOp,
					CascadeUtils.NowMs,
					connected: true,
					present: false,
					result: null,
					arrivedAtMs: null
				);
			}
			
		}

		public async Task Store(OpResponse opResponse) {
			if (!opResponse.Connected)
				throw new Exception("Don't attempt to store responses from a disconnected store");
			IdType id = (IdType) CascadeUtils.ConvertTo(typeof(IdType), opResponse.RequestOp.Id);
			if (id == null)
				throw new Exception("Unable to get right value for Id");
			long arrivedAt = opResponse.ArrivedAtMs ?? CascadeUtils.NowMs;
			if (!opResponse.Present) {
				models.Remove(id);
			}
			else {
				if (opResponse.Result is null)
					throw new Exception("When Present is true, Result cannot be null");
				Model model = (opResponse.Result as Model)!;
				models[id] = new Tuple<Model, long>(model, arrivedAt);
			}
		}
	}
	
	public class ModelObjectStore : ICascadeCache {
		private Dictionary<Type, IModelTypeStore> typeStores;
		
		public ModelObjectStore(Dictionary<Type, IModelTypeStore> typeStores) {
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

		public Task Store(OpResponse opResponse) {
			if (opResponse.RequestOp.Type is null)
				throw new Exception("Type cannot be null");
			if (!typeStores.ContainsKey(opResponse.RequestOp.Type))
				throw new Exception("No type store for that type");
			var store = typeStores[opResponse.RequestOp.Type];
			return store.Store(opResponse);
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
		public async Task SecondReadResultComesFromFirstCacheNotOrigin() {
			var origin = new MockOrigin();
			var thingModelStore1 = new ModelTypeStore<Thing, long>();
			var cache1 = new ModelObjectStore(typeStores: new Dictionary<Type, IModelTypeStore>() {
				{typeof(Thing), thingModelStore1}
			});
			var thingModelStore2 = new ModelTypeStore<Thing, long>();
			var cache2 = new ModelObjectStore(typeStores: new Dictionary<Type, IModelTypeStore>() {
				{typeof(Thing), thingModelStore2}
			});
			
			// read from origin
			var cascade = new CascadeDataLayer(origin,new ICascadeCache[] {cache1,cache2}, new CascadeConfig());
			var thing = await cascade.Read<Thing>(5);
			
			Assert.AreEqual(5,thing!.Id);
			
			// should also be in both caches
			var store1ThingResponse = await thingModelStore1.Fetch(RequestOp.ReadOp<Thing>(5));
			Assert.AreEqual((store1ThingResponse.Result as Thing)!.Id,5);
			var store2ThingResponse = await thingModelStore2.Fetch(RequestOp.ReadOp<Thing>(5));
			Assert.AreEqual((store2ThingResponse.Result as Thing)!.Id,5);
		}
	}

}
