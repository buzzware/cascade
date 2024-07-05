using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Buzzware.Cascade.Testing;
using NUnit.Framework;

namespace Buzzware.Cascade.Test {

	[TestFixture]
	partial class SuperModelTests {
		[Test]
		public void Immutability()
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
		public void ProxyingEvents()
		{
			var actual = new Thing() { id = 3, name = "Rex" };
			var proxy = new Thing(actual);
			var proxyEvents = new Dictionary<string,object>();
			proxy.PropertyChanged += (sender, args) =>
				proxyEvents[args.PropertyName] = (sender as Thing)!.GetType().GetProperty(args.PropertyName)!.GetValue(sender); 
				
			actual.colour = "blue";
			Assert.AreEqual("blue", proxyEvents["colour"]);	// changing the actual caused a property change on the proxy
			actual.__mutable = false;		// no more changes to actual
			
			proxy.colour = "red";
			Assert.AreEqual("red", proxy.colour);				// proxy changes override the actual
			
			Assert.AreEqual("Rex", proxy.name);					// proxy get returns actual property value
		}

		[Test]
		public void ChangingProxiedForSameDoesntRaiseEvents() {
			var actual = new Thing() { id = 3, name = "Rex" };
			var proxy = new Thing(actual);
			var proxyEvents = new Dictionary<string,object>();
			proxy.PropertyChanged += (sender, args) =>
				proxyEvents[args.PropertyName] = (sender as Thing)!.GetType().GetProperty(args.PropertyName)!.GetValue(sender);
			Assert.That(proxy.__GetChanges().Keys.Count,Is.Zero);
			Assert.That(proxyEvents.Keys.Count, Is.Zero);
			
			var sameActual = (Thing)actual.Clone();
			proxy.__SetProxyFor(sameActual,true,true);
			Assert.That(proxy.__ProxyFor,Is.SameAs(sameActual));
			Assert.That(proxy.__GetChanges().Keys.Count,Is.Zero);
			Assert.That(proxyEvents.Keys.Count, Is.Zero);
			
			Assert.That(proxy.id,Is.EqualTo(actual.id));
			Assert.That(proxy.name,Is.EqualTo(actual.name));

			sameActual.colour = "blue";
			Assert.That(proxyEvents["colour"], Is.EqualTo("blue"));	// changing the sameActual caused a property change on the proxy
		}

		[Test]
		public void MaintainChangesForNewProxiedSame() {
			var actual = new Thing() { id = 3, name = "Rex" };
			var proxy = new Thing(actual);
			var proxyEvents = new Dictionary<string, object?>();
			proxy.PropertyChanged += (sender, args) =>
				proxyEvents[args.PropertyName] = (sender as Thing)!.GetType().GetProperty(args.PropertyName)!.GetValue(sender);

			proxy.name = "Fred";
			Assert.That(proxyEvents["name"], Is.EqualTo("Fred"));	// changing the actual caused a property change on the proxy
			Assert.That(proxy.name, Is.EqualTo("Fred"));
			Assert.That(proxy.__GetChanges().Count, Is.EqualTo(1));
			Assert.That(proxy.__GetChanges()["name"], Is.EqualTo("Fred"));
			
			var sameActual = (Thing)actual.Clone();
			proxyEvents.Clear();
			proxy.__SetProxyFor(sameActual,true,true);
			Assert.That(proxyEvents.Count,Is.Zero);
			Assert.That(proxy.name, Is.EqualTo("Fred"));
			Assert.That(proxy.__GetChanges().Count, Is.EqualTo(1));
			Assert.That(proxy.__GetChanges()["name"], Is.EqualTo("Fred"));
		}
		
		[Test]
		public void MaintainChangesForNewProxiedDiffRaise() {
			var actual = new Thing() { id = 3, name = "Rex" };
			var proxy = new Thing(actual);
			var proxyEvents = new Dictionary<string, object?>();
			proxy.PropertyChanged += (sender, args) =>
				proxyEvents[args.PropertyName] = (sender as Thing)!.GetType().GetProperty(args.PropertyName)!.GetValue(sender);

			proxy.name = "Fred";
			Assert.That(proxyEvents["name"], Is.EqualTo("Fred"));	// changing the actual caused a property change on the proxy
			Assert.That(proxy.name, Is.EqualTo("Fred"));
			Assert.That(proxy.__GetChanges().Count, Is.EqualTo(1));
			Assert.That(proxy.__GetChanges()["name"], Is.EqualTo("Fred"));
			
			var diffActual = (Thing)actual.Clone();
			diffActual.name = "John";
			diffActual.colour = "Yellow";
			proxyEvents.Clear();
			proxy.__SetProxyFor(diffActual,true,true);
			// Fred change is maintained
			Assert.That(proxy.name, Is.EqualTo("Fred"));
			Assert.That(proxy.__GetChanges().Count, Is.EqualTo(1));
			Assert.That(proxy.__GetChanges()["name"], Is.EqualTo("Fred"));
			// Yellow incoming change raises event
			Assert.That(proxyEvents.Count,Is.EqualTo(1));
			Assert.That(proxyEvents["colour"], Is.EqualTo("Yellow"));
		}
		
		[Test]
		public void ChangesNotMaintainedOrIncomingRaisedWhenFalse() {
			var actual = new Thing() { id = 3, name = "Rex" };
			var proxy = new Thing(actual);
			var proxyEvents = new Dictionary<string, object?>();
			proxy.PropertyChanged += (sender, args) =>
				proxyEvents[args.PropertyName] = (sender as Thing)!.GetType().GetProperty(args.PropertyName)!.GetValue(sender);

			proxy.name = "Fred";
			Assert.That(proxyEvents["name"], Is.EqualTo("Fred"));	// changing the actual caused a property change on the proxy
			Assert.That(proxy.name, Is.EqualTo("Fred"));
			Assert.That(proxy.__GetChanges().Count, Is.EqualTo(1));
			Assert.That(proxy.__GetChanges()["name"], Is.EqualTo("Fred"));
			
			var diffActual = (Thing)actual.Clone();
			diffActual.name = "John";
			diffActual.colour = "Yellow";
			proxyEvents.Clear();
			proxy.__SetProxyFor(diffActual,false,false);
			// Fred change is not maintained
			Assert.That(proxy.name, Is.EqualTo("John"));
			Assert.That(proxy.__GetChanges().Count, Is.Zero);
			// Yellow incoming change doesn't raise events
			Assert.That(proxyEvents.Count,Is.Zero);
		}

		[Test]
		public void IncomingChangeThatMatchesEditClearsChange() {
			var actual = new Thing() { id = 3, name = "Rex" };
			var proxy = new Thing(actual);
			var proxyEvents = new Dictionary<string, object?>();
			proxy.PropertyChanged += (sender, args) =>
				proxyEvents[args.PropertyName] = (sender as Thing)!.GetType().GetProperty(args.PropertyName)!.GetValue(sender);

			proxy.name = "Fred";
			
			var diffActual = (Thing)actual.Clone();
			diffActual.name = "Fred";
			
			proxyEvents.Clear();
			proxy.__SetProxyFor(diffActual,true,true);
			
			Assert.That(proxy.name, Is.EqualTo("Fred"));
			Assert.That(proxy.__GetChanges().Count, Is.EqualTo(0));	// should not be considered a change because it matches the proxy change
			Assert.That(proxyEvents.Count,Is.EqualTo(1));
			Assert.That(proxyEvents.First().Key, Is.EqualTo(nameof(Thing.__HasChanges)));
		}
	}
}
