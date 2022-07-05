using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cascade;
using Cascade.testing;
using NUnit.Framework;

namespace Test {
	
	[TestFixture]
	public class ReadTests {
		[Test]
		public async Task ReadWithoutCache() {
			var origin = new MockOrigin();
			var cascade = new CascadeDataLayer(origin,new ICascadeCache[] {}, new CascadeConfig());
			var thing = await cascade.Read<Thing>(5);
			Assert.AreEqual(5,thing!.Id);
		}
		
		[Test]
		public async Task ReadWithModelCachesMultitest() {
			var origin = new MockOrigin(nowMs:1000,handleRequest: (origin, requestOp) => {
				var nowMs = origin.NowMs;
				var thing = new Thing(requestOp.IdAsInt ?? 0);
				thing.UpdatedAtMs = requestOp.TimeMs;
				return Task.FromResult(new OpResponse(
					requestOp: requestOp,
					nowMs,
					connected: true,
					present: true,
					result: thing,
					arrivedAtMs: nowMs
				));
			});
			var thingModelStore1 = new ModelClassCache<Thing, long>();
			var cache1 = new ModelCache(aClassCache: new Dictionary<Type, IModelClassCache>() {
				{typeof(Thing), thingModelStore1}
			});
			var thingModelStore2 = new ModelClassCache<Thing, long>();
			var cache2 = new ModelCache(aClassCache: new Dictionary<Type, IModelClassCache>() {
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
