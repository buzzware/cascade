using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cascade.Testing;
using NUnit.Framework;
using StandardExceptions;

namespace Cascade.Test {

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
		

		[Test]
		public async Task PopulateHasManyAndBelongsToWithoutCache() {
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
			
			var cascade = new CascadeDataLayer(origin, new ICascadeCache[] { }, new CascadeConfig(), new MockCascadePlatform(), ErrorControl.Instance, new CascadeJsonSerialization());
			
			var parent = await cascade.Get<Parent>(1);
			Assert.AreEqual(1, parent!.id);
			Assert.IsNull(parent.Children);
			await cascade.Populate(parent, "Children");
			Assert.AreEqual(2,parent.Children!.Count());
			Assert.IsTrue(parent.Children!.Any(c=>c.id=="5"));
			Assert.IsTrue(parent.Children!.Any(c=>c.id=="6"));
			Assert.IsFalse(parent.Children!.Any(c=>c.id=="7"));

			var child = parent.Children!.FirstOrDefault()!;
			await cascade.Populate(child, "Parent");
			Assert.AreSame(parent,child.Parent);
			
			var parent2 = await cascade.Get<Parent>(1, populate: new string[] {"Children"});
			Assert.AreEqual(2,parent2.Children!.Count());
			Assert.IsTrue(parent2.Children!.Any(c=>c.id=="5"));
			Assert.IsTrue(parent2.Children!.Any(c=>c.id=="6"));
		}
	}

}
