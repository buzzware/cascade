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
	public class ChildCollectionTests {
		
		MockOrigin2 origin;
		MockModelClassOrigin<Parent> parentOrigin;
		MockModelClassOrigin<Child> childOrigin;

		/// <summary>
		/// Sets up the test environment by initializing mock origin objects for Parent and Child entities.
		/// </summary>
		[SetUp]
		public void SetUp() {
			
			// Initialization of mock origins
			parentOrigin = new MockModelClassOrigin<Parent>();
			childOrigin = new MockModelClassOrigin<Child>();

			// Setting up origin with a dictionary mapping type to the mock origins
			origin = new MockOrigin2(
				new Dictionary<Type, IModelClassOrigin>() {
					{ typeof(Parent), parentOrigin },
					{ typeof(Child), childOrigin },
				},
				1000
			);
		}

		/// <summary>
		/// Tests the ability of the Cascade library to correctly manage child collection updates, 
		/// ensuring newly added children are reflected in the parent's collection when requested with updated data.
		/// </summary>
		[Test]
		public async Task AddingChildren() {
			
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

			// Storing parents and children in their mock origins
			foreach (var p in allParents)
				await parentOrigin.Store(p.id, p);
			foreach (var c in allChildren)
				await childOrigin.Store(c.id, c);

			// Setup caches for model classes
			var parentCache = new ModelClassCache<Parent, long>();
			var childCache = new ModelClassCache<Child, string>();
			var modelCache = new ModelCache(aClassCache: new Dictionary<Type, IModelClassCache>() {
				{ typeof(Parent), parentCache },
				{ typeof(Child), childCache }
			});
			
			// Setup the cascade data layer
			var cascade = new CascadeDataLayer(
				origin, 
				new ICascadeCache[] { modelCache }, 
				new CascadeConfig(),
				new MockCascadePlatform(),
				ErrorControl.Instance,
				new CascadeJsonSerialization()
			);
			
			// Fetch the parent and populate its children
			var parent1 = await cascade.Get<Parent>(1, populate: new []{"Children"});
			Assert.That(parent1.Children.Count(), Is.EqualTo(2));

			// Add a new child in the origin for parent1
			var child5 = new Child() { id = "C5", parentId = 1, tally = 11 };
			await childOrigin.Store(child5.id, child5);
			
			Assert.That(parent1.Children.Count(), Is.EqualTo(2)); // Verify children aren't updated automatically
			await cascade.Populate(parent1,new []{"Children"}, freshnessSeconds:0);
			Assert.That(parent1.Children.Count(), Is.EqualTo(3));
			Assert.That(parent1.Children, Has.Member(child5)); // Verify new child is in the updated collection

			// Fetch parent1 again to ensure it populates with 3 children
			parent1 = await cascade.Get<Parent>(1, populate: new []{"Children"});
			Assert.That(parent1.Children.Count(), Is.EqualTo(3));
			Assert.That(parent1.Children, Has.Member(child5));
			
			// Retrieve pre-existing child collection for the specific parent
			var preExistingChildCollection = (await cascade.GetWhereCollection<Child>(nameof(Child.parentId), parent1.id.ToString())).ToImmutableArray();
			Assert.That(preExistingChildCollection, Has.Length.EqualTo(3));

			// Manually add a new child and update local collections
			var child6 = new Child() { id = "C6", parentId = 1, tally = 13 };
			await childOrigin.Store(child6.id, child6);
			preExistingChildCollection = preExistingChildCollection.Insert(0,child6);

			// Update collection cache with the new child
			var collectionIds = preExistingChildCollection.Select(c => c.id).ToImmutableArray();
			await cascade.SetCacheWhereCollection(typeof(Child), nameof(Child.parentId), parent1.id.ToString(), preExistingChildCollection);
			
			// Retrieve cached collection and validate it against local collection IDs
			var cachedCollection = (await cascade.GetWhereCollection<Child>(nameof(Child.parentId), parent1.id.ToString(),freshnessSeconds:1000)).ToImmutableArray();
			var cachedIds = cachedCollection.Select(c => c.id).ToImmutableArray();
			Assert.That(cachedIds,Is.EquivalentTo(collectionIds));

			// Retrieve fresh collection and validate it against local collection IDs
			var freshCollection = (await cascade.GetWhereCollection<Child>(nameof(Child.parentId), parent1.id.ToString(),freshnessSeconds:0)).ToImmutableArray();
			var freshIds = freshCollection.Select(c => c.id).ToImmutableArray();
			Assert.That(collectionIds.Sort(),Is.EquivalentTo(freshIds.Sort()));
		}
	}
}
