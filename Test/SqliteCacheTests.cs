using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cascade;
using Cascade.testing;
using NUnit.Framework;
using Test;

namespace CascadeCacheRnD {
	[TestFixture]
	public class SqliteCacheTests {

		MockOrigin origin;

		[SetUp]
		public void SetUp() {
			origin = new MockOrigin(nowMs: 1000, handleRequest: (origin, requestOp) => {
				var nowMs = origin.NowMs;
				var thing = new Thing() {
					id = requestOp.IdAsInt ?? 0
				};
				thing.updatedAtMs = requestOp.TimeMs;
				return Task.FromResult(new OpResponse(
					requestOp: requestOp,
					nowMs,
					connected: true,
					exists: true,
					result: thing,
					arrivedAtMs: nowMs
				));
			});
		}

		[Test]
		public async Task TestMetadata() {
			var path = System.IO.Path.GetTempFileName();
			var conn = new SQLite.SQLiteAsyncConnection(path);
			var db = new TestDatabase(conn);
			await db.Reset();
			
			var sqliteThingCache = new SqliteClassCache<Thing, long>(db);
			await sqliteThingCache.Setup();
			var sqliteGadgetCache = new SqliteClassCache<Gadget, string>(db);
			await sqliteGadgetCache.Setup();
			var cache1 = new ModelCache(aClassCache: new Dictionary<Type, IModelClassCache>() {
				{ typeof(Thing), sqliteThingCache },
				{ typeof(Gadget), sqliteGadgetCache }
			});

			// read from origin
			var cascade = new CascadeDataLayer(origin, new ICascadeCache[] { cache1 }, new CascadeConfig() { DefaultFreshnessSeconds = 1 });
			
			var thing5 = new Thing() {
				id = 5,
				colour = "red"
			};
			var thing5ArrivedAt = cascade.NowMs;			
			await sqliteThingCache.Store(thing5.id,thing5,thing5ArrivedAt);

			origin.IncNowMs();
			
			var gadget6 = new Gadget() {
				id = "abc",
				weight = 2.5,
				power = 9.2
			};
			var gadget6ArrivedAt = cascade.NowMs;
			await sqliteGadgetCache.Store(gadget6.id,gadget6,gadget6ArrivedAt);
			
			origin.IncNowMs();

			var opResponse = await sqliteThingCache.Fetch(RequestOp.GetOp<Thing>(thing5.id, cascade.NowMs));
			var loaded5 = (opResponse.Result as Thing)!;
			Assert.AreEqual(thing5ArrivedAt,opResponse.ArrivedAtMs);
			Assert.AreEqual(thing5.colour,loaded5.colour);
			
			opResponse = await sqliteGadgetCache.Fetch(RequestOp.GetOp<Gadget>(gadget6.id, cascade.NowMs));
			var loaded6 = (opResponse.Result as Gadget)!;
			Assert.AreEqual(gadget6ArrivedAt,opResponse.ArrivedAtMs);
			Assert.AreEqual(gadget6.weight,loaded6.weight);

			await sqliteGadgetCache.Clear();
			
			// thing unaffected
			opResponse = await sqliteThingCache.Fetch(RequestOp.GetOp<Thing>(thing5.id, cascade.NowMs));
			Assert.NotNull(opResponse.Result);
			Assert.AreEqual(thing5ArrivedAt,opResponse.ArrivedAtMs);
			
			// gadget cleared including metadata
			opResponse = await sqliteGadgetCache.Fetch(RequestOp.GetOp<Gadget>(gadget6.id, cascade.NowMs));
			Assert.IsNull(opResponse.Result);
			Assert.IsNull(opResponse.ArrivedAtMs);
			var meta6 = await db.Get<CascadeModelMeta>(CascadeModelMeta.GenerateId<Gadget>(6));
			Assert.IsNull(meta6);
		}
		
		[Test]
		public async Task SimpleReadThroughCache() {
			var path = System.IO.Path.GetTempFileName();
			var conn = new SQLite.SQLiteAsyncConnection(path);
			var db = new TestDatabase(conn);
			await db.Reset();
			
			var sqliteThingCache = new SqliteClassCache<Thing, long>(db);
			await sqliteThingCache.Setup();
			var cache1 = new ModelCache(aClassCache: new Dictionary<Type, IModelClassCache>() {
				{ typeof(Thing), sqliteThingCache }
			});

			// read from origin
			var cascade = new CascadeDataLayer(origin, new ICascadeCache[] { cache1 }, new CascadeConfig() { DefaultFreshnessSeconds = 1 });
			var thing1 = await cascade.Get<Thing>(5);

			Assert.AreEqual(5, thing1!.id);
			Assert.AreEqual(cascade.NowMs, thing1.updatedAtMs);

			origin.IncNowMs();
			
			var thing2 = await cascade.Get<Thing>(5, freshnessSeconds: 2);
			Assert.AreEqual(thing1.updatedAtMs, thing2!.updatedAtMs);
			
			var thing3 = await cascade.Get<Thing>(5, freshnessSeconds: 0);
			Assert.AreEqual(origin.NowMs, thing3!.updatedAtMs);
		}
	}
}
