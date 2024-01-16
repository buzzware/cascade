using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Cascade.Testing;
using NUnit.Framework;
using StandardExceptions;

namespace Cascade.Test {
	[TestFixture]
	public class ChildCollectionTests {
		
		MockOrigin2 origin;
		MockModelClassOrigin<Parent> parentOrigin;
		MockModelClassOrigin<Child> childOrigin;

		[SetUp]
		public void SetUp() {
			parentOrigin = new MockModelClassOrigin<Parent>();
			childOrigin = new MockModelClassOrigin<Child>();
			origin = new MockOrigin2(
				new Dictionary<Type, IModelClassOrigin>() {
					{ typeof(Parent), parentOrigin },
					{ typeof(Child), childOrigin },
				},
				1000
			);
		}

		[Test]
		public async Task AddingChildren() {
			Parent[] allParents = new[] {
				new Parent() { id = 1, colour = "red" },
				new Parent() { id = 2, colour = "green" }
			};
			Child[] allChildren = new[] {
				new Child() { id = "C1", parentId = 1, weight = 5},
				new Child() { id = "C2", parentId = 1, weight = 7},
				new Child() { id = "C3", parentId = 2, weight = 2},
				new Child() { id = "C4", parentId = 2, weight = 4},
			};
			foreach (var p in allParents)
				await parentOrigin.Store(p.id, p);
			foreach (var c in allChildren)
				await childOrigin.Store(c.id, c);
			var parentCache = new ModelClassCache<Parent, long>();
			var childCache = new ModelClassCache<Child, string>();
			var modelCache = new ModelCache(aClassCache: new Dictionary<Type, IModelClassCache>() {
				{ typeof(Parent), parentCache },
				{ typeof(Child), childCache }
			});
			var cascade = new CascadeDataLayer(
				origin, 
				new ICascadeCache[] { modelCache }, 
				new CascadeConfig(),
				new MockCascadePlatform(),
				ErrorControl.Instance,
				new CascadeJsonSerialization()
			);
			
			// now setup

			var parent1 = await cascade.Get<Parent>(1, populate: new []{"Children"});
			Assert.That(parent1.Children.Count(), Is.EqualTo(2));

			// a new child for parent1 is created in the origin
			var child5 = new Child() { id = "C5", parentId = 1, weight = 11 };
			await childOrigin.Store(child5.id, child5);
			
			Assert.That(parent1.Children.Count(), Is.EqualTo(2));						// doesn't get automatically updated
			await cascade.Populate(parent1,new []{"Children"}, freshnessSeconds:0);		// populate should update it
			Assert.That(parent1.Children.Count(), Is.EqualTo(3));
			Assert.That(parent1.Children, Has.Member(child5));	// populate did update the collection correctly
			
			// getting the parent again should have 3 children
			parent1 = await cascade.Get<Parent>(1, populate: new []{"Children"});
			Assert.That(parent1.Children.Count(), Is.EqualTo(3));
			Assert.That(parent1.Children, Has.Member(child5));
			
			
			var preExistingChildCollection = (await cascade.GetWhereCollection<Child>(nameof(Child.parentId), parent1.id.ToString())).ToImmutableArray();
			Assert.That(preExistingChildCollection, Has.Length.EqualTo(3));
			// manually add child 6 to local collections
			var child6 = new Child() { id = "C6", parentId = 1, weight = 13 };
			await childOrigin.Store(child6.id, child6);		// in place of cascade.Create()
			preExistingChildCollection = preExistingChildCollection.Insert(0,child6);	// insert as first
			var collectionIds = preExistingChildCollection.Select(c => c.id).ToImmutableArray();
			await cascade.SetCacheWhereCollection(typeof(Child), nameof(Child.parentId), parent1.id.ToString(), preExistingChildCollection);
			
			var cachedCollection = (await cascade.GetWhereCollection<Child>(nameof(Child.parentId), parent1.id.ToString(),freshnessSeconds:1000)).ToImmutableArray();
			var cachedIds = cachedCollection.Select(c => c.id).ToImmutableArray();

			Assert.That(cachedIds,Is.EquivalentTo(collectionIds));

			var freshCollection = (await cascade.GetWhereCollection<Child>(nameof(Child.parentId), parent1.id.ToString(),freshnessSeconds:0)).ToImmutableArray();
			var freshIds = freshCollection.Select(c => c.id).ToImmutableArray();
			Assert.That(collectionIds.Sort(),Is.EquivalentTo(freshIds.Sort()));


			// var redThings = await cascade.Query<Parent>("red_things", new JsonObject {
			// 	["colour"] = "red"
			// });
			// var redIds = redThings.Select(t => t.id).ToImmutableArray();
			// Assert.That(redIds, Is.EqualTo(new long[] {1, 3}));
			//
			// // check that collection & models are in cache
			// Parent? thing;
			//
			// thing = await thingModelStore1.Fetch<Parent>(1);
			// Assert.AreEqual(1,thing.id);
			// Assert.AreEqual("red",thing.colour);
			// thing = await thingModelStore1.Fetch<Parent>(3);
			// Assert.AreEqual(3,thing.id);
			// Assert.AreEqual("red",thing.colour);
			// thing = await thingModelStore1.Fetch<Parent>(2);
			// Assert.AreEqual(null, thing);
			//
			// var rcBefore = origin.RequestCount;
			// origin.IncNowMs();
			//
			// // request with freshness=2
			// var redThings2 = await cascade.Query<Parent>("red_things", new JsonObject {
			// 	["colour"] = "red"
			// }, freshnessSeconds: 2);
			// Assert.That(redThings2.Select(t => t.id).ToImmutableArray(), Is.EqualTo(new long[] {1, 3}));	// same response
			// Assert.AreEqual(rcBefore,origin.RequestCount);	// didn't use origin
			//
			// rcBefore = origin.RequestCount;
			// origin.IncNowMs();
			//
			// redThings2 = (await cascade.Query<Parent>("red_things", new JsonObject {
			// 	["colour"] = "red"
			// }, freshnessSeconds: 0)).ToImmutableArray();
			// Assert.That(redThings2.Select(t => t.id).ToImmutableArray(), Is.EqualTo(new long[] {1, 3}));	// same response
			// Assert.AreEqual(rcBefore+1,origin.RequestCount);	// did use origin
		}

	}
}
