using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Cascade;
using Cascade.testing;
using NUnit.Framework;

namespace Cascade {
	class Parent : CascadeModel {
		public int id { get; set; }

		[HasMany(foreignIdProperty: "parentId")]
		public IEnumerable<Child>? Children { get; set; }

		public string colour { get; set; }
		public override object CascadeId() {
			return id;
		}
	}


	class Child : CascadeModel {
		public int id { get; set; }
		public int? parentId { get; set; }
		
		[BelongsTo(idProperty: "parentId")]
		public Parent? Parent { get; set; }

		public override object CascadeId() {
			return id;
		}
		
		public int age { get; set; }
	}


	[TestFixture]
	public class AssociationTests {



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

		// [SetUp]
		// public void SetUp() {
		// 	origin = new MockOrigin(nowMs:1000,handleRequest: (origin, requestOp) => {
		// 		var nowMs = origin.NowMs;
		// 		var thing = new Thing() {
		// 			id = requestOp.IdAsInt ?? 0
		// 		};
		// 		thing.updatedAtMs = requestOp.TimeMs;
		// 		return Task.FromResult(new OpResponse(
		// 			requestOp: requestOp,
		// 			nowMs,
		// 			connected: true,
		// 			exists: true,
		// 			result: thing,
		// 			arrivedAtMs: nowMs
		// 		));
		// 	});
		// }

		
		

		[Test]
		public async Task PopulateHasManyAndBelongsToWithoutCache() {
			Parent[] allParent = new[] {
				new Parent() { id = 1, colour = "red" },
				new Parent() { id = 2, colour = "green" }
			};
			foreach (var p in allParent)
				await parentOrigin.Store(p.id, p);
			Child[] allChildren = new[] {
				new Child() { id = 5, parentId = 1, age = 2 },
				new Child() { id = 6, parentId = 1, age = 4 },
				new Child() { id = 7, parentId = 2, age = 5 },
				new Child() { id = 8, parentId = 2, age = 7 },
			};
			foreach (var c in allChildren)
				await childOrigin.Store(c.id, c);
			
			var cascade = new CascadeDataLayer(origin, new ICascadeCache[] { }, new CascadeConfig());
			
			var parent = await cascade.Get<Parent>(1);
			Assert.AreEqual(1, parent!.id);
			Assert.IsNull(parent.Children);
			await cascade.Populate(parent, "Children");
			Assert.AreEqual(2,parent.Children!.Count());
			Assert.IsTrue(parent.Children!.Any(c=>c.id==5));
			Assert.IsTrue(parent.Children!.Any(c=>c.id==6));
			Assert.IsFalse(parent.Children!.Any(c=>c.id==7));

			var child = parent.Children!.FirstOrDefault()!;
			await cascade.Populate(child, "Parent");
			Assert.AreSame(parent,child.Parent);
		}
		
		/*
		
		Options for implementing populate (maintaining semi-immutable models) :
		
		1. Populate sets a "ImmutableArray<Child> Children" property (unlocks & relocks if SuperModel)
		2. Populate returns a modified clone of parent with children
		3. Populate is only available on requests (Get & Query)
		
		
		*/
	}
}
