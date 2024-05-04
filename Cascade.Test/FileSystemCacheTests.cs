using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Cascade.Testing;
using NUnit.Framework;
using Serilog;
using Buzzware.StandardExceptions;

namespace Cascade.Test {
	[TestFixture]
	public class FileSystemCacheTests {
		private string tempDir;
		MockOrigin2 origin;
		MockModelClassOrigin<Thing> thingOrigin;
		CascadeDataLayer cascade;
		private ModelCache modelCache;
		private FileSystemClassCache<Thing,Int32> thingModelCache;

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
			thingModelCache = new FileSystemClassCache<Thing, int>(tempDir);
			modelCache = new ModelCache	(aClassCache: new Dictionary<Type, IModelClassCache>() {
				{ typeof(Thing), thingModelCache }
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
		public async Task ModelCacheStoreAndFetch() {
			var inThing = new Thing() {
				id = 1,
				colour = "green"
			};
			await modelCache.Store(typeof(Thing), inThing.id, inThing, origin.NowMs);
			
			origin.NowMs += 1;
			
			var getOp = RequestOp.GetOp<Thing>(inThing.id, origin.NowMs);
			var outResponse = await modelCache.Fetch(getOp);

			var outThing = (outResponse.Result as Thing)!;
			Assert.That(outThing.id,Is.EqualTo(1));
			Assert.That(outThing.colour,Is.EqualTo("green"));
			
			Assert.That(outResponse.Connected, Is.True);
			Assert.That(outResponse.Exists, Is.True);
			Assert.That(outResponse.ArrivedAtMs, Is.EqualTo(1000));
			Assert.That(outResponse.TimeMs, Is.EqualTo(origin.NowMs));
		}

		[Test]
		public async Task ModelCacheStoreAndFetchCollection() {
			var inIds = new int[] {1,2,3};
			var name = "THINGS";
			await modelCache.StoreCollection(
				typeof(Thing),
				name,
				inIds, 
				origin.NowMs
			);
			
			origin.NowMs += 1;
			
			var getOp = RequestOp.GetCollectionOp<Thing>(name, origin.NowMs);
			var outResponse = await modelCache.Fetch(getOp);
			
			Assert.That(outResponse.ResultIds,Is.EqualTo(inIds));
		}

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
			
			cascade.Hold<Thing>(1);
			cascade.HoldCollection<Thing>(coll1Name);
			
			Assert.That((await modelCache.Fetch(RequestOp.GetOp<Thing>(thing1.id, freshnessSeconds: 0))).Exists,Is.True);
			Assert.That((await modelCache.Fetch(RequestOp.GetOp<Thing>(thing2.id, freshnessSeconds: 0))).Exists,Is.True);
			
			Assert.That((await modelCache.Fetch(RequestOp.GetCollectionOp<Thing>(coll1Name))).Exists,Is.True);
			Assert.That((await modelCache.Fetch(RequestOp.GetCollectionOp<Thing>(coll2Name))).Exists,Is.True);
			
			await modelCache.ClearAll(exceptHeld: true);

			Assert.That((await modelCache.Fetch(RequestOp.GetOp<Thing>(thing1.id, freshnessSeconds: 0))).Exists,Is.True);
			Assert.That((await modelCache.Fetch(RequestOp.GetOp<Thing>(thing2.id, freshnessSeconds: 0))).Exists,Is.False);
			
			Assert.That((await modelCache.Fetch(RequestOp.GetCollectionOp<Thing>(coll1Name))).Exists,Is.True);
			Assert.That((await modelCache.Fetch(RequestOp.GetCollectionOp<Thing>(coll2Name))).Exists,Is.False);
		}
	}
}
