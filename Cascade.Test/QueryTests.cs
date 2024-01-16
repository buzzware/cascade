using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Cascade.Testing;
using NUnit.Framework;
using StandardExceptions;

namespace Cascade.Test {
	[TestFixture]
	public class QueryTests {
		
		MockOrigin2 origin;
		MockModelClassOrigin<Parent> thingOrigin;
		MockModelClassOrigin<Child> gadgetOrigin;

		[SetUp]
		public void SetUp() {
			thingOrigin = new MockModelClassOrigin<Parent>();
			gadgetOrigin = new MockModelClassOrigin<Child>();
			origin = new MockOrigin2(
				new Dictionary<Type, IModelClassOrigin>() {
					{ typeof(Parent), thingOrigin },
					{ typeof(Child), gadgetOrigin },
				},
				1000
			);
		}

		[Test]
		public async Task Simple() {
			Parent[] allThings = new[] {
				new Parent() { id = 1, colour = "red" },
				new Parent() { id = 2, colour = "green" },
				new Parent() { id = 3, colour = "red" },
				new Parent() { id = 4, colour = "yellow" },
			};
			foreach (var t in allThings)
				await thingOrigin.Store(t.id, t);
			var thingModelStore1 = new ModelClassCache<Parent, long>();
			var cache1 = new ModelCache(aClassCache: new Dictionary<Type, IModelClassCache>() {
				{ typeof(Parent), thingModelStore1 }
			});
			var cascade = new CascadeDataLayer(origin, new ICascadeCache[] { cache1 }, new CascadeConfig(), new MockCascadePlatform(), ErrorControl.Instance, new CascadeJsonSerialization());
			var redThings = await cascade.Query<Parent>("red_things", new JsonObject {
				["colour"] = "red"
			});
			var redIds = redThings.Select(t => t.id).ToImmutableArray();
			Assert.That(redIds, Is.EqualTo(new long[] {1, 3}));
			
			// check that collection & models are in cache
			Parent? thing;
			
			thing = await thingModelStore1.Fetch<Parent>(1);
			Assert.AreEqual(1,thing.id);
			Assert.AreEqual("red",thing.colour);
			thing = await thingModelStore1.Fetch<Parent>(3);
			Assert.AreEqual(3,thing.id);
			Assert.AreEqual("red",thing.colour);
			thing = await thingModelStore1.Fetch<Parent>(2);
			Assert.AreEqual(null, thing);

			var rcBefore = origin.RequestCount;
			origin.IncNowMs();
			
			// request with freshness=2
			var redThings2 = await cascade.Query<Parent>("red_things", new JsonObject {
				["colour"] = "red"
			}, freshnessSeconds: 2);
			Assert.That(redThings2.Select(t => t.id).ToImmutableArray(), Is.EqualTo(new long[] {1, 3}));	// same response
			Assert.AreEqual(rcBefore,origin.RequestCount);	// didn't use origin
			
			rcBefore = origin.RequestCount;
			origin.IncNowMs();
			
			redThings2 = (await cascade.Query<Parent>("red_things", new JsonObject {
				["colour"] = "red"
			}, freshnessSeconds: 0)).ToImmutableArray();
			Assert.That(redThings2.Select(t => t.id).ToImmutableArray(), Is.EqualTo(new long[] {1, 3}));	// same response
			Assert.AreEqual(rcBefore+1,origin.RequestCount);	// did use origin

			var red_things_collection = await cascade.GetCollection<Parent>("red_things");
			Assert.That(red_things_collection, Is.EqualTo(new long[] { 1, 3 }));
		}


		// [Test]
		// public async Task QueryWithSqliteCache() {
		// 	var path = System.IO.Path.GetTempFileName();
		// 	var conn = new SQLite.SQLiteAsyncConnection(path);
		// 	var db = new TestDatabase(conn);
		// 	await db.Reset();
		// 	
		// 	Child[] allGadgets = new[] {
		// 		new Child() { id = "aaa", power = 1, weight = 100 },
		// 		new Child() { id = "bbb", power = 2, weight = 123 },
		// 		new Child() { id = "ccc", power = 3, weight = 456 },
		// 		new Child() { id = "ddd", power = 4, weight = 100 }
		// 	};
		// 	foreach (var t in allGadgets)
		// 		await gadgetOrigin.Store(t.id, t);
		// 	
		// 	var memoryGadgetCache = new ModelClassCache<Child, string>();
		// 	var memoryCache = new ModelCache(aClassCache: new Dictionary<Type, IModelClassCache>() {
		// 		{ typeof(Child), memoryGadgetCache }
		// 	});
		//
		// 	var sqliteGadgetCache = new SqliteClassCache<Child, string>(db);
		// 	await sqliteGadgetCache.Setup();
		// 	var sqliteCache = new ModelCache(aClassCache: new Dictionary<Type, IModelClassCache>() {
		// 		{ typeof(Child), sqliteGadgetCache }
		// 	});
		// 	
		// 	var cascade = new CascadeDataLayer(origin, new ICascadeCache[] { memoryCache, sqliteCache }, new CascadeConfig(), new MockCascadePlatform(), ErrorControl.Instance);
		// 	
		// 	var gadgets100 = await cascade.Query<Child>("gadgets100", new JsonObject {
		// 		["weight"] = 100
		// 	});
		// 	var gadgets100Ids = gadgets100.Select(t => t.id).ToImmutableArray();
		// 	Assert.That(gadgets100Ids, Is.EqualTo(new string[] {"aaa", "ddd"}));
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
