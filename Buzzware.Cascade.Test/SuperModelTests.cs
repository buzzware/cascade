
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
      Assert.That(thing!.id, Is.EqualTo(5));
      Assert.That(thing.name, Is.EqualTo("Fred"));

      // Set the model to immutable and verify changes are not allowed.
      thing.__mutable = false;
      Assert.Catch(() => thing.name = "John");
      Assert.That(thing.name, Is.EqualTo("Fred"));

      // Set the model to mutable and verify changes are allowed.
      thing.__mutable = true;
      thing.name = "John";
      Assert.That(thing.name, Is.EqualTo("John"));
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
      Assert.That(proxyEvents["colour"], Is.EqualTo("blue"));  // changing the actual caused a property change on the proxy
      actual.__mutable = false;    // no more changes to actual

      // Change a property in the proxy and check if that change is independent of immutability of actual.
      proxy.colour = "red";
      Assert.That(proxy.colour, Is.EqualTo("red"));        // proxy changes override the actual

      // Verify that accessors on the proxy still refer back to the actual object values.
      Assert.That(proxy.name, Is.EqualTo("Rex"));          // proxy get returns actual property value
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

    /// <summary>
    /// Test __ApplyChanges when setting a property to null on a proxy where:
    /// - the local backing store is null (default)
    /// - the proxied value is non-null
    /// - the property has no entry in _propertySet
    /// This should result in the property being set to null as a tracked change.
    /// </summary>
    [Test]
    public void ApplyChangesNullOnProxyWithNonNullProxiedValue() {
      var actual = new AllTypesModel() {
        AString = "original",
        AInt = 42,
        ABool = true
      };
      var proxy = new AllTypesModel();
      proxy.__SetProxyFor(actual, false, false);

      // Verify proxy returns proxied values
      Assert.That(proxy.AString, Is.EqualTo("original"));
      Assert.That(proxy.AInt, Is.EqualTo(42));
      Assert.That(proxy.ABool, Is.EqualTo(true));
      Assert.That(proxy.__HasChanges, Is.False);

      // Apply changes setting properties to null
      proxy.__ApplyChanges(new Dictionary<string, object?>() {
        [nameof(AllTypesModel.AString)] = null,
        [nameof(AllTypesModel.AInt)] = null,
        [nameof(AllTypesModel.ABool)] = null
      });

      // Properties should now be null (not the proxied values)
      Assert.That(proxy.AString, Is.Null);
      Assert.That(proxy.AInt, Is.Null);
      Assert.That(proxy.ABool, Is.Null);

      // Changes should be tracked
      Assert.That(proxy.__HasChanges, Is.True);
      var changes = proxy.__GetChanges();
      Assert.That(changes.ContainsKey(nameof(AllTypesModel.AString)), Is.True);
      Assert.That(changes.ContainsKey(nameof(AllTypesModel.AInt)), Is.True);
      Assert.That(changes.ContainsKey(nameof(AllTypesModel.ABool)), Is.True);
    }

    /// <summary>
    /// Test __ApplyChanges when setting a property to null on a proxy where:
    /// - the property was previously changed locally to a non-null value
    /// - then we apply null
    /// This should work correctly because the backing store is non-null.
    /// </summary>
    [Test]
    public void ApplyChangesNullOnProxyAfterLocalChange() {
      var actual = new AllTypesModel() {
        AString = "original"
      };
      var proxy = new AllTypesModel();
      proxy.__SetProxyFor(actual, false, false);

      // Make a local change first
      proxy.AString = "modified";
      Assert.That(proxy.AString, Is.EqualTo("modified"));
      Assert.That(proxy.__HasChanges, Is.True);

      // Apply null
      proxy.__ApplyChanges(new Dictionary<string, object?>() {
        [nameof(AllTypesModel.AString)] = null
      });

      // Property should now be null
      Assert.That(proxy.AString, Is.Null);
      Assert.That(proxy.__HasChanges, Is.True);
      Assert.That(proxy.__GetChanges()[nameof(AllTypesModel.AString)], Is.Null);
    }

    /// <summary>
    /// Test __ApplyChanges when setting a property to null on a proxy where:
    /// - the proxied value is also null
    /// - the local backing store is null
    /// - there is no entry in _propertySet
    /// In this case, the property should remain null and no change should be recorded
    /// (since the effective value doesn't change).
    /// </summary>
    [Test]
    public void ApplyChangesNullOnProxyWithNullProxiedValue() {
      var actual = new AllTypesModel() {
        AString = null
      };
      var proxy = new AllTypesModel();
      proxy.__SetProxyFor(actual, false, false);

      // Verify proxy returns null (from proxied object)
      Assert.That(proxy.AString, Is.Null);
      Assert.That(proxy.__HasChanges, Is.False);

      // Apply null - effective value doesn't change
      proxy.__ApplyChanges(new Dictionary<string, object?>() {
        [nameof(AllTypesModel.AString)] = null
      });

      // Effective value is still null, no change needed
      Assert.That(proxy.AString, Is.Null);
      // Note: whether this should be tracked as a change is debatable,
      // but the effective value hasn't changed
    }

    /// <summary>
    /// Test __ApplyChanges with a mix of null and non-null values on a proxy.
    /// </summary>
    [Test]
    public void ApplyChangesMixedNullAndNonNullOnProxy() {
      var actual = new AllTypesModel() {
        AString = "original",
        AInt = 42,
        ABool = true,
        ADouble = 3.14
      };
      var proxy = new AllTypesModel();
      proxy.__SetProxyFor(actual, false, false);

      // Apply mixed changes
      proxy.__ApplyChanges(new Dictionary<string, object?>() {
        [nameof(AllTypesModel.AString)] = null,      // null replacing non-null
        [nameof(AllTypesModel.AInt)] = 100,          // non-null replacing non-null
        [nameof(AllTypesModel.ABool)] = null,        // null replacing non-null
        [nameof(AllTypesModel.ADouble)] = 2.71       // non-null replacing non-null
      });

      Assert.That(proxy.AString, Is.Null);
      Assert.That(proxy.AInt, Is.EqualTo(100));
      Assert.That(proxy.ABool, Is.Null);
      Assert.That(proxy.ADouble, Is.EqualTo(2.71));

      Assert.That(proxy.__HasChanges, Is.True);
      var changes = proxy.__GetChanges();
      Assert.That(changes.Count, Is.EqualTo(4));
    }

    /// <summary>
    /// Test that when the proxied value is null, the proxy has a local non-null change,
    /// and then the proxy property is set back to null, the property should no longer
    /// appear as a change (since it now matches the proxied value).
    /// </summary>
    [Test]
    public void SetPropertyToNullClearsChangeWhenMatchesProxiedNull() {
      var actual = new AllTypesModel() {
        AString = null,
        AInt = null
      };
      var proxy = new AllTypesModel();
      proxy.__SetProxyFor(actual, false, false);

      // Verify initial state - no changes, proxy returns null from proxied
      Assert.That(proxy.AString, Is.Null);
      Assert.That(proxy.AInt, Is.Null);
      Assert.That(proxy.__HasChanges, Is.False);

      // Make local changes to non-null values
      proxy.AString = "modified";
      proxy.AInt = 99;
      Assert.That(proxy.AString, Is.EqualTo("modified"));
      Assert.That(proxy.AInt, Is.EqualTo(99));
      Assert.That(proxy.__HasChanges, Is.True);
      Assert.That(proxy.__GetChanges().Count, Is.EqualTo(2));

      // Set properties back to null (matching the proxied value)
      proxy.AString = null;
      proxy.AInt = null;

      // Properties should be null
      Assert.That(proxy.AString, Is.Null);
      Assert.That(proxy.AInt, Is.Null);

      // Since the values now match the proxied values, they should no longer be tracked as changes
      Assert.That(proxy.__HasChanges, Is.False);
      Assert.That(proxy.__GetChanges().Count, Is.EqualTo(0));
    }

  }
}
