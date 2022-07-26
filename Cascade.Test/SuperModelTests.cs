using System.Collections.Generic;
using System.Text.Json;
using NUnit.Framework;

namespace Cascade.Test {

	[TestFixture]
	class SuperModelTests {
		
		public class Thing : SuperModel {

			// for setting proxyFor
			public Thing(Thing? proxyFor=null) : base(proxyFor) {
			}
			
			// for JSON deserialize
			public Thing() : base(null) {
			}
			
			public int id {
				get => GetProperty(ref _id); 
				set => SetProperty(ref _id, value);
			}
			private int _id;

			public string? name {
				get => GetProperty(ref _name); 
				set => SetProperty(ref _name, value);
			}
			private string? _name;

			public string? colour {
				get => GetProperty(ref _colour);
				set => SetProperty(ref _colour, value);
			}
			private string? _colour;

		}
		
		
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
