using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Buzzware.Cascade.Testing;
using NUnit.Framework;
using Buzzware.StandardExceptions;

namespace Buzzware.Cascade.Test {

	/// <summary>
	/// Tests the Association aspects of the Cascade library, specifically for the 'has-many' and 'belongs-to'
	/// relationships between Parent and Child entities. It ensures that associations can be populated correctly
	/// without reliance on caching mechanisms, verifying the integrity of directional relationships.
	/// </summary>
	[TestFixture]
	public class AssociationTests {

		// Mock origins for testing the association functionalities
		MockOrigin2 origin;
		MockModelClassOrigin<Parent> parentOrigin;
		MockModelClassOrigin<Child> childOrigin;

		/// <summary>
		/// Sets up the initial configuration and mock data sources required for each test.
		/// This setup includes initializing mock origins for Parent and Child, and preparing
		/// the primary MockOrigin2 instance to simulate a database with predefined mock objects.
		/// </summary>
		[SetUp]
		public void SetUp() {

			// Initialize mock origins for Parent and Child entities
			parentOrigin = new MockModelClassOrigin<Parent>();
			childOrigin = new MockModelClassOrigin<Child>();

			// Setup the composite origin with both Parent and Child mock origins
			origin = new MockOrigin2(
				new Dictionary<Type, IModelClassOrigin>() {
					{ typeof(Parent), parentOrigin },
					{ typeof(Child), childOrigin },
				},
				1000
			);
		}
		
		/// <summary>
		/// Tests that the Cascade library can correctly populate a Parent's Children
		/// and a Child's Parent associations without using cache.
		/// It verifies both directional associations: 'has-many' and 'belongs-to'.
		/// </summary>
		[Test]
		public async Task PopulateHasManyAndBelongsToWithoutCache() {

			// Create and store sample Parent objects in the parentOrigin
			Parent[] allParent = new[] {
				new Parent() { id = 1, colour = "red" },
				new Parent() { id = 2, colour = "green" }
			};
			foreach (var p in allParent)
				await parentOrigin.Store(p.id, p);
			
			// Create and store sample Child objects with parent associations in the childOrigin
			Child[] allChildren = new[] {
				new Child() { id = "5", parentId = 1, level = 2 },
				new Child() { id = "6", parentId = 1, level = 4 },
				new Child() { id = "7", parentId = 2, level = 5 },
				new Child() { id = "8", parentId = 2, level = 7 },
			};
			foreach (var c in allChildren)
				await childOrigin.Store(c.id, c);
			
			// Initialize the CascadeDataLayer without cache to test fresh data fetch
			var cascade = new CascadeDataLayer(origin, new ICascadeCache[] { }, new CascadeConfig(), new MockCascadePlatform(), ErrorControl.Instance, new CascadeJsonSerialization());

			// Fetch the Parent entity and ensure its initial state has been set correctly
			var parent = await cascade.Get<Parent>(1);
			Assert.AreEqual(1, parent!.id);
			Assert.IsNull(parent.Children);
			
			// Populate the Parent's 'Children' property and verify that it contains correct associations
			await cascade.Populate(parent, "Children");
			Assert.AreEqual(2, parent.Children!.Count());
			Assert.IsTrue(parent.Children!.Any(c => c.id == "5"));
			Assert.IsTrue(parent.Children!.Any(c => c.id == "6"));
			Assert.IsFalse(parent.Children!.Any(c => c.id == "7"));

			// Fetch the first Child and verify its 'Parent' association corresponds to the correct Parent
			var child = parent.Children!.FirstOrDefault()!;
			await cascade.Populate(child, "Parent");
			Assert.AreSame(parent, child.Parent);
			
			// Re-fetch the Parent entity using population option and ensure the 'Children' associations are correct
			var parent2 = await cascade.Get<Parent>(1, populate: new string[] { "Children" });
			Assert.AreEqual(2, parent2.Children!.Count());
			Assert.IsTrue(parent2.Children!.Any(c => c.id == "5"));
			Assert.IsTrue(parent2.Children!.Any(c => c.id == "6"));
		}
	}
}

