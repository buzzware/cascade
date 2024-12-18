
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Buzzware.Cascade.Testing;
using NUnit.Framework;

namespace Buzzware.Cascade.Test {

  /// <summary>
  /// Test suite for utility functions in the Cascade library
  /// </summary>
  [TestFixture]
  public class UtilityTests {
    
    /// <summary>
    /// Tests ConvertTo for converting various data types to other types accurately.
    /// </summary>
    [Test]
    public async Task TypeConversion() {

      // Test conversion of string literals to integral types.
      Assert.AreEqual((byte)1, CascadeTypeUtils.ConvertTo(typeof(byte),"1",null));
      Assert.AreEqual((int)1, CascadeTypeUtils.ConvertTo(typeof(int),"1",null));
      Assert.AreEqual((long)1, CascadeTypeUtils.ConvertTo(typeof(long),"1",null));
      
      // Test conversion from integral types to string.
      Assert.AreEqual("1", CascadeTypeUtils.ConvertTo(typeof(string),(byte)1,null));
      Assert.AreEqual("1", CascadeTypeUtils.ConvertTo(typeof(string),(int)1,null));
      Assert.AreEqual("1", CascadeTypeUtils.ConvertTo(typeof(string),(long)1,null));
      
      // Test conversion between integral types, ensuring the result maintains expected type and value.
      Assert.That(CascadeTypeUtils.ConvertTo(typeof(byte),(int)1), Is.EqualTo(1).And.InstanceOf<Byte>());
      Assert.That(CascadeTypeUtils.ConvertTo(typeof(byte),(long)1), Is.EqualTo(1).And.InstanceOf<Byte>());
      
      Assert.That(CascadeTypeUtils.ConvertTo(typeof(long),(byte)1), Is.EqualTo(1).And.InstanceOf<long>());
      Assert.That(CascadeTypeUtils.ConvertTo(typeof(long),(int)1), Is.EqualTo(1).And.InstanceOf<long>());
      Assert.That(CascadeTypeUtils.ConvertTo(typeof(int),(byte)1), Is.EqualTo(1).And.InstanceOf<int>());
      Assert.That(CascadeTypeUtils.ConvertTo(typeof(int),(long)1), Is.EqualTo(1).And.InstanceOf<int>());
    }

    /// <summary>
    /// Test ValueCompatibleWithType which verifies if given values are compatible with specified integer types.
    /// </summary>
    [Test]
    public void IntegerCompatible() {

      // Test compatibility of integer literals with various integer types.
      Assert.That(CascadeTypeUtils.ValueCompatibleWithType(1, typeof(Int64)), Is.True);
      Assert.That(CascadeTypeUtils.ValueCompatibleWithType(1U, typeof(Int64)), Is.True);
      Assert.That(CascadeTypeUtils.ValueCompatibleWithType(1L, typeof(Int64)), Is.True);
      Assert.That(CascadeTypeUtils.ValueCompatibleWithType(1, typeof(int)), Is.True);
      
      // Test compatibility of integer literals with types that are not compatible.
      Assert.That(CascadeTypeUtils.ValueCompatibleWithType(1, typeof(byte)), Is.False);
      Assert.That(CascadeTypeUtils.ValueCompatibleWithType(1, typeof(string)), Is.False);
      Assert.That(CascadeTypeUtils.ValueCompatibleWithType(1L, typeof(string)), Is.False);
      
      // Test strings like integers
      Assert.That(CascadeTypeUtils.ValueCompatibleWithType("1", typeof(string)), Is.True);
      Assert.That(CascadeTypeUtils.ValueCompatibleWithType("1", typeof(int)), Is.False);
    }

    /// <summary>
    /// Tests if a type determined from a property in the Parent class, namely Children, is recognized as an enumerable type.
    /// This test evaluates whether the Cascade utility can correctly determine if a type supports enumeration.
    /// </summary>
    [Test]
    public void IsEnumerableType() {
      
      // Create list of Child objects as test data.
      var children = new List<Child>() {
        new Child() {id = "1", level = 1},
        new Child() {id = "2", level = 2},
        new Child() {id = "3", level = 3}
      };
      
      // Use reflection to get the Children property type
      var parent = new Parent();
      var nullableChildrenType = typeof(Parent).GetProperty(nameof(Parent.Children))!.PropertyType;
      
      // Get the non-nullable type of the Children property
      var nonNullableTargetType = CascadeTypeUtils.DeNullType(nullableChildrenType);
      
      var isEnumerable = CascadeTypeUtils.IsEnumerableModelType(nonNullableTargetType);
      Assert.That(isEnumerable, Is.True);
    }

    /// <summary>
    /// Test GetDefaultValue with a variety of types
    /// </summary>
    [Test]
    public void DefaultTypeTest() {
      Assert.That(CascadeTypeUtils.GetDefaultValue(typeof(string)),Is.EqualTo(null));
      Assert.That(CascadeTypeUtils.GetDefaultValue(typeof(bool)),Is.EqualTo(false));
      Assert.That(CascadeTypeUtils.GetDefaultValue(typeof(bool?)),Is.EqualTo(null));
      Assert.That(CascadeTypeUtils.GetDefaultValue(typeof(int)),Is.EqualTo(0));
      Assert.That(CascadeTypeUtils.GetDefaultValue(typeof(long)),Is.EqualTo(0));
      Assert.That(CascadeTypeUtils.GetDefaultValue(typeof(double)),Is.EqualTo(0.0));
      Assert.That(CascadeTypeUtils.GetDefaultValue(typeof(double?)),Is.EqualTo(null));
      Assert.That(CascadeTypeUtils.GetDefaultValue(typeof(DateTime?)),Is.EqualTo(null));
      Assert.That(CascadeTypeUtils.GetDefaultValue(typeof(DateTime)),Is.EqualTo(DateTime.MinValue));
    }

    [Test]
    public void InnerTypeTest() {
      Assert.That(CascadeTypeUtils.InnerType(typeof(Thing[])),Is.EqualTo(typeof(Thing)));
      Assert.That(CascadeTypeUtils.InnerType(typeof(Thing)),Is.EqualTo(null));
      Assert.That(CascadeTypeUtils.InnerType(typeof(byte[])),Is.EqualTo(typeof(byte)));
      Assert.That(CascadeTypeUtils.InnerType(typeof(byte)),Is.EqualTo(null));
      Assert.That(CascadeTypeUtils.InnerType(typeof(int[])),Is.EqualTo(typeof(int)));
      Assert.That(CascadeTypeUtils.InnerType(typeof(int)),Is.EqualTo(null));
    }
    
    [Test]
    public void IdKeyDictionaryTest() {
      // Create a dictionary with a custom comparer that treats int 1 and long 1 as equivalent
      var dictionary = new IdKeyDictionary<List<SuperModel>>();

      var list1 = new List<SuperModel>(new SuperModel[]{new Child() {id = "1"}});
      var list2 = new List<SuperModel>(new SuperModel[]{new Child() {id = "2"}});
      var list3 = new List<SuperModel>(new SuperModel[]{new Child() {id = "3"}});
      var list4 = new List<SuperModel>(new SuperModel[]{new Child() {id = "4"}});
      
      // Add and read values with various key types including null, string, int, and long
      dictionary.Add(null, list1);
      Assert.That(dictionary[null], Is.EqualTo(list1));
      dictionary.Add("stringKey", list2);
      Assert.That(dictionary["stringKey"], Is.EqualTo(list2));
      dictionary.Add(1, list3); // write with int
      Assert.That(dictionary[1], Is.EqualTo(list3));  // read with int
      Assert.That(dictionary[1L], Is.EqualTo(list3));  // read with long
      dictionary[1] = list4; // write with long
      Assert.That(dictionary[1L], Is.EqualTo(list4));  // read with long
      Assert.That(dictionary[1], Is.EqualTo(list4));  // read with int

      dictionary.Add("1", list2);   // write with stringified int
      Assert.That(dictionary["1"], Is.EqualTo(list2));  // read with same ok 
      Assert.That(dictionary[1], Is.EqualTo(list4));    // doesn't affect actual int
    }
  }
}
