
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Buzzware.Cascade.Testing;
using NUnit.Framework;
using Serilog;
using Buzzware.StandardExceptions;

namespace Buzzware.Cascade.Test {

  /// <summary>
  /// This test fixture class contains unit tests for the FileSystemCache functionality 
  /// within the Cascade library. It verifies the caching behaviors for model objects and collections 
  /// using a file system-based cache mechanism.
  /// </summary>
	[TestFixture]
	public class FileSystemCacheTests {
		private string tempDir;
		MockOrigin2 origin;
		MockModelClassOrigin<Thing> thingOrigin;
		CascadeDataLayer cascade;
		private ModelCache modelCache;
		private FileSystemClassCache<Thing,Int32> thingModelCache;

    /// <summary>
    /// Sets up the testing environment before each test is run. It creates a temporary directory
    /// for the file system cache and initializes the mock origins, model cache, and CascadeDataLayer.
    /// </summary>
		[SetUp]
		public void SetUp() {
			tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
			Log.Debug($"Buzzware.Cascade cache directory {tempDir}");
			Directory.CreateDirectory(tempDir);
			
			// Initialize the mock origins with a type mapping for Thing
			thingOrigin = new MockModelClassOrigin<Thing>();
			origin = new MockOrigin2(
				new Dictionary<Type, IModelClassOrigin>() {
					{ typeof(Thing), thingOrigin }
				},
				1000
			);
			
			// Initialize the file system class cache for Thing
			thingModelCache = new FileSystemClassCache<Thing, int>(tempDir);
			modelCache = new ModelCache	(aClassCache: new Dictionary<Type, IModelClassCache>() {
				{ typeof(Thing), thingModelCache }
			});
			
			// Initialize the cascade data layer with the mock origin and cache
			cascade = new CascadeDataLayer(
				origin, 
				new ICascadeCache[] { modelCache }, 
				new CascadeConfig() {StoragePath = tempDir},
				new MockCascadePlatform(),
				ErrorControl.Instance,
				new CascadeJsonSerialization()
			);
		}

    /// <summary>
    /// Cleans up the testing environment by deleting the temporary cache directory after each test.
    /// </summary>
		[TearDown]
		public void TearDown() {
			if (Directory.Exists(tempDir)) {
				Directory.Delete(tempDir, true);
			}
		}

    /// <summary>
    /// Tests storing and fetching a single model object in the model cache. 
    /// It verifies the object's properties and the connected status of the cache.
    /// </summary>
		[Test]
		public async Task ModelCacheStoreAndFetch() {
			var inThing = new Thing() {
				id = 1,
				colour = "green"
			};

			// Store the model object in the cache
			await modelCache.Store(typeof(Thing), inThing.id, inThing, origin.NowMs);
			
			// Increment the time to simulate passage of time before fetching
			origin.NowMs += 1;
			
			// Fetch the model object from the cache
			var getOp = RequestOp.GetOp<Thing>(inThing.id, origin.NowMs);
			var outResponse = await modelCache.Fetch(getOp);

			// Verify that the fetched object's properties match the stored object
			var outThing = (outResponse.Result as Thing)!;
			Assert.That(outThing.id,Is.EqualTo(1));
			Assert.That(outThing.colour,Is.EqualTo("green"));
			
			// Verify cache response properties
			Assert.That(outResponse.Exists, Is.True);
			Assert.That(outResponse.ArrivedAtMs, Is.EqualTo(1000));
			Assert.That(outResponse.TimeMs, Is.EqualTo(origin.NowMs));
		}

    /// <summary>
    /// Tests storing and fetching a collection of model objects in the model cache.
    /// It verifies that the fetched collection of IDs matches the stored collection.
    /// </summary>
		[Test]
		public async Task ModelCacheStoreAndFetchCollection() {
			var inIds = new int[] {1,2,3};
			var name = "THINGS";

			// Store the collection of IDs in the cache
			await modelCache.StoreCollection(
				typeof(Thing),
				name,
				inIds, 
				origin.NowMs
			);
			
			// Increment the time to simulate passage of time before fetching
			origin.NowMs += 1;
			
			// Fetch the collection from the cache
			var getOp = RequestOp.GetCollectionOp<Thing>(name, origin.NowMs);
			var outResponse = await modelCache.Fetch(getOp);
			
			// Verify that the fetched collection of IDs matches the stored collection
			Assert.That(outResponse.ResultIds,Is.EqualTo(inIds));
		}

    /// <summary>
    /// Tests the clearing of cache while retaining held items. It verifies that only 
    /// specified objects and collections held by the cascade are retained after clearing.
    /// </summary>
		[Test]
		public async Task ClearAllExceptHeld() {
			var thing1 = new Thing() {
				id = 1,
				colour = "green"
			};
			await modelCache.Store(typeof(Thing), thing1.id, thing1, origin.NowMs);
			var thing2 = new Thing() {
				id = 2,
				colour = "red"
			};
			await modelCache.Store(typeof(Thing), thing2.id, thing2, origin.NowMs);

			var coll1 = new int[] { 1, 2 };
			var coll1Name = "coll1";
			var coll2 = new int[] { 2, 1 };
			var coll2Name = "coll2";
			
			// Store collections in the cache
			await modelCache.StoreCollection(
				typeof(Thing),
				coll1Name,
				coll1, 
				origin.NowMs
			);
			await modelCache.StoreCollection(
				typeof(Thing),
				coll2Name,
				coll2, 
				origin.NowMs
			);
			
			// Hold certain items and collections in the cascade
			cascade.Hold<Thing>(1);
			cascade.HoldCollection<Thing>(coll1Name);
			
			OpResponse opResponse;

			// Verify that all objects and collections exist in the cache
			opResponse = await modelCache.Fetch(RequestOp.GetOp<Thing>(thing1.id, freshnessSeconds: 0, timeMs: origin.NowMs));
			Assert.That(opResponse.Exists,Is.True);
			opResponse = await modelCache.Fetch(RequestOp.GetOp<Thing>(thing2.id, freshnessSeconds: 0, timeMs: origin.NowMs));
			Assert.That(opResponse.Exists,Is.True);
			
			Assert.That((await modelCache.Fetch(RequestOp.GetCollectionOp<Thing>(coll1Name, timeMs: origin.NowMs))).Exists,Is.True);
			Assert.That((await modelCache.Fetch(RequestOp.GetCollectionOp<Thing>(coll2Name, timeMs: origin.NowMs))).Exists,Is.True);
			
			// Clear the cache, retaining only held items
			await modelCache.ClearAll(exceptHeld: true);

			// Verify held items still exist while others do not
			Assert.That((await modelCache.Fetch(RequestOp.GetOp<Thing>(thing1.id, freshnessSeconds: 0, timeMs: origin.NowMs))).Exists,Is.True);
			Assert.That((await modelCache.Fetch(RequestOp.GetOp<Thing>(thing2.id, freshnessSeconds: 0, timeMs: origin.NowMs))).Exists,Is.False);
			
			Assert.That((await modelCache.Fetch(RequestOp.GetCollectionOp<Thing>(coll1Name, timeMs: origin.NowMs))).Exists,Is.True);
			Assert.That((await modelCache.Fetch(RequestOp.GetCollectionOp<Thing>(coll2Name, timeMs: origin.NowMs))).Exists,Is.False);
		}
	}
}
