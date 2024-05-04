// using System;
// using System.Collections.Generic;
// using System.Collections.Immutable;
// using System.Text.Json.Nodes;
// using System.Threading.Tasks;
// using Cascade;
// using Cascade.Test;
// using Cascade.testing;
// using NUnit.Framework;
// using Buzzware.StandardExceptions;
//
// namespace Cascade.Test {
// 	[TestFixture]
// 	public class SqliteCacheTests {
//
// 		MockOrigin origin;
//
// 		[SetUp]
// 		public void SetUp() {
// 			origin = new MockOrigin(nowMs: 1000, handleRequest: (origin, requestOp) => {
// 				var nowMs = origin.NowMs;
// 				var thing = new Parent() {
// 					id = requestOp.IdAsInt ?? 0
// 				};
// 				thing.updatedAtMs = requestOp.TimeMs;
// 				return Task.FromResult(new OpResponse(
// 					requestOp: requestOp,
// 					nowMs,
// 					connected: true,
// 					exists: true,
// 					result: thing,
// 					arrivedAtMs: nowMs
// 				));
// 			});
// 		}
//
// 		[Test]
// 		public async Task TestMetadata() {
// 			var path = System.IO.Path.GetTempFileName();
// 			var conn = new SQLite.SQLiteAsyncConnection(path);
// 			var db = new TestDatabase(conn);
// 			await db.Reset();
// 			
// 			var sqliteThingCache = new SqliteClassCache<Parent, long>(db);
// 			await sqliteThingCache.Setup();
// 			var sqliteGadgetCache = new SqliteClassCache<Child, string>(db);
// 			await sqliteGadgetCache.Setup();
// 			var cache1 = new ModelCache(aClassCache: new Dictionary<Type, IModelClassCache>() {
// 				{ typeof(Parent), sqliteThingCache },
// 				{ typeof(Child), sqliteGadgetCache }
// 			});
//
// 			// read from origin
// 			var cascade = new CascadeDataLayer(origin, new ICascadeCache[] { cache1 }, new CascadeConfig() { DefaultFreshnessSeconds = 1 }, new MockCascadePlatform(), ErrorControl.Instance);
// 			
// 			var thing5 = new Parent() {
// 				id = 5,
// 				colour = "red"
// 			};
// 			var thing5ArrivedAt = cascade.NowMs;			
// 			await sqliteThingCache.Store(thing5.id,thing5,thing5ArrivedAt);
//
// 			origin.IncNowMs();
// 			
// 			var gadget6 = new Child() {
// 				id = "abc",
// 				weight = 2.5,
// 				power = 9.2
// 			};
// 			var gadget6ArrivedAt = cascade.NowMs;
// 			await sqliteGadgetCache.Store(gadget6.id,gadget6,gadget6ArrivedAt);
// 			
// 			origin.IncNowMs();
//
// 			var opResponse = await sqliteThingCache.Fetch(RequestOp.GetOp<Parent>(thing5.id, cascade.NowMs));
// 			var loaded5 = (opResponse.Result as Parent)!;
// 			Assert.AreEqual(thing5ArrivedAt,opResponse.ArrivedAtMs);
// 			Assert.AreEqual(thing5.colour,loaded5.colour);
// 			
// 			opResponse = await sqliteGadgetCache.Fetch(RequestOp.GetOp<Child>(gadget6.id, cascade.NowMs));
// 			var loaded6 = (opResponse.Result as Child)!;
// 			Assert.AreEqual(gadget6ArrivedAt,opResponse.ArrivedAtMs);
// 			Assert.AreEqual(gadget6.weight,loaded6.weight);
//
// 			await sqliteGadgetCache.Clear();
// 			
// 			// thing unaffected
// 			opResponse = await sqliteThingCache.Fetch(RequestOp.GetOp<Parent>(thing5.id, cascade.NowMs));
// 			Assert.NotNull(opResponse.Result);
// 			Assert.AreEqual(thing5ArrivedAt,opResponse.ArrivedAtMs);
// 			
// 			// gadget cleared including metadata
// 			opResponse = await sqliteGadgetCache.Fetch(RequestOp.GetOp<Child>(gadget6.id, cascade.NowMs));
// 			Assert.IsNull(opResponse.Result);
// 			Assert.IsNull(opResponse.ArrivedAtMs);
// 			var meta6 = await db.Get<CascadeModelMeta>(CascadeModelMeta.GenerateId<Child>(6));
// 			Assert.IsNull(meta6);
// 		}
// 		
// 		[Test]
// 		public async Task SimpleReadThroughCache() {
// 			var path = System.IO.Path.GetTempFileName();
// 			var conn = new SQLite.SQLiteAsyncConnection(path);
// 			var db = new TestDatabase(conn);
// 			await db.Reset();
// 			
// 			var sqliteThingCache = new SqliteClassCache<Parent, long>(db);
// 			await sqliteThingCache.Setup();
// 			var cache1 = new ModelCache(aClassCache: new Dictionary<Type, IModelClassCache>() {
// 				{ typeof(Parent), sqliteThingCache }
// 			});
//
// 			// read from origin
// 			var cascade = new CascadeDataLayer(origin, new ICascadeCache[] { cache1 }, new CascadeConfig() { DefaultFreshnessSeconds = 1 }, new MockCascadePlatform(), ErrorControl.Instance);
// 			var thing1 = await cascade.Get<Parent>(5);
//
// 			Assert.AreEqual(5, thing1!.id);
// 			Assert.AreEqual(cascade.NowMs, thing1.updatedAtMs);
//
// 			origin.IncNowMs();
// 			
// 			var thing2 = await cascade.Get<Parent>(5, freshnessSeconds: 2);
// 			Assert.AreEqual(thing1.updatedAtMs, thing2!.updatedAtMs);
// 			
// 			var thing3 = await cascade.Get<Parent>(5, freshnessSeconds: 0);
// 			Assert.AreEqual(origin.NowMs, thing3!.updatedAtMs);
// 		}
//
//
// 		[Test]
// 		public async Task TestCollections() {
// 			var path = System.IO.Path.GetTempFileName();
// 			var conn = new SQLite.SQLiteAsyncConnection(path);
// 			var db = new TestDatabase(conn);
// 			await db.Reset();
//
// 			var sqliteThingCache = new SqliteClassCache<Parent, long>(db);
// 			await sqliteThingCache.Setup();
// 			var sqliteGadgetCache = new SqliteClassCache<Child, string>(db);
// 			await sqliteGadgetCache.Setup();
// 			var cache = new ModelCache(aClassCache: new Dictionary<Type, IModelClassCache>() {
// 				{ typeof(Parent), sqliteThingCache },
// 				{ typeof(Child), sqliteGadgetCache }
// 			});
// 			var cascade = new CascadeDataLayer(origin, new ICascadeCache[] { cache }, new CascadeConfig() { DefaultFreshnessSeconds = 1 }, new MockCascadePlatform(), ErrorControl.Instance);
//
// 			var collection_name = "my_things";
// 			var ids = ImmutableArray.Create<object>(1, 2, 3);
// 			var response = await cache.Fetch(RequestOp.QueryOp<Parent>(collection_name, new JsonObject(), 0));
// 			Assert.AreEqual(false,response.Exists);
// 			Assert.AreEqual(null,response.Result);
// 			await cache.StoreCollection(typeof(Parent), CascadeUtils.CollectionKeyFromName(typeof(Parent).Name,collection_name), ids, 0);
//
// 			response = await cache.Fetch(RequestOp.QueryOp<Parent>(collection_name, null, 0));
// 			Assert.IsTrue(CascadeTypeUtils.IsEqualEnumerable(ids,response.ResultIds));
// 			
// 			response = await cache.Fetch(RequestOp.QueryOp<Parent>("not_my_key", null, 0));
// 			Assert.IsFalse(response.Exists);
// 			
// 			response = await cache.Fetch(RequestOp.QueryOp<Child>(collection_name, null, 0));
// 			Assert.IsFalse(response.Exists);
// 		}
// 	}
// }
