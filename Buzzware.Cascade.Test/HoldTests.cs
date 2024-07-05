using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Buzzware.Cascade.Testing;
using NUnit.Framework;
using Serilog;
using Buzzware.StandardExceptions;

namespace Buzzware.Cascade.Test {

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

			cascade.Hold<Thing>(thing1.id);
			Assert.That(cascade.IsHeld<Thing>(thing1.id), Is.True);
			Assert.That(cascade.IsHeld<Thing>(thing2.id), Is.False);

			var held = cascade.ListHeldIds<Thing>();
			Assert.That(held,Is.EquivalentTo(new int[] {thing1.id}));
			
			cascade.Unhold<Thing>(thing1.id);
			Assert.That(cascade.IsHeld<Thing>(thing1.id), Is.False);
			
			cascade.Unhold<Thing>(thing2.id);	// shouldn't crash
			
			held = cascade.ListHeldIds<Thing>();
			Assert.That(held,Is.EquivalentTo(new int[] {}));
		}

		[Test]
		public async Task HoldUnholdIsHeldListHeldCollections() {
			const string coll1Name = "red_things";
			const string coll2Name = "blue_things";
			await cascade.SetCollection<Thing>(coll1Name, new object[] { 1, 2, 3 });
			await cascade.SetCollection<Thing>(coll2Name, new object[] { 4, 5, 6 });
			
			cascade.HoldCollection<Thing>(coll1Name);
			Assert.That(cascade.IsCollectionHeld<Thing>(coll1Name), Is.True);
			Assert.That(cascade.IsCollectionHeld<Thing>(coll2Name), Is.False);

			var held = cascade.ListHeldCollections(typeof(Thing));
			Assert.That(held,Is.EquivalentTo(new string[] {coll1Name}));
			
			cascade.UnholdCollection<Thing>(coll1Name);
			Assert.That(cascade.IsCollectionHeld<Thing>(coll1Name), Is.False);
			
			cascade.UnholdCollection<Thing>(coll2Name);	// shouldn't crash
			
			held = cascade.ListHeldCollections(typeof(Thing));
			Assert.That(held,Is.EquivalentTo(new string[] {}));
			
		}

		[Test]
		public async Task ModelCacheClearAllExceptHeld() {
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

			var collAName = "A";
			//var collAName = CascadeUtils.CollectionKeyFromName(nameof(Thing), "A");
			var collBName = "B";
			//var collBName = CascadeUtils.CollectionKeyFromName(nameof(Thing), "B");
			
			// await modelCache.StoreCollection(typeof(Thing), collAName, new object[] { 1, 2 }, cascade.NowMs);
			// await modelCache.StoreCollection(typeof(Thing), collBName, new object[] { 2, 1 }, cascade.NowMs);

			await cascade.SetCollection<Thing>(collAName, new object[] { 1, 2 });
			await cascade.SetCollection<Thing>(collBName, new object[] { 2, 1 });
			
			
			cascade.Hold<Thing>(thing1.id);
			Assert.That(cascade.IsHeld<Thing>(thing1.id),Is.True);
			Assert.That(cascade.IsHeld<Thing>(thing2.id),Is.False);
			
			cascade.HoldCollection<Thing>(collAName);
			Assert.That(cascade.IsCollectionHeld<Thing>(collAName),Is.True);
			Assert.That(cascade.IsCollectionHeld<Thing>(collBName),Is.False);
			
			Assert.That((await memoryCache.Fetch(RequestOp.GetOp<Thing>(thing1.id, freshnessSeconds: 0))).Exists,Is.True);
			Assert.That((await memoryCache.Fetch(RequestOp.GetOp<Thing>(thing2.id, freshnessSeconds: 0))).Exists,Is.True);

			Assert.That((await memoryCache.Fetch(RequestOp.GetCollectionOp<Thing>(collAName))).Exists,Is.True);
			Assert.That((await memoryCache.Fetch(RequestOp.GetCollectionOp<Thing>(collBName))).Exists,Is.True);
			
			await memoryCache.ClearAll(exceptHeld: true);
		
			Assert.That((await memoryCache.Fetch(RequestOp.GetOp<Thing>(thing1.id, freshnessSeconds: 0))).Exists,Is.True);
			Assert.That((await memoryCache.Fetch(RequestOp.GetOp<Thing>(thing2.id, freshnessSeconds: 0))).Exists,Is.False);
			
			Assert.That((await memoryCache.Fetch(RequestOp.GetCollectionOp<Thing>(collAName))).Exists,Is.True);
			Assert.That((await memoryCache.Fetch(RequestOp.GetCollectionOp<Thing>(collBName))).Exists,Is.False);
		}
		
		[Test]
		public async Task BlobHoldUnholdIsHeldListHeldIds() {

			const string blobPath1 = "a/b/c";
			const string blobPath2 = "a/b/d";
			
			cascade.HoldBlob(blobPath1);
			Assert.That(cascade.IsHeldBlob(blobPath1), Is.True);

			var held = cascade.ListHeldBlobPaths();
			Assert.That(held,Is.EquivalentTo(new string[] {blobPath1}));
			
			cascade.UnholdBlob(blobPath1);
			Assert.That(cascade.IsHeldBlob(blobPath1), Is.False);
			
			cascade.UnholdBlob(blobPath2);	// shouldn't crash
			
			held = cascade.ListHeldBlobPaths();
			Assert.That(held,Is.EquivalentTo(new string[] {}));
		}
		
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
			
			Assert.That(cascade.IsHeldBlob(blobPath1),Is.True);
			Assert.That(cascade.IsHeldBlob(blobPath2),Is.False);
			Assert.That(cascade.IsHeldBlob(blobPath3),Is.True);
			
			Assert.That((await fileCache.Fetch(RequestOp.BlobGetOp(blobPath1, freshnessSeconds: 0))).Exists,Is.True);
			Assert.That((await fileCache.Fetch(RequestOp.BlobGetOp(blobPath2, freshnessSeconds: 0))).Exists,Is.True);
			Assert.That((await fileCache.Fetch(RequestOp.BlobGetOp(blobPath3, freshnessSeconds: 0))).Exists,Is.True);

			await fileCache.ClearAll(exceptHeld: true);

			Assert.That(cascade.IsHeldBlob(blobPath1),Is.True);
			Assert.That(cascade.IsHeldBlob(blobPath2),Is.False);
			Assert.That(cascade.IsHeldBlob(blobPath3),Is.True);
			
			Assert.That((await fileCache.Fetch(RequestOp.BlobGetOp(blobPath1, freshnessSeconds: 0))).Exists,Is.True);
			Assert.That((await fileCache.Fetch(RequestOp.BlobGetOp(blobPath2, freshnessSeconds: 0))).Exists,Is.False);
			Assert.That((await fileCache.Fetch(RequestOp.BlobGetOp(blobPath3, freshnessSeconds: 0))).Exists,Is.True);
		}
	}
}
