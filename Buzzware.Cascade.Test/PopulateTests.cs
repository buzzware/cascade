
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
  /// Test the data population features of the Cascade library.
  /// It includes setup and teardown methods, and a test to verify handling objects with a 
  /// one-to-many relationship and checking the 'hold' functionality within the Cascade library.
  /// </summary>
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

		/// <summary>
		/// SetUp method initializes the test environment.
    /// Configure the mock origins and model caches, and prepares the CascadeDataLayer with relevant configurations.
		/// </summary>
		[SetUp]
		public void SetUp() {
			// Create a temporary directory for testing purposes
			tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
			Log.Debug($"Buzzware.Cascade cache directory {tempDir}");
			Directory.CreateDirectory(tempDir);

			// Initialize mock origins for Parent and Child model classes
			parentOrigin = new MockModelClassOrigin<Parent>();
			childOrigin = new MockModelClassOrigin<Child>();
			origin = new MockOrigin2(
				new Dictionary<Type, IModelClassOrigin>() {
					{ typeof(Parent), parentOrigin },
					{ typeof(Child), childOrigin },
				},
				1000
			);

			// Initialize model cache for Thing model
			modelCache = new ModelCache(aClassCache: new Dictionary<Type, IModelClassCache>() {
				{ typeof(Thing), new ModelClassCache<Thing, int>() }
			});

			// Initialize file system cache for Thing model
			fileCache = new ModelCache(aClassCache: new Dictionary<Type, IModelClassCache>() {
				{ typeof(Thing), new FileSystemClassCache<Thing, int>(tempDir) }
			});

			// Creating a CascadeDataLayer with configuration and caching strategies
			cascade = new CascadeDataLayer(
				origin,
				new ICascadeCache[] { modelCache, fileCache },
				new CascadeConfig() { StoragePath = tempDir },
				new MockCascadePlatform(),
				ErrorControl.Instance,
				new CascadeJsonSerialization()
			);
		}

		/// <summary>
		/// TearDown method cleans up the test environment, ensuring no residual temporary files or directories remain.
		/// </summary>
		[TearDown]
		public void TearDown() {
			if (Directory.Exists(tempDir)) {
				Directory.Delete(tempDir, true);
			}
		}

		/// <summary>
		/// Test the retrieval of a Parent model and its associated Children using the Cascade Get method.
		/// Verifies that both the Parent and its Children are marked as held within the Cascade data layer.
		/// </summary>
		[Test]
		public async Task GetHasManyHold() {
			// Define Parent instances and store them in the mock origin
			Parent[] allParent = new[] {
				new Parent() { id = 1, colour = "red" },
				new Parent() { id = 2, colour = "green" }
			};
			foreach (var p in allParent)
				await parentOrigin.Store(p.id, p);

			// Define Child instances and store them in the mock origin
			Child[] allChildren = new[] {
				new Child() { id = "5", parentId = 1, age = 2 },
				new Child() { id = "6", parentId = 1, age = 4 },
				new Child() { id = "7", parentId = 2, age = 5 },
				new Child() { id = "8", parentId = 2, age = 7 },
			};
			foreach (var c in allChildren)
				await childOrigin.Store(c.id, c);
			
			// Retrieve a Parent with populated Children and the hold flag set to true
			var parent = await cascade.Get<Parent>(1, populate: new string[] {"Children"}, hold: true);

			// Assert that the Parent and its Children are marked as held in the Cascade layer
			Assert.That(cascade.IsHeld<Parent>(parent.id),Is.True);
			Assert.That(cascade.IsHeld<Child>("5"),Is.True);
			Assert.That(cascade.IsHeld<Child>("6"),Is.True);

			// Assert that the collection of Children is held for the specified Parent
			Assert.That(cascade.IsCollectionHeld<Child>(
				CascadeUtils.WhereCollectionKey(nameof(Child), nameof(Child.parentId), parent.id.ToString())),
				Is.True
			);
		}
	}
}
