using System;
using System.Collections.Generic;
using System.Security;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Cascade.Test {
	[TestFixture]
	public class JsonSerializationTests {
		private CascadeJsonSerialization sz;


		[SetUp]
		public void SetUp() {
			sz = new CascadeJsonSerialization(ignoreUnderscoreProperties: true, ignoreAssociations: true);
		}

		[Test]
		public async Task Simple() {
			var child = new Child() { age = 25 };
			var output = sz.SerializeToNode(child);
			Assert.That(output.HasKey(nameof(SuperModel.__mutable)),Is.False);
			Assert.That(output.HasKey(nameof(Child.Parent)),Is.False);
			Assert.That(output.Keys, Is.EquivalentTo(new string[] { "id","parentId","weight","power","age","updatedAtMs" }));
				
			
			var parent = new Parent() { colour = "blue", Children = new[] { child } };
			output = sz.SerializeToNode(parent);
			Assert.That(output.HasKey(nameof(SuperModel.__mutable)),Is.False);
			Assert.That(output.HasKey(nameof(Parent.Children)),Is.False);
			Assert.That(output.Keys(), Is.EquivalentTo(new string[] { "id","colour","Size","updatedAtMs" }));
			
			child.Parent = parent;
			output = sz.SerializeToNode(child);
			Assert.That(output.HasKey(nameof(Child.Parent)),Is.False);
		}
	}
}
