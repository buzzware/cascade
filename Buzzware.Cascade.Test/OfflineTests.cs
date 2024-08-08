
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Buzzware.Cascade.Testing;
using NUnit.Framework;
using Buzzware.StandardExceptions;

namespace Buzzware.Cascade.Test {

	/// <summary>
	/// Test the offline and caching functionality of the Cascade library.
	/// Includes tests dealing with data retrieval, caching, and online versus offline mode handling,
	/// using mock objects and model origins to simulate application behavior.
	/// </summary>
	[TestFixture]
	public class OfflineTests {
		private string tempDir;
		MockOrigin2 origin;
		MockModelClassOrigin<Thing> thingOrigin;
		CascadeDataLayer cascade;
		private ModelCache modelCache;

		private ModelClassCache<Thing,int> thingMemoryCache;

		/// <summary>
		/// Sets up the necessary objects and mock configurations for each test case.
		/// Initializes mocks for model origins and caches, configuring a Cascade data layer.
		/// </summary>
		[SetUp]
		public void SetUp() {
			thingOrigin = new MockModelClassOrigin<Thing>();
			origin = new MockOrigin2(
				new Dictionary<Type, IModelClassOrigin>() {
					{ typeof(Thing), thingOrigin }
				},
				1000
			);
			thingMemoryCache = new ModelClassCache<Thing, int>();
			modelCache = new ModelCache	(aClassCache: new Dictionary<Type, IModelClassCache>() {
				{ typeof(Thing), thingMemoryCache }
			});
			
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
		}

		/// <summary>
		/// Test the GetCascadeIdType utility to ensure correct ID type retrieval for different model classes.
		/// </summary>
		[Test]
		public void IdType() {
			var docketIdType = CascadeTypeUtils.GetCascadeIdType(typeof(Child));
			Assert.That(docketIdType,Is.EqualTo(typeof(string)));

			var thingIdType = CascadeTypeUtils.GetCascadeIdType(typeof(Thing));
			Assert.That(thingIdType,Is.EqualTo(typeof(Int32)));
		}

		/// <summary>
		/// Tests caching behavior when data is online.
		/// Verifies that the cached item is retrieved via the origin when it surpasses freshness threshold.
		/// </summary>
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

			// Simulate a more recent data from the origin
			var originThing = new Thing() {
				id = 1,
				colour = "red"
			};
			thingOrigin.Store(1, originThing);
			
			// Advance time to simulate data staleness and retrieve again
			origin.NowMs += 6 * 60000;
			var lateThing = await cascade.Get<Thing>(inThing.id, freshnessSeconds: 5 * 60);
			Assert.That(lateThing, Is.SameAs(originThing));
			Assert.That(cascade.ConnectionOnline, Is.True);
		}


		/// <summary>
		/// Tests the behavior of the cache when the storage is offline.
		/// Ensures data can still be retrieved from cache regardless of freshness once offline.
		/// Validates exception handling when only fresh data is required but unavailable.
		/// </summary>
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
			
			// Simulate offline state by causing an offline exception
			origin.ActLikeOffline = true;
			origin.NowMs += 10*60000;
			
			// try to get same thing again with overdue freshness - should return from cache anyway
			var offlineThing = await cascade.Get<Thing>(cacheThing.id, freshnessSeconds: 5*60, fallbackFreshnessSeconds: RequestOp.FRESHNESS_ANY);
			Assert.That(offlineThing,Is.SameAs(cacheThing));
			Assert.That(cascade.ConnectionOnline,Is.True);	// Ensures connection still considered online

			cascade.ConnectionOnline = false;
			
			// Retrieve from cache under fresh demand, despite offline
			offlineThing = await cascade.Get<Thing>(cacheThing.id, freshnessSeconds: 0);
			Assert.That(offlineThing,Is.SameAs(cacheThing));
			
			// Throws NotAvailableOffline when demanding fresh data that is not available
			Assert.ThrowsAsync<DataNotAvailableOffline>(async () => await cascade.Get<Thing>(cacheThing.id, freshnessSeconds: -1));
		}
	}
}
