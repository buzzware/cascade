using System.Collections.Generic;
using System.Text.Json;
using NUnit.Framework;

namespace Cascade.Test {

	[TestFixture]
	partial class SuperModelTests {
		[Test]
		public void Test1()
		{
			string json = @"{
				'id': 5,
				'name': 'Fred',
				'colour': 'green'
			}".Replace("'","\"");

			Thing? thing = JsonSerializer.Deserialize<Thing>(json);
			Assert.AreEqual(thing!.id, 5);
			Assert.AreEqual(thing.name, "Fred");
			thing.__mutable = false;

			Assert.Catch(() => thing.name = "John");
			Assert.AreEqual(thing.name, "Fred");
			
			thing.__mutable = true;
			thing.name = "John";
			Assert.AreEqual(thing.name, "John");
		}

		[Test]
		public void Test2()
		{
			var actual = new Thing() { id = 3, name = "Rex" };
			var proxy = new Thing(actual);
			var changes = new Dictionary<string,object>();
			proxy.PropertyChanged += (sender, args) =>
				changes[args.PropertyName] = (sender as Thing)!.GetType().GetProperty(args.PropertyName)!.GetValue(sender); 
				
			actual.colour = "blue";
			Assert.AreEqual("blue", changes["colour"]);	// changing the actual caused a property change on the proxy
			actual.__mutable = false;		// no more changes to actual
			
			proxy.colour = "red";
			Assert.AreEqual("red", proxy.colour);				// proxy changes override the actual
			
			Assert.AreEqual("Rex", proxy.name);					// proxy get returns actual property value
		}
	}
}
