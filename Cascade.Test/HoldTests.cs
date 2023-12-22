using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using Serilog;
using StandardExceptions;

namespace Cascade.Test {

	[TestFixture]
	public class HoldTests {
		private string tempDir;
		MockOrigin2 origin;
		MockModelClassOrigin<Thing> thingOrigin;
		CascadeDataLayer cascade;
		private ModelCache modelCache;

		private ModelClassCache<Thing, int> thingMemoryCache;
		//private FileSystemClassCache<Cascade.Test.Thing,Int32> thingModelCache;

		[SetUp]
		public void SetUp() {
			tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
			Log.Debug($"Cascade cache directory {tempDir}");
			Directory.CreateDirectory(tempDir);
			thingOrigin = new MockModelClassOrigin<Thing>();
			origin = new MockOrigin2(
				new Dictionary<Type, IModelClassOrigin>() {
					{ typeof(Thing), thingOrigin }
				},
				1000
			);
			//thingModelCache = new FileSystemClassCache<Cascade.Test.Thing, int>(tempDir);
			thingMemoryCache = new ModelClassCache<Thing, int>();
			modelCache = new ModelCache(aClassCache: new Dictionary<Type, IModelClassCache>() {
				{ typeof(Cascade.Test.Thing), thingMemoryCache }
			});

			//fileSystemCache = new FileSystemCache(tempDir);
			cascade = new CascadeDataLayer(
				origin,
				new ICascadeCache[] { modelCache },
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
			await modelCache.Store(typeof(Thing), thing1.id, thing1, origin.NowMs);
			var thing2 = new Thing() {
				id = 2,
				colour = "red"
			};
			await modelCache.Store(typeof(Thing), thing2.id, thing1, origin.NowMs);

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
			
			Assert.That((await modelCache.Fetch(RequestOp.GetOp<Thing>(thing1.id, freshnessSeconds: 0))).Exists,Is.True);
			Assert.That((await modelCache.Fetch(RequestOp.GetOp<Thing>(thing2.id, freshnessSeconds: 0))).Exists,Is.True);

			Assert.That((await modelCache.Fetch(RequestOp.GetCollectionOp<Thing>(collAName))).Exists,Is.True);
			Assert.That((await modelCache.Fetch(RequestOp.GetCollectionOp<Thing>(collBName))).Exists,Is.True);
			
			await modelCache.ClearAll(exceptHeld: true);
		
			Assert.That((await modelCache.Fetch(RequestOp.GetOp<Thing>(thing1.id, freshnessSeconds: 0))).Exists,Is.True);
			Assert.That((await modelCache.Fetch(RequestOp.GetOp<Thing>(thing2.id, freshnessSeconds: 0))).Exists,Is.False);
			
			Assert.That((await modelCache.Fetch(RequestOp.GetCollectionOp<Thing>(collAName))).Exists,Is.True);
			Assert.That((await modelCache.Fetch(RequestOp.GetCollectionOp<Thing>(collBName))).Exists,Is.False);
		}
	}
}
