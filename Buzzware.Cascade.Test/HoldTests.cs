
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
	/// Test suite for verifying the functionality of holding and un-holding models and collections
	/// within the Cascade library, as well as validating cache clearing operations while preserving held objects.
	/// </summary>
	[TestFixture]
	public class HoldTests {
		private string tempDir;
		MockOrigin2 origin;
		MockModelClassOrigin<Thing> thingOrigin;
		CascadeDataLayer cascade;

		private ModelClassCache<Thing, int> thingMemoryCache;
		private ModelCache memoryCache;

		private FileSystemClassCache<Thing,int> thingFileCache;
		private ModelCache fileCache;

		/// <summary>
		/// Sets up the required resources, origins, and caches necessary for running the tests.
		/// It initializes the directory for file caching, memory and file caches, and the cascade data layer with mock origins and configurations.
		/// </summary>
		[SetUp]
		public void SetUp() {
			tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
			Log.Debug($"Buzzware.Cascade cache directory {tempDir}");
			Directory.CreateDirectory(tempDir);
			thingOrigin = new MockModelClassOrigin<Thing>();
			
			origin = new MockOrigin2(
				new Dictionary<Type, IModelClassOrigin>() {
					{ typeof(Thing), thingOrigin }
				},
				1000
			);
			
			thingMemoryCache = new ModelClassCache<Thing, int>();
			memoryCache = new ModelCache(aClassCache: new Dictionary<Type, IModelClassCache>() {
				{ typeof(Thing), thingMemoryCache }
			});
			
			thingFileCache = new FileSystemClassCache<Thing, int>(tempDir);
			fileCache = new ModelCache(
				aClassCache: new Dictionary<Type, IModelClassCache>() {
					{ typeof(Thing), new FileSystemClassCache<Thing, int>(tempDir) }
				},
				new FileBlobCache(tempDir)
			);
			
			cascade = new CascadeDataLayer(
				origin,
				new ICascadeCache[] { memoryCache, fileCache },
				new CascadeConfig() {StoragePath = tempDir},
				new MockCascadePlatform(),
				ErrorControl.Instance,
				new CascadeJsonSerialization()
			);
		}

		[TearDown]
		public void TearDown() {
			if (Directory.Exists(tempDir)) {
				Directory.Delete(tempDir, true);
			}
		}

		/// <summary>
		/// Tests holding and un-holding single model instances within the Cascade library's cache
		/// and verifies the ability to query held model identifiers.
		/// </summary>
		[Test]
		public async Task HoldUnholdIsHeldListHeldIds() {
			var thing1 = new Thing() {
				id = 1,
				colour = "green"
			};
			var thing2 = new Thing() {
				id = 2,
				colour = "red"
			};

			// Test holding and verifying the isHeld status for a model instance and another not held
			cascade.Hold<Thing>(thing1.id);
			Assert.That(cascade.IsHeld<Thing>(thing1.id), Is.True);
			Assert.That(cascade.IsHeld<Thing>(thing2.id), Is.False);

			// Verify listing held IDs returns expected results
			var held = cascade.ListHeldIds<Thing>();
			Assert.That(held,Is.EquivalentTo(new int[] {thing1.id}));
			
			// Test un-holding and verify the isHeld status changes to false
			cascade.Unhold<Thing>(thing1.id);
			Assert.That(cascade.IsHeld<Thing>(thing1.id), Is.False);
			
			// Ensure no exception if un-holding a non-held entity
			cascade.Unhold<Thing>(thing2.id);

			// Verify listing held IDs returns empty
			held = cascade.ListHeldIds<Thing>();
			Assert.That(held,Is.EquivalentTo(new int[] {}));
		}

		/// <summary>
		/// Tests holding and un-holding collections of model instances within the Cascade library's cache
		/// and verifies the ability to query held collections.
		/// </summary>
		[Test]
		public async Task HoldUnholdIsHeldListHeldCollections() {
			const string coll1Name = "red_things";
			const string coll2Name = "blue_things";

			// Set up initial collections in the cache
			await cascade.SetCollection<Thing>(coll1Name, new object[] { 1, 2, 3 });
			await cascade.SetCollection<Thing>(coll2Name, new object[] { 4, 5, 6 });
			
			// Test holding and verifying the isHeld status for a collection and another not held
			cascade.HoldCollection<Thing>(coll1Name);
			Assert.That(cascade.IsCollectionHeld<Thing>(coll1Name), Is.True);
			Assert.That(cascade.IsCollectionHeld<Thing>(coll2Name), Is.False);

			// Verify listing held collections returns expected results
			var held = cascade.ListHeldCollections(typeof(Thing));
			Assert.That(held,Is.EquivalentTo(new string[] {coll1Name}));
			
			// Test un-holding and verify the isHeld status changes to false
			cascade.UnholdCollection<Thing>(coll1Name);
			Assert.That(cascade.IsCollectionHeld<Thing>(coll1Name), Is.False);
			
			// Ensure no exception if un-holding a non-held collection
			cascade.UnholdCollection<Thing>(coll2Name);

			// Verify listing held collections returns empty
			held = cascade.ListHeldCollections(typeof(Thing));
			Assert.That(held,Is.EquivalentTo(new string[] {}));
		}

		/// <summary>
		/// Tests clearing the model cache while ensuring held models remain intact,
		/// and verifies that non-held models are cleared from the cache.
		/// </summary>
		[Test]
		public async Task ModelCacheClearAllExceptHeld() {
			// Initialize models and store them in memory cache
			var thing1 = new Thing() {
				id = 1,
				colour = "green"
			};
			await memoryCache.Store(typeof(Thing), thing1.id, thing1, origin.NowMs);

			var thing2 = new Thing() {
				id = 2,
				colour = "red"
			};
			await memoryCache.Store(typeof(Thing), thing2.id, thing1, origin.NowMs);

			// Initialize collections and store them in memory cache
			var collAName = "A";
			var collBName = "B";
			await cascade.SetCollection<Thing>(collAName, new object[] { 1, 2 });
			await cascade.SetCollection<Thing>(collBName, new object[] { 2, 1 });
			
			// Test holding one model and a collection
			cascade.Hold<Thing>(thing1.id);
			Assert.That(cascade.IsHeld<Thing>(thing1.id),Is.True);
			Assert.That(cascade.IsHeld<Thing>(thing2.id),Is.False);
			
			cascade.HoldCollection<Thing>(collAName);
			Assert.That(cascade.IsCollectionHeld<Thing>(collAName),Is.True);
			Assert.That(cascade.IsCollectionHeld<Thing>(collBName),Is.False);
			
			// Ensure both models and collections exist in memory
			Assert.That((await memoryCache.Fetch(RequestOp.GetOp<Thing>(thing1.id, freshnessSeconds: 0, timeMs: origin.NowMs))).Exists,Is.True);
			Assert.That((await memoryCache.Fetch(RequestOp.GetOp<Thing>(thing2.id, freshnessSeconds: 0, timeMs: origin.NowMs))).Exists,Is.True);

			Assert.That((await memoryCache.Fetch(RequestOp.GetCollectionOp<Thing>(collAName, timeMs: origin.NowMs))).Exists,Is.True);
			Assert.That((await memoryCache.Fetch(RequestOp.GetCollectionOp<Thing>(collBName, timeMs: origin.NowMs))).Exists,Is.True);
			
			// Clear all in the memory cache except those held
			await memoryCache.ClearAll(exceptHeld: true);

			// Verify held model and collection persist, while others are cleared
			Assert.That((await memoryCache.Fetch(RequestOp.GetOp<Thing>(thing1.id, freshnessSeconds: 0, timeMs: origin.NowMs))).Exists,Is.True);
			Assert.That((await memoryCache.Fetch(RequestOp.GetOp<Thing>(thing2.id, freshnessSeconds: 0, timeMs: origin.NowMs))).Exists,Is.False);
			
			Assert.That((await memoryCache.Fetch(RequestOp.GetCollectionOp<Thing>(collAName, timeMs: origin.NowMs))).Exists,Is.True);
			Assert.That((await memoryCache.Fetch(RequestOp.GetCollectionOp<Thing>(collBName, timeMs: origin.NowMs))).Exists,Is.False);
		}
		
		/// <summary>
		/// Tests holding and un-holding blob items in the Cascade library,
		/// and verifies the ability to list held blob paths.
		/// </summary>
		[Test]
		public async Task BlobHoldUnholdIsHeldListHeldIds() {
			const string blobPath1 = "a/b/c";
			const string blobPath2 = "a/b/d";
			
			// Test holding a blob and verifying its held status
			cascade.HoldBlob(blobPath1);
			Assert.That(cascade.IsHeldBlob(blobPath1), Is.True);

			// Verify listing held blob paths shows the correct held blobs
			var held = cascade.ListHeldBlobPaths();
			Assert.That(held,Is.EquivalentTo(new string[] {blobPath1}));
			
			// Test un-holding a blob and ensure the status changes
			cascade.UnholdBlob(blobPath1);
			Assert.That(cascade.IsHeldBlob(blobPath1), Is.False);

			// Ensure no exception if un-holding a non-held blob
			cascade.UnholdBlob(blobPath2);

			// Verify held blob paths are empty after un-holding
			held = cascade.ListHeldBlobPaths();
			Assert.That(held,Is.EquivalentTo(new string[] {}));
		}
		
		/// <summary>
		/// Tests clearing the blob cache while ensuring held blobs remain intact,
		/// and verifies that non-held blobs are cleared from the cache.
		/// </summary>
		[Test]
		public async Task BlobModelCacheClearAllExceptHeld() {
			const string blobPath1 = "a/b/c";
			byte[] blob1 = TestUtils.NewBlob(11, 16);
			await cascade.BlobPut(blobPath1, blob1);
			cascade.HoldBlob(blobPath1);	// manual hold
			
			const string blobPath2 = "a/b/d";
			byte[] blob2 = TestUtils.NewBlob(22, 16);
			await cascade.BlobPut(blobPath2, blob2);

			const string blobPath3 = "a/x";
			byte[] blob3 = TestUtils.NewBlob(22, 16);
			await cascade.BlobPut(blobPath3, blob3);
			await cascade.BlobGet(blobPath3, hold: true);		// hold via BlobGet

			// Check the held status of each blob
			Assert.That(cascade.IsHeldBlob(blobPath1),Is.True);
			Assert.That(cascade.IsHeldBlob(blobPath2),Is.False);
			Assert.That(cascade.IsHeldBlob(blobPath3),Is.True);

			// Ensure all blobs exist in cache before clearing
			Assert.That((await fileCache.Fetch(RequestOp.BlobGetOp(blobPath1, freshnessSeconds: 0, timeMs: origin.NowMs))).Exists,Is.True);
			Assert.That((await fileCache.Fetch(RequestOp.BlobGetOp(blobPath2, freshnessSeconds: 0, timeMs: origin.NowMs))).Exists,Is.True);
			Assert.That((await fileCache.Fetch(RequestOp.BlobGetOp(blobPath3, freshnessSeconds: 0, timeMs: origin.NowMs))).Exists,Is.True);

			// Clear the cache except for held blobs
			await fileCache.ClearAll(exceptHeld: true);

			// Verify that held blobs persist and non-held blobs are cleared
			Assert.That(cascade.IsHeldBlob(blobPath1),Is.True);
			Assert.That(cascade.IsHeldBlob(blobPath2),Is.False);
			Assert.That(cascade.IsHeldBlob(blobPath3),Is.True);
			
			Assert.That((await fileCache.Fetch(RequestOp.BlobGetOp(blobPath1, freshnessSeconds: 0, timeMs: origin.NowMs))).Exists,Is.True);
			Assert.That((await fileCache.Fetch(RequestOp.BlobGetOp(blobPath2, freshnessSeconds: 0, timeMs: origin.NowMs))).Exists,Is.False);
			Assert.That((await fileCache.Fetch(RequestOp.BlobGetOp(blobPath3, freshnessSeconds: 0, timeMs: origin.NowMs))).Exists,Is.True);

			// Test the BlobDestroy and ensure it does not affect hold status until explicitly unheld
			await cascade.BlobDestroy(blobPath3);
			Assert.That(cascade.IsHeldBlob(blobPath3),Is.True);	// Destroying does not unhold
			cascade.UnholdBlob(blobPath3);														// Unhold it
			Assert.That(cascade.IsHeldBlob(blobPath3),Is.False);
		}
	}
}
