using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Cascade.Testing;
using NUnit.Framework;
using Serilog;
using StandardExceptions;

namespace Cascade.Test {

	[TestFixture]
	public class PopulateTests {
		private string tempDir;
		MockOrigin2 origin;
		MockModelClassOrigin<Thing> thingOrigin;
		MockModelClassOrigin<Parent> parentOrigin;
		MockModelClassOrigin<Child> childOrigin;
		
		CascadeDataLayer cascade;

		private ModelCache modelCache;

		private ModelCache fileCache;

		[SetUp]
		public void SetUp() {
			tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
			Log.Debug($"Cascade cache directory {tempDir}");
			Directory.CreateDirectory(tempDir);

			parentOrigin = new MockModelClassOrigin<Parent>();
			childOrigin = new MockModelClassOrigin<Child>();
			origin = new MockOrigin2(
				new Dictionary<Type, IModelClassOrigin>() {
					{ typeof(Parent), parentOrigin },
					{ typeof(Child), childOrigin },
				},
				1000
			);

			modelCache = new ModelCache(aClassCache: new Dictionary<Type, IModelClassCache>() {
				{ typeof(Thing), new ModelClassCache<Thing, int>() }
			});

			fileCache = new ModelCache(aClassCache: new Dictionary<Type, IModelClassCache>() {
				{ typeof(Thing), new FileSystemClassCache<Thing, int>(tempDir) }
			});

			cascade = new CascadeDataLayer(
				origin,
				new ICascadeCache[] { modelCache, fileCache },
				new CascadeConfig() { StoragePath = tempDir },
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
		public async Task GetHasManyHold() {
			Parent[] allParent = new[] {
				new Parent() { id = 1, colour = "red" },
				new Parent() { id = 2, colour = "green" }
			};
			foreach (var p in allParent)
				await parentOrigin.Store(p.id, p);
			Child[] allChildren = new[] {
				new Child() { id = "5", parentId = 1, age = 2 },
				new Child() { id = "6", parentId = 1, age = 4 },
				new Child() { id = "7", parentId = 2, age = 5 },
				new Child() { id = "8", parentId = 2, age = 7 },
			};
			foreach (var c in allChildren)
				await childOrigin.Store(c.id, c);
			
			var parent = await cascade.Get<Parent>(1, populate: new string[] {"Children"}, hold: true);
			
			Assert.That(cascade.IsHeld<Parent>(parent.id),Is.True);
			Assert.That(cascade.IsHeld<Child>("5"),Is.True);
			Assert.That(cascade.IsHeld<Child>("6"),Is.True);
			Assert.That(cascade.IsCollectionHeld<Child>(
				CascadeUtils.WhereCollectionKey(nameof(Child), nameof(Child.parentId), parent.id.ToString())),
				Is.True
			);
		}
	}
}
