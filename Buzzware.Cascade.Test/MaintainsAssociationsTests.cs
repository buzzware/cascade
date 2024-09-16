using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Buzzware.Cascade.Testing;
using NUnit.Framework;
using Buzzware.StandardExceptions;

namespace Buzzware.Cascade.Test {

	/// <summary>
	/// Test BelongsTo and HasMany associations
	/// </summary>
	[TestFixture]
	public class MaintainsAssociationsTests {

		MockOrigin2 origin;
		MockModelClassOrigin<Parent> parentOrigin;
		MockModelClassOrigin<Child> childOrigin;
		private MockModelClassOrigin<ChildDetail> detailOrigin;

		CascadeDataLayer cascade;
		
		/// <summary>
		/// Sets up the test environment by initializing mock origin objects for Parent and Child entities.
		/// </summary>
		[SetUp]
		public async Task SetUp() {

			// Initialization of mock origins
			parentOrigin = new MockModelClassOrigin<Parent>();
			childOrigin = new MockModelClassOrigin<Child>();
			detailOrigin = new MockModelClassOrigin<ChildDetail>();

			// Setting up origin with a dictionary mapping type to the mock origins
			origin = new MockOrigin2(
				new Dictionary<Type, IModelClassOrigin>() {
					{ typeof(Parent), parentOrigin },
					{ typeof(Child), childOrigin },
					{ typeof(ChildDetail), detailOrigin }
				},
				1000
			);
			
			// Initial setup of parent and child objects
			Parent[] allParents = new[] {
				new Parent() { id = 1, colour = "red" },
				new Parent() { id = 2, colour = "green" }
			};
			Child[] allChildren = new[] {
				new Child() { id = "C1", parentId = 1, tally = 5},
				new Child() { id = "C2", parentId = 1, tally = 7},
				new Child() { id = "C3", parentId = 2, tally = 2},
				new Child() { id = "C4", parentId = 2, tally = 4},
			};
			ChildDetail[] allChildDetails = new[] {
				new ChildDetail() { id = "CD1", childId = "C1", description = "Detail 1" },
				new ChildDetail() { id = "CD2", childId = "C2", description = "Detail 2" },
			};

			// Storing parents and children in their mock origins
			foreach (var p in allParents)
				await parentOrigin.Store(p.id, p);
			foreach (var c in allChildren)
				await childOrigin.Store(c.id, c);
			foreach (var cd in allChildDetails)
				await detailOrigin.Store(cd.id, cd);

			// Setup caches for model classes
			var parentCache = new ModelClassCache<Parent, long>();
			var childCache = new ModelClassCache<Child, string>();
			var childDetailCache = new ModelClassCache<ChildDetail, string>();
			var modelCache = new ModelCache(aClassCache: new Dictionary<Type, IModelClassCache>() {
				{ typeof(Parent), parentCache },
				{ typeof(Child), childCache },
				{ typeof(ChildDetail), childDetailCache }
			});
			
			// Setup the cascade data layer
			
			cascade = new CascadeDataLayer(
				origin, 
				new ICascadeCache[] {
					modelCache
				}, 
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

		/// <summary>
		/// Verifies that the data can be read correctly from the origin when no cache is available.
		/// </summary>
		[Test]
		public async Task ReadWithoutCache() {
			var cascade = new CascadeDataLayer(origin, new ICascadeCache[] { }, new CascadeConfig(), new MockCascadePlatform(), ErrorControl.Instance, new CascadeJsonSerialization());
			var thing = await cascade.Get<Parent>(2);
			Assert.AreEqual(2, thing!.id);
		}

		/// <summary>
		/// Tests that updating the parentId property of a Child causes an internal Cascade Get for the Parent
		/// </summary>
		[Test]
		public async Task UpdateBelongsToAssociation() {
			var child1 = await cascade.Get<Child>("C1", populate: new[] { nameof(Child.Parent) });
			Assert.That(child1.parentId, Is.EqualTo(1));
			Assert.That(child1.Parent.id, Is.EqualTo(child1.parentId));

			var updatedChild = await cascade.Update(child1, new Dictionary<string, object?>() { [nameof(Child.tally)] = 123 });
			Assert.That(updatedChild.tally, Is.EqualTo(123));
			Assert.That(updatedChild.parentId, Is.EqualTo(child1.parentId));
			Assert.That(updatedChild.Parent, Is.SameAs(child1.Parent));
			
			updatedChild = await cascade.Update(updatedChild, new Dictionary<string, object?> {[nameof(Child.parentId)] = 2, [nameof(Child.tally)] = 567});
			
			Assert.That(updatedChild.parentId, Is.EqualTo(2));
			Assert.That(updatedChild.Parent.id, Is.EqualTo(updatedChild.parentId));
			Assert.That(updatedChild.tally, Is.EqualTo(567));
		}

		// /// <summary>
		// /// Tests that Create/Update/Replace operations maintain Parent.HasMany association
		// /// </summary>
		// [Test]
		// public async Task MaintainHasManyAssociation() {
		// 	var parent = await cascade.Get<Parent>(1, populate: new[] { nameof(Parent.Children) });
		// 	var initialChildren = parent.Children.ToList();
		//
		// 	// Test Create
		// 	var newParent = await cascade.Create(new Parent { id = 3, colour = "blue", Children = initialChildren });
		// 	Assert.That(newParent.Children, Is.SameAs(initialChildren));
		//
		// 	// Test Update
		// 	await cascade.Update(parent, new Dictionary<string, object?> {[nameof(Parent.colour)] = "yellow" });
		// 	Assert.That(parent.Children, Is.SameAs(initialChildren));
		//
		// 	// Test Replace
		// 	var replacedParent = await cascade.Replace(parent);
		// 	Assert.That(replacedParent.Children, Is.SameAs(initialChildren));
		// }
		//
		// /// <summary>
		// /// Tests that Create/Update/Replace operations maintain Child.Detail (HasOne) association
		// /// </summary>
		// [Test]
		// public async Task MaintainHasOneAssociation() {
		// 	var child = await cascade.Get<Child>("C1", populate: new[] { nameof(Child.Detail) });
		// 	var initialDetail = child.Detail;
		//
		// 	// Test Create
		// 	var newChild = await cascade.Create(new Child { id = "C5", parentId = 1, tally = 10, Detail = initialDetail });
		// 	Assert.That(newChild.Detail, Is.SameAs(initialDetail));
		//
		// 	// Test Update
		// 	await cascade.Update(child, new Dictionary<string, object?> {[nameof(Child.tally)] = 6 });
		// 	Assert.That(child.Detail, Is.SameAs(initialDetail));
		//
		// 	// Test Replace
		// 	var replacedChild = await cascade.Replace(child);
		// 	Assert.That(replacedChild.Detail, Is.SameAs(initialDetail));
		// }		
		
		// [Test]
		// public async Task FromBlob_UpdatePath_PopulatesAssociation()
		// {
		// 	// Arrange
		// 	var bitmap = new Bitmap(10, 10);
		// 	var image = TestUtils.BlobFromBitmap(bitmap, ImageFormat.Png);
		// 	await cascade.BlobPut("path/to/image1.png", image);
		// 	await cascade.BlobPut("path/to/image2.png", image);
		//
		// 	var thingPhoto = new ThingPhoto { id = 1, imagePath = "path/to/image1.png" };
		// 	await cascade.Create(thingPhoto);
		//
		// 	// Act
		// 	await cascade.Populate(thingPhoto, nameof(ThingPhoto.Image));
		//
		// 	// Assert
		// 	Assert.That(thingPhoto.imagePath, Is.EqualTo("path/to/image1.png"));
		// 	Assert.That(thingPhoto.Image, Is.Not.Null);
		//
		// 	// Act
		// 	await cascade.Update(thingPhoto, new { imagePath = "path/to/image2.png" });
		//
		// 	// Assert
		// 	Assert.That(thingPhoto.imagePath, Is.EqualTo("path/to/image2.png"));
		// 	Assert.That(thingPhoto.Image, Is.Not.Null);
		// 	Assert.That(thingPhoto.Image.Width, Is.EqualTo(10));
		// }
		
	}
}		
