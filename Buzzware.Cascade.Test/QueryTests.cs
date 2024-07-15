using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Buzzware.Cascade.Testing;
using NUnit.Framework;
using Buzzware.StandardExceptions;

namespace Buzzware.Cascade.Test {
	[TestFixture]
	public class QueryTests {
		
		MockOrigin2 origin;
		MockModelClassOrigin<Parent> thingOrigin;
		MockModelClassOrigin<Child> gadgetOrigin;

		[SetUp]
		public void SetUp() {
			thingOrigin = new MockModelClassOrigin<Parent>();
			gadgetOrigin = new MockModelClassOrigin<Child>();
			origin = new MockOrigin2(
				new Dictionary<Type, IModelClassOrigin>() {
					{ typeof(Parent), thingOrigin },
					{ typeof(Child), gadgetOrigin },
				},
				1000
			);
		}

		[Test]
		public async Task Simple() {
			Parent[] allThings = new[] {
				new Parent() { id = 1, colour = "red" },
				new Parent() { id = 2, colour = "green" },
				new Parent() { id = 3, colour = "red" },
				new Parent() { id = 4, colour = "yellow" },
			};
			foreach (var t in allThings)
				await thingOrigin.Store(t.id, t);
			var thingModelStore1 = new ModelClassCache<Parent, long>();
			var cache1 = new ModelCache(aClassCache: new Dictionary<Type, IModelClassCache>() {
				{ typeof(Parent), thingModelStore1 }
			});
			var cascade = new CascadeDataLayer(origin, new ICascadeCache[] { cache1 }, new CascadeConfig(), new MockCascadePlatform(), ErrorControl.Instance, new CascadeJsonSerialization());
			var redThings = await cascade.Query<Parent>("red_things", new JsonObject {
				["colour"] = "red"
			});
			var redIds = redThings.Select(t => t.id).ToImmutableArray();
			Assert.That(redIds, Is.EqualTo(new long[] {1, 3}));
			
			// check that collection & models are in cache
			Parent? thing;
			
			thing = await thingModelStore1.Fetch<Parent>(1);
			Assert.AreEqual(1,thing.id);
			Assert.AreEqual("red",thing.colour);
			thing = await thingModelStore1.Fetch<Parent>(3);
			Assert.AreEqual(3,thing.id);
			Assert.AreEqual("red",thing.colour);
			thing = await thingModelStore1.Fetch<Parent>(2);
			Assert.AreEqual(null, thing);

			var rcBefore = origin.RequestCount;
			origin.IncNowMs();
			
			// request with freshness=2
			var redThings2 = await cascade.Query<Parent>("red_things", new JsonObject {
				["colour"] = "red"
			}, freshnessSeconds: 2);
			Assert.That(redThings2.Select(t => t.id).ToImmutableArray(), Is.EqualTo(new long[] {1, 3}));	// same response
			Assert.AreEqual(rcBefore,origin.RequestCount);	// didn't use origin
			
			rcBefore = origin.RequestCount;
			origin.IncNowMs();
			
			redThings2 = (await cascade.Query<Parent>("red_things", new JsonObject {
				["colour"] = "red"
			}, freshnessSeconds: 0)).ToImmutableArray();
			Assert.That(redThings2.Select(t => t.id).ToImmutableArray(), Is.EqualTo(new long[] {1, 3}));	// same response
			Assert.AreEqual(rcBefore+1,origin.RequestCount);	// did use origin

			var red_things_collection = await cascade.GetCollection<Parent>("red_things");
			Assert.That(red_things_collection, Is.EqualTo(new long[] { 1, 3 }));
		}
	}
}
