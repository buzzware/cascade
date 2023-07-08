using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using StandardExceptions;

namespace Cascade.Test {

	[TestFixture]
	public class OfflineTests {
		private string tempDir;
		MockOrigin2 origin;
		MockModelClassOrigin<Thing> thingOrigin;
		CascadeDataLayer cascade;
		private ModelCache modelCache;

		private ModelClassCache<Thing,int> thingMemoryCache;
		//private FileSystemClassCache<Cascade.Test.Thing,Int32> thingModelCache;

		[SetUp]
		public void SetUp() {
			//tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
			//Log.Debug($"Cascade cache directory {tempDir}");
			//Directory.CreateDirectory(tempDir);
			thingOrigin = new MockModelClassOrigin<Thing>();
			origin = new MockOrigin2(
				new Dictionary<Type, IModelClassOrigin>() {
					{ typeof(Thing), thingOrigin }
				},
				1000
			);
			//thingModelCache = new FileSystemClassCache<Cascade.Test.Thing, int>(tempDir);
			thingMemoryCache = new ModelClassCache<Thing, int>();
			modelCache = new ModelCache	(aClassCache: new Dictionary<Type, IModelClassCache>() {
				{ typeof(Cascade.Test.Thing), thingMemoryCache }
			});
			
			//fileSystemCache = new FileSystemCache(tempDir);
			cascade = new CascadeDataLayer(
				origin, 
				new ICascadeCache[] { modelCache }, 
				new CascadeConfig(),
				new MockCascadePlatform(),
				ErrorControl.Instance,
				new CascadeJsonSerialization()
			);
		}

		[TearDown]
		public void TearDown() {
			// if (Directory.Exists(tempDir)) {
			// 	Directory.Delete(tempDir, true);
			// }
		}

		[Test]
		public void IdType() {
			var docketIdType = CascadeTypeUtils.GetCascadeIdType(typeof(Child));
			Assert.That(docketIdType,Is.EqualTo(typeof(string)));

			var thingIdType = CascadeTypeUtils.GetCascadeIdType(typeof(Thing));
			Assert.That(thingIdType,Is.EqualTo(typeof(Int32)));
			// var monsterIdType = CascadeTypeUtils.GetCascadeIdType(typeof(Monster));
			// Assert.That(monsterIdType,Is.EqualTo(typeof(Int32)));
		}

		[Test]
		public async Task SimpleOnlineCacheAndOriginByFreshness() {
			// setup cascade with mock origin
			var inThing = new Thing() {
				id = 1,
				colour = "green"
			};
			await modelCache.Store(typeof(Thing), inThing.id, inThing, origin.NowMs);

			// get something online from origin
			var thing = await cascade.Get<Thing>(inThing.id);

			Assert.That(cascade.ConnectionOnline, Is.True);

			var originThing = new Thing() {
				id = 1,
				colour = "red"
			};
			thingOrigin.Store(1, originThing);
			
			origin.NowMs += 6 * 60000;
			var lateThing = await cascade.Get<Thing>(inThing.id, freshnessSeconds: 5 * 60);
			Assert.That(lateThing, Is.SameAs(originThing));
			Assert.That(cascade.ConnectionOnline, Is.True);
		}


		[Test]
		public async Task SimpleOfflineCache() {
			// setup cascade with mock origin
			var cacheThing = new Thing() {
				id = 1,
				colour = "green"
			};
			await modelCache.Store(typeof(Thing), cacheThing.id, cacheThing, origin.NowMs);
			
			// get something online, should return from cache
			var thing = await cascade.Get<Thing>(cacheThing.id);
			Assert.That(thing,Is.SameAs(cacheThing));
			Assert.That(cascade.ConnectionOnline,Is.True);
			
			// go cause offline exception and delay 
			origin.ActLikeOffline = true;
			origin.NowMs += 10*60000;
			
			// try to get same thing again with overdue freshness - should return from cache anyway
			var offlineThing = await cascade.Get<Thing>(cacheThing.id, freshnessSeconds: 5*60);
			Assert.That(offlineThing,Is.SameAs(cacheThing));
			Assert.That(cascade.ConnectionOnline,Is.False);
			
			// try again with freshness=0 - should still return from cache
			offlineThing = await cascade.Get<Thing>(cacheThing.id, freshnessSeconds: 0);
			Assert.That(offlineThing,Is.SameAs(cacheThing));
			
			// insist on fresh from origin, but origin doesn't have it, so should throw NotAvailableOffline 
			Assert.ThrowsAsync<DataNotAvailableOffline>(async () => await cascade.Get<Thing>(cacheThing.id, freshnessSeconds: -1));
		}
	}
}
