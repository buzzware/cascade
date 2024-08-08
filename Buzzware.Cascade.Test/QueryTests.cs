
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Buzzware.Cascade.Testing;
using NUnit.Framework;
using Buzzware.StandardExceptions;

namespace Buzzware.Cascade.Test {

  /// <summary>
  /// Tests the querying functionality of the Cascade library by simulating a data layer with mock data stores.
  /// Validates different aspects of caching and data retrieval using simple queries.
  /// </summary>
	[TestFixture]
	public class QueryTests {

    // Variables for holding mock data origins
		MockOrigin2 origin;
		MockModelClassOrigin<Parent> thingOrigin;
		MockModelClassOrigin<Child> gadgetOrigin;

    /// <summary>
    /// Sets up the mock environment for each test. It initializes data origins and an overall mock data source.
    /// </summary>
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

    /// <summary>
    /// Tests the basic querying functionality, including caching and freshness settings, in the Cascade library.
    /// Verifies that results are returned based on specific criteria and validates cache behavior on repeated queries.
    /// </summary>
		[Test]
		public async Task Simple() {

      // Simulating a dataset of Parent objects with different colors
			Parent[] allThings = new[] {
				new Parent() { id = 1, colour = "red" },
				new Parent() { id = 2, colour = "green" },
				new Parent() { id = 3, colour = "red" },
				new Parent() { id = 4, colour = "yellow" },
			};

      // Storing each Parent object in the mock origin store asynchronously
			foreach (var t in allThings)
				await thingOrigin.Store(t.id, t);
      
      // Setting up a model class cache and overall cache to be used by Cascade
			var thingModelStore1 = new ModelClassCache<Parent, long>();
			var cache1 = new ModelCache(aClassCache: new Dictionary<Type, IModelClassCache>() {
				{ typeof(Parent), thingModelStore1 }
			});
      
      // Initializing the CascadeDataLayer with the mock origin, caches and other configurations
			var cascade = new CascadeDataLayer(origin, new ICascadeCache[] { cache1 }, new CascadeConfig(), new MockCascadePlatform(), ErrorControl.Instance, new CascadeJsonSerialization());
			
      // Querying for Parent objects where the color is 'red'
      var redThings = await cascade.Query<Parent>("red_things", new JsonObject {
				["colour"] = "red"
			});
			var redIds = redThings.Select(t => t.id).ToImmutableArray();
			Assert.That(redIds, Is.EqualTo(new long[] {1, 3}));
			
			// Check that the queried Parent objects are correctly cached
			Parent? thing;
			thing = await thingModelStore1.Fetch<Parent>(1);
			Assert.AreEqual(1, thing.id);
			Assert.AreEqual("red", thing.colour);
			thing = await thingModelStore1.Fetch<Parent>(3);
			Assert.AreEqual(3, thing.id);
			Assert.AreEqual("red", thing.colour);
			thing = await thingModelStore1.Fetch<Parent>(2);
			Assert.AreEqual(null, thing); // Item not queried/shouldn't exist in cache

      // Check request count when cache should fulfill query without hitting origin
			var rcBefore = origin.RequestCount;
			origin.IncNowMs();
			
      // Query with a freshness parameter
      var redThings2 = await cascade.Query<Parent>("red_things", new JsonObject {
				["colour"] = "red"
			}, freshnessSeconds: 2);

      // Verify result consistency and validate no additional origin requests
			Assert.That(redThings2.Select(t => t.id).ToImmutableArray(), Is.EqualTo(new long[] {1, 3}));	// same response
			Assert.AreEqual(rcBefore, origin.RequestCount);	// didn't use origin

      // Validate behavior when freshness imposes a forced origin request
			rcBefore = origin.RequestCount;
			origin.IncNowMs();
			
			redThings2 = (await cascade.Query<Parent>("red_things", new JsonObject {
				["colour"] = "red"
			}, freshnessSeconds: 0)).ToImmutableArray();
			Assert.That(redThings2.Select(t => t.id).ToImmutableArray(), Is.EqualTo(new long[] {1, 3}));	// same response
			Assert.AreEqual(rcBefore + 1, origin.RequestCount);	// did use origin

      // Retrieve cached collection and validate it matches queried ids
			var red_things_collection = await cascade.GetCollection<Parent>("red_things");
			Assert.That(red_things_collection, Is.EqualTo(new long[] { 1, 3 }));
		}
	}
}
