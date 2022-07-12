using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Cascade;
using Cascade.testing;
using NUnit.Framework;

namespace Test {

	public interface IModelClassOrigin {
		Task<IEnumerable<object>> Query(object criteria, string key);
		Task<object?> Get(object id);
	}

	public class MockModelClassOrigin<M> : IModelClassOrigin {
		private readonly Dictionary<object, M> models = new Dictionary<object, M>();

		public async Task<IEnumerable<object>> Query(object criteria,string key) {
			var enumerable1 = models.ToList().FindAll(idModel => {
				var crit = criteria as JsonObject;
				var model = idModel.Value!;
				var result = crit.ToList().All(kv => {
					var mv = model!.GetType().GetProperty(kv.Key)!.GetValue(model);
					return kv.Value!.GetValue<object>() == mv;
				});
				return result;
			}).ToArray();
			var enumerable2 = enumerable1.Select(k => ((object) k.Value)!).ToArray();
			return enumerable2;
		}
		
		public async Task<object?> Get(object id) {
			models.TryGetValue(id, out var result);
			return result;
		}

		public async Task Store(object id, M model) {
			models[id] = model;
		}
	}
	
	public class MockOrigin2 : MockOrigin {

		public int RequestCount { get; protected set; }

		private readonly Dictionary<Type,IModelClassOrigin> classOrigins;
		// private Func<MockOrigin,RequestOp,Task<OpResponse>>? HandleRequest;

		public MockOrigin2(Dictionary<Type, IModelClassOrigin> classOrigins, long nowMs = 1000)  {
			NowMs = nowMs;
			this.classOrigins = classOrigins;
		}
		
		public override async Task<OpResponse> ProcessRequest(RequestOp request) {

			RequestCount += 1;
			
			object? result = null;
			
			var co = classOrigins[request.Type];
			
			switch (request.Verb) {
				case RequestVerb.Query:
					result = await co.Query(request.Criteria,request.Key!);
					break;
				case RequestVerb.Get: 
					result = await co.Get(request.Id);
					break;
				default:
					throw new NotImplementedException();
			}

			return new OpResponse(
				request,
				NowMs,
				true,
				true,
				NowMs,
				result
			);
		}


		// public CascadeDataLayer Cascade { get; set; } 
		//
		// public long NowMs { get; set; }
		//
		// public long IncNowMs(long incMs=1000) {
		// 	return NowMs += incMs;
		// }
		//
		// public Task<OpResponse> ProcessRequest(RequestOp request) {
		// 	if (HandleRequest != null)
		// 		return HandleRequest(this,request);
		// 	throw new NotImplementedException("Attach HandleRequest or override this");
		// }
	}
	

	[TestFixture]
	public class QueryTests {
		
		MockOrigin2 origin;
		MockModelClassOrigin<Thing> thingOrigin;

		[SetUp]
		public void SetUp() {
			thingOrigin = new MockModelClassOrigin<Thing>();
			origin = new MockOrigin2(
				new Dictionary<Type, IModelClassOrigin>() {
					{ typeof(Thing), thingOrigin }
				},
				1000
			);



			// origin = new MockOrigin(nowMs:1000,handleRequest: (origin, requestOp) => {
			// 	var nowMs = origin.NowMs;
			// 	var thing = new Thing() {
			// 		Id = requestOp.IdAsInt ?? 0
			// 	};
			// 	thing.UpdatedAtMs = requestOp.TimeMs;
			// 	return Task.FromResult(new OpResponse(
			// 		requestOp: requestOp,
			// 		nowMs,
			// 		connected: true,
			// 		exists: true,
			// 		result: thing,
			// 		arrivedAtMs: nowMs
			// 	));
			// });
		}

		[Test]
		public async Task Simple() {
			Thing[] allThings = new[] {
				new Thing() { id = 1, colour = "red" },
				new Thing() { id = 2, colour = "green" },
				new Thing() { id = 3, colour = "red" },
				new Thing() { id = 4, colour = "yellow" },
			};
			foreach (var t in allThings)
				await thingOrigin.Store(t.id, t);
			var thingModelStore1 = new ModelClassCache<Thing, long>();
			var cache1 = new ModelCache(aClassCache: new Dictionary<Type, IModelClassCache>() {
				{ typeof(Thing), thingModelStore1 }
			});
			var cascade = new CascadeDataLayer(origin, new ICascadeCache[] { cache1 }, new CascadeConfig());
			var redThings = (await cascade.Query<Thing>("red_things", new JsonObject {
				["colour"] = "red"
			})).ToArray();
			var redIds = redThings.Select(t => t.id).ToArray();
			Assert.That(redIds, Is.EqualTo(new long[] {1, 3}));
			
			// check that collection & models are in cache
			Thing? thing;
			
			thing = await thingModelStore1.Fetch<Thing>(1);
			Assert.AreEqual(1,thing.id);
			Assert.AreEqual("red",thing.colour);
			thing = await thingModelStore1.Fetch<Thing>(3);
			Assert.AreEqual(3,thing.id);
			Assert.AreEqual("red",thing.colour);
			thing = await thingModelStore1.Fetch<Thing>(2);
			Assert.AreEqual(null, thing);

			var rcBefore = origin.RequestCount;
			origin.IncNowMs();
			
			// request with freshness=2
			var redThings2 = await cascade.Query<Thing>("red_things", new JsonObject {
				["colour"] = "red"
			}, freshnessSeconds: 2);
			Assert.That(redThings2.Select(t => t.id).ToImmutableArray(), Is.EqualTo(new long[] {1, 3}));	// same response
			Assert.AreEqual(rcBefore,origin.RequestCount);	// didn't use origin
			
			rcBefore = origin.RequestCount;
			origin.IncNowMs();
			
			redThings2 = (await cascade.Query<Thing>("red_things", new JsonObject {
				["colour"] = "red"
			}, freshnessSeconds: 0)).ToArray();
			Assert.That(redThings2.Select(t => t.id).ToImmutableArray(), Is.EqualTo(new long[] {1, 3}));	// same response
			Assert.AreEqual(rcBefore+1,origin.RequestCount);	// did use origin
		}


		// [Test]
		// public async Task QueryWithSqliteCache() {
		// 	
		// 	
		// 	
		// }



		// [Test]
		// public async Task ReadWithoutCache() {
		// 	var cascade = new CascadeDataLayer(origin,new ICascadeCache[] {}, new CascadeConfig());
		// 	var thing = await cascade.Read<Thing>(5);
		// 	Assert.AreEqual(5,thing!.Id);
		// }
		
		// [Test]
		// public async Task ReadWithModelCachesMultitest() {
		// 	var thingModelStore1 = new ModelClassCache<Thing, long>();
		// 	var cache1 = new ModelCache(aClassCache: new Dictionary<Type, IModelClassCache>() {
		// 		{typeof(Thing), thingModelStore1}
		// 	});
		// 	var thingModelStore2 = new ModelClassCache<Thing, long>();
		// 	var cache2 = new ModelCache(aClassCache: new Dictionary<Type, IModelClassCache>() {
		// 		{typeof(Thing), thingModelStore2}
		// 	});
		// 	
		// 	// read from origin
		// 	var cascade = new CascadeDataLayer(origin,new ICascadeCache[] {cache1,cache2}, new CascadeConfig() {DefaultFreshnessSeconds = 1});
		// 	var thing1 = await cascade.Read<Thing>(5);
		// 	
		// 	Assert.AreEqual(5,thing1!.Id);
		// 	Assert.AreEqual(cascade.NowMs,thing1.UpdatedAtMs);
		// 	
		// 	// should also be in both caches
		// 	var store1ThingResponse = await thingModelStore1.Fetch(RequestOp.ReadOp<Thing>(5,cascade.NowMs));
		// 	Assert.AreEqual((store1ThingResponse.Result as Thing)!.Id,5);
		// 	Assert.AreEqual(cascade.NowMs,(store1ThingResponse.Result as Thing)!.UpdatedAtMs);
		// 	var store2ThingResponse = await thingModelStore2.Fetch(RequestOp.ReadOp<Thing>(5,cascade.NowMs));
		// 	Assert.AreEqual((store2ThingResponse.Result as Thing)!.Id,5);
		// 	Assert.AreEqual(cascade.NowMs,(store2ThingResponse.Result as Thing)!.UpdatedAtMs);
		//
		// 	origin.IncNowMs();
		// 	
		// 	// freshness=5 allows for cached version 
		// 	var thing2 = (await cascade.Read<Thing>(5,freshnessSeconds: 5))!;
		// 	Assert.AreEqual(thing1.UpdatedAtMs,thing2.UpdatedAtMs);
		// 	
		// 	// freshness=0 doesn't allow for cached version 
		// 	var thing3 = (await cascade.Read<Thing>(5,freshnessSeconds: 0))!;
		// 	Assert.AreEqual(origin.NowMs,thing3.UpdatedAtMs);
		//
		// 	// caches should also be updated
		// 	store1ThingResponse = await thingModelStore1.Fetch(RequestOp.ReadOp<Thing>(5,cascade.NowMs));
		// 	Assert.AreEqual(origin.NowMs,(store1ThingResponse.Result as Thing)!.UpdatedAtMs);
		// 	store2ThingResponse = await thingModelStore2.Fetch(RequestOp.ReadOp<Thing>(5,cascade.NowMs));
		// 	Assert.AreEqual(origin.NowMs,(store2ThingResponse.Result as Thing)!.UpdatedAtMs);
		// 	
		// 	origin.IncNowMs(2000);
		// 	
		// 	// freshness=2 should allow for cached version 
		// 	var thing4 = (await cascade.Read<Thing>(5,freshnessSeconds: 2))!;
		// 	Assert.AreEqual(thing3.UpdatedAtMs,thing4.UpdatedAtMs);
		//
		// 	// freshness=1 should get fresh version 
		// 	var thing5 = (await cascade.Read<Thing>(5,freshnessSeconds: 1))!;
		// 	Assert.AreEqual(origin.NowMs,thing5.UpdatedAtMs);
		// 	
		// 	origin.IncNowMs(1000);
		// 	
		// 	// clear cache1, freshnessSeconds=1 should return value from cache2 and update cache1
		// 	await cache1.Clear();
		// 	var thing6 = (await cascade.Read<Thing>(thing4.Id,freshnessSeconds: 1))!;			// should get cache2 version
		// 	Assert.AreEqual(thing6.UpdatedAtMs,thing5.UpdatedAtMs);
		// 	store1ThingResponse = await thingModelStore1.Fetch(RequestOp.ReadOp<Thing>(5,cascade.NowMs));
		// 	Assert.AreEqual(thing6.UpdatedAtMs,(store1ThingResponse.Result as Thing)!.UpdatedAtMs);
		// }
	}
}
