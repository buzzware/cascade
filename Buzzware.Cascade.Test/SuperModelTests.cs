
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Buzzware.Cascade.Testing;
using NUnit.Framework;

namespace Buzzware.Cascade.Test {

  /// <summary>
  /// Tests for the SuperModel class, focusing on properties like immutability, proxying, and change tracking.
  /// </summary>
  [TestFixture]
  class SuperModelTests {

    /// <summary>
    /// Verifies the immutability feature of the Thing model. Ensures properties cannot 
    /// be changed when the model is set to be immutable and can be changed when mutable.
    /// </summary>
    [Test]
    public void Immutability()
    {
      string json = @"{
        'id': 5,
        'name': 'Fred',
        'colour': 'green'
      }".Replace("'","\"");

      // Deserializing JSON into a Thing object.
      Thing? thing = JsonSerializer.Deserialize<Thing>(json);
      Assert.AreEqual(thing!.id, 5);
      Assert.AreEqual(thing.name, "Fred");
      
      // Set the model to immutable and verify changes are not allowed.
      thing.__mutable = false;
      Assert.Catch(() => thing.name = "John");
      Assert.AreEqual(thing.name, "Fred");
      
      // Set the model to mutable and verify changes are allowed.
      thing.__mutable = true;
      thing.name = "John";
      Assert.AreEqual(thing.name, "John");
    }

    /// <summary>
    /// Test the proxying of events. Verifies that changes to the actual object 
    /// are reflected and propagated through the proxy.
    /// </summary>
    [Test]
    public void ProxyingEvents()
    {
      var actual = new Thing() { id = 3, name = "Rex" };
      var proxy = new Thing(actual);
      var proxyEvents = new Dictionary<string,object?>();
      proxy.PropertyChanged += (sender, args) =>
        proxyEvents[args.PropertyName] = (sender as Thing)!.GetType().GetProperty(args.PropertyName)!.GetValue(sender); 
        
      // Change a property in the actual object and check if it's propagated through the proxy.
      actual.colour = "blue";
      Assert.AreEqual("blue", proxyEvents["colour"]);  // changing the actual caused a property change on the proxy
      actual.__mutable = false;    // no more changes to actual
      
      // Change a property in the proxy and check if that change is independent of immutability of actual.
      proxy.colour = "red";
      Assert.AreEqual("red", proxy.colour);        // proxy changes override the actual
      
      // Verify that accessors on the proxy still refer back to the actual object values.
      Assert.AreEqual("Rex", proxy.name);          // proxy get returns actual property value
    }

    /// <summary>
    /// Ensures that replacing the proxied object with an identical object does not 
    /// raise events or reflect in change tracking.
    /// </summary>
    [Test]
    public void ChangingProxiedForSameDoesntRaiseEvents() {
      var actual = new Thing() { id = 3, name = "Rex" };
      var proxy = new Thing(actual);
      var proxyEvents = new Dictionary<string,object?>();
      proxy.PropertyChanged += (sender, args) =>
        proxyEvents[args.PropertyName] = (sender as Thing)!.GetType().GetProperty(args.PropertyName)!.GetValue(sender);

      // Assert to ensure no initial changes or events are listed for the proxy.
      Assert.That(proxy.__GetChanges().Keys.Count,Is.Zero);
      Assert.That(proxyEvents.Keys.Count, Is.Zero);
      
      // Clone the actual object to use as the new proxied object.
      var sameActual = (Thing)actual.Clone();
      proxy.__SetProxyFor(sameActual,true,true);

      // Verify the proxy points to the new cloned object and no changes or events are reflected.
      Assert.That(proxy.__ProxyFor,Is.SameAs(sameActual));
      Assert.That(proxy.__GetChanges().Keys.Count,Is.Zero);
      Assert.That(proxyEvents.Keys.Count, Is.Zero);
      
      // Confirm that the proxy still mirrors properties of the actual object.
      Assert.That(proxy.id,Is.EqualTo(actual.id));
      Assert.That(proxy.name,Is.EqualTo(actual.name));

      // Change a property on the cloned actual object and verify event propagation.
      sameActual.colour = "blue";
      Assert.That(proxyEvents["colour"], Is.EqualTo("blue"));  // changing the sameActual caused a property change on the proxy
    }

    /// <summary>
    /// Maintains tracked changes on the proxy object even when the proxied actual object changes, 
    /// provided they represent the same data state.
    /// </summary>
    [Test]
    public void MaintainChangesForNewProxiedSame() {
      var actual = new Thing() { id = 3, name = "Rex" };
      var proxy = new Thing(actual);
      var proxyEvents = new Dictionary<string, object?>();
      proxy.PropertyChanged += (sender, args) =>
        proxyEvents[args.PropertyName] = (sender as Thing)!.GetType().GetProperty(args.PropertyName)!.GetValue(sender);

      // Make a change to the proxy and log the event.
      proxy.name = "Fred";
      Assert.That(proxyEvents["name"], Is.EqualTo("Fred"));  // changing the actual caused a property change on the proxy
      Assert.That(proxy.name, Is.EqualTo("Fred"));
      Assert.That(proxy.__GetChanges().Count, Is.EqualTo(1));
      Assert.That(proxy.__GetChanges()["name"], Is.EqualTo("Fred"));
      
      // Create a clone of the actual with the same state.
      var sameActual = (Thing)actual.Clone();
      proxyEvents.Clear();
      proxy.__SetProxyFor(sameActual,true,true);

      // Ensure no new events or changes are recorded since the proxied object state matches.
      Assert.That(proxyEvents.Count,Is.Zero);
      Assert.That(proxy.name, Is.EqualTo("Fred"));
      Assert.That(proxy.__GetChanges().Count, Is.EqualTo(1));
      Assert.That(proxy.__GetChanges()["name"], Is.EqualTo("Fred"));
    }
    
    /// <summary>
    /// Verifies that changes in the proxy are maintained when a new,
    /// differently configured proxied object is used. Ensures new states raise events.
    /// </summary>
    [Test]
    public void MaintainChangesForNewProxiedDiffRaise() {
      var actual = new Thing() { id = 3, name = "Rex" };
      var proxy = new Thing(actual);
      var proxyEvents = new Dictionary<string, object?>();
      proxy.PropertyChanged += (sender, args) =>
        proxyEvents[args.PropertyName] = (sender as Thing)!.GetType().GetProperty(args.PropertyName)!.GetValue(sender);

      // Make a change to the proxy and log the event.
      proxy.name = "Fred";
      Assert.That(proxyEvents["name"], Is.EqualTo("Fred"));  // changing the actual caused a property change on the proxy
      Assert.That(proxy.name, Is.EqualTo("Fred"));
      Assert.That(proxy.__GetChanges().Count, Is.EqualTo(1));
      Assert.That(proxy.__GetChanges()["name"], Is.EqualTo("Fred"));
      
      // Create a differently configured clone of the actual.
      var diffActual = (Thing)actual.Clone();
      diffActual.name = "John";
      diffActual.colour = "Yellow";
      proxyEvents.Clear();

      // Set the proxy to the newly configured clone.
      proxy.__SetProxyFor(diffActual,true,true);

      // Verify that the change to 'name' is maintained on the proxy.
      Assert.That(proxy.name, Is.EqualTo("Fred"));
      Assert.That(proxy.__GetChanges().Count, Is.EqualTo(1));
      Assert.That(proxy.__GetChanges()["name"], Is.EqualTo("Fred"));

      // Verify that the incoming 'colour' change raised an event on the proxy.
      Assert.That(proxyEvents.Count,Is.EqualTo(1));
      Assert.That(proxyEvents["colour"], Is.EqualTo("Yellow"));
    }
    
    /// <summary>
    /// Tests that proxy does not maintain existing changes or raise events for incoming changes
    /// when the setup method specifies false for both maintenance and event raising.
    /// </summary>
    [Test]
    public void ChangesNotMaintainedOrIncomingRaisedWhenFalse() {
      var actual = new Thing() { id = 3, name = "Rex" };
      var proxy = new Thing(actual);
      var proxyEvents = new Dictionary<string, object?>();
      proxy.PropertyChanged += (sender, args) =>
        proxyEvents[args.PropertyName] = (sender as Thing)!.GetType().GetProperty(args.PropertyName)!.GetValue(sender);

      // Make a change to the proxy and log the event.
      proxy.name = "Fred";
      Assert.That(proxyEvents["name"], Is.EqualTo("Fred"));  // changing the actual caused a property change on the proxy
      Assert.That(proxy.name, Is.EqualTo("Fred"));
      Assert.That(proxy.__GetChanges().Count, Is.EqualTo(1));
      Assert.That(proxy.__GetChanges()["name"], Is.EqualTo("Fred"));

      // Create a different actual instance with changes to test lack of event raising.
      var diffActual = (Thing)actual.Clone();
      diffActual.name = "John";
      diffActual.colour = "Yellow";
      proxyEvents.Clear();

      // Set proxy without maintaining changes and without raising events.
      proxy.__SetProxyFor(diffActual,false,false);

      // Confirm that proxy has loaded new state without maintaining old changes.
      Assert.That(proxy.name, Is.EqualTo("John"));
      Assert.That(proxy.__GetChanges().Count, Is.Zero);

      // Confirm no events were raised for the changes incoming from the new proxied object.
      Assert.That(proxyEvents.Count,Is.Zero);
    }

    /// <summary>
    /// Ensures if the proxied object changes to match a previously tracked 
    /// edit on the proxy, the change tracking is cleared for that property.
    /// </summary>
    [Test]
    public void IncomingChangeThatMatchesEditClearsChange() {
      var actual = new Thing() { id = 3, name = "Rex" };
      var proxy = new Thing(actual);
      var proxyEvents = new Dictionary<string, object?>();
      proxy.PropertyChanged += (sender, args) =>
        proxyEvents[args.PropertyName] = (sender as Thing)!.GetType().GetProperty(args.PropertyName)!.GetValue(sender);

      // Modify proxy's name property.
      proxy.name = "Fred";
      
      // Clone and modify actual object to have same 'name'.
      var diffActual = (Thing)actual.Clone();
      diffActual.name = "Fred";

      // Reset any prior events and set proxy to the altered actual.
      proxyEvents.Clear();
      proxy.__SetProxyFor(diffActual,true,true);

      // Confirm that since the incoming name matches the proxy's change, it is no longer tracked as a change.
      Assert.That(proxy.name, Is.EqualTo("Fred"));
      Assert.That(proxy.__GetChanges().Count, Is.EqualTo(0));  // should not be considered a change because it matches the proxy change
      
      // Confirm event was raised, indicating potential synchronization or state update.
      Assert.That(proxyEvents.Count,Is.EqualTo(1));
      Assert.That(proxyEvents.First().Key, Is.EqualTo(nameof(Thing.__HasChanges)));
    }


    // Model used for testing 
    class AllTypesModel : SuperModel {
      
      private string? _aString;
      public string? AString
      {
          get => GetProperty(ref _aString);
          set => SetProperty(ref _aString, value);
      }

      private int? _aInt;
      public int? AInt
      {
          get => GetProperty(ref _aInt);
          set => SetProperty(ref _aInt, value);
      }

      private bool? _aBool;
      public bool? ABool
      {
          get => GetProperty(ref _aBool);
          set => SetProperty(ref _aBool, value);
      }

      private double? _aDouble;
      public double? ADouble
      {
          get => GetProperty(ref _aDouble);
          set => SetProperty(ref _aDouble, value);
      }

      private DateTime? _aDateTime;
      public DateTime? ADateTime
      {
          get => GetProperty(ref _aDateTime);
          set => SetProperty(ref _aDateTime, value);
      }
    }

    /// <summary>
    /// Test __ApplyChanges with various type conversions
    /// </summary>
    [Test]
    public void ApplyChangesGeneralConversion() {

      var DateTime1 = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc); 
      var DateTime2 = new DateTime(2020, 12, 25, 0, 0, 0, DateTimeKind.Utc); 
      
      var model = new AllTypesModel() {
        AString = "xyz",
        AInt = 3,
        ABool = true,
        ADouble = 3.14,
        ADateTime = DateTime1
      };
      model.__ApplyChanges(new Dictionary<string, object?>() {
        [nameof(AllTypesModel.AString)] = 123,
        [nameof(AllTypesModel.AInt)] = "123",
        [nameof(AllTypesModel.ABool)] = "false",
        [nameof(AllTypesModel.ADouble)] = 5,
        [nameof(AllTypesModel.ADateTime)] = DateTime2
      });
      Assert.That(model.AString, Is.EqualTo("123"));
      Assert.That(model.AInt, Is.EqualTo(123));
      Assert.That(model.ABool, Is.False);
      Assert.That(model.ADouble, Is.EqualTo(5.0));
      Assert.That(model.ADateTime, Is.EqualTo(DateTime2));
    }
    
    /// <summary>
    /// Test DateTime conversions
    /// </summary>
    [Test]
    public void ApplyChangesDateTimeConversion() {
      var DateTime1 = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc); 
      var DateTime2 = new DateTime(2020, 12, 25, 0, 0, 0, DateTimeKind.Utc); 
      var model = new AllTypesModel();
      
      model.__ApplyChanges(new Dictionary<string, object?>() {
        [nameof(AllTypesModel.ADateTime)] = DateTime1.ToString("yyyy-MM-dd HH:mm:ss")
      });
      Assert.That(model.ADateTime, Is.EqualTo(DateTime1));
      
      model.__ApplyChanges(new Dictionary<string, object?>() {
        [nameof(AllTypesModel.ADateTime)] = DateTime2.ToString("yyyy-MM-ddTHH:mm:ss")
      });
      Assert.That(model.ADateTime, Is.EqualTo(DateTime2));
      
      model.__ApplyChanges(new Dictionary<string, object?>() {
        [nameof(AllTypesModel.ADateTime)] = DateTime1.ToString("yyyy-MM-ddTHH:mm:ss")+"Z"
      });
      Assert.That(model.ADateTime.Value.ToUniversalTime(), Is.EqualTo(DateTime1));
    }
    
  }
}
