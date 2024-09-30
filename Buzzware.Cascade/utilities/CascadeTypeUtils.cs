using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Easy.Common.Extensions;

namespace Buzzware.Cascade {

  /// <summary>
  /// Utility class providing methods to interact and retrieve information about types 
  /// in the Cascade library, particularly for handling type conversions, checks, 
  /// and operations related to model properties.
  /// </summary>
  public static class CascadeTypeUtils {

    /// <summary>
    /// Retrieves a hierarchy of types for the given type, considering generic type arguments if present.
    /// </summary>
    /// <param name="type">The type for which to retrieve the hierarchy.</param>
    /// <returns>An array of types representing the hierarchy of the given type.</returns>
    public static Type[] GetTypeLayers(Type type) {
      List<Type>? result = new List<Type>();
      Type? curr = type;
      do {
        result.Add(curr);
        curr = curr.GenericTypeArguments.FirstOrDefault();
      } while (curr != null);
      return result.ToArray();
    }

    /// <summary>
    /// Checks if a given object is considered an identifier, which can be a string or a primitive type.
    /// </summary>
    /// <param name="id">The object to check.</param>
    /// <returns>True if the object is an identifier; otherwise, false.</returns>
    public static bool IsId(object? id) {
      return (id is String) || (id?.GetType().IsPrimitive ?? false);
    }
    
    /// <summary>
    /// Determines if a given object is a model. An object is considered a model if it is a class 
    /// and not an identifier type.
    /// </summary>
    /// <param name="id">The object to check.</param>
    /// <returns>True if the object is a model; otherwise, false.</returns>
    public static bool IsModel(object? id) {
      if (IsId(id))
        return false;
      return id?.GetType()?.IsClass ?? false;
    }
    
    /// <summary>
    /// Retrieves the underlying type if the provided type is nullable; otherwise, returns the original type.
    /// </summary>
    /// <param name="type">The type to evaluate.</param>
    /// <returns>The non-nullable underlying type if nullable, or the original type.</returns>
    public static Type DeNullType(Type type) {
      return Nullable.GetUnderlyingType(type) ?? type;
    }

    /// <summary>
    /// Retrieves the inner type of a given generic type.
    /// </summary>
    /// <param name="type">The type to evaluate.</param>
    /// <returns>The inner type of the generic type; otherwise, null.</returns>
    public static Type? InnerType(Type type) {
      return type.GenericTypeArguments.FirstOrDefault();
    }

    /// <summary>
    /// Determines if a given type is a nullable value type.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type is nullable; otherwise, false.</returns>
    public static bool IsNullableType(Type type) {
      return type.IsGenericType && type.GetGenericTypeDefinition().Equals(typeof(Nullable<>));
    }

    /// <summary>
    /// Checks if the specified type is an enumerable type, excluding strings.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type is enumerable and not a string; otherwise, false.</returns>
    public static bool IsEnumerableType(Type type) {
      return (type?.Implements<IEnumerable>() ?? false) && type != typeof(string);
    }

    /// <summary>
    /// Static definition for the byte array type, used for blob representations.
    /// </summary>
    public static readonly Type BlobType = typeof(byte[]);
    
    /// <summary>
    /// Checks if a given type is considered a blob type (byte array).
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type is a blob type; otherwise, false.</returns>
    public static bool IsBlobType(Type type) {
      return type == BlobType;
    }
    
    /// <summary>
    /// Determines if an object is of blob type (byte array).
    /// </summary>
    /// <param name="obj">The object to check.</param>
    /// <returns>True if the object is a blob type; otherwise, false.</returns>
    public static bool IsBlob(object? obj) {
      return obj!=null && IsBlobType(obj.GetType());
    }
    
    /// <summary>
    /// For lists or arrays, retrieves the single type of the item. Handles nullable, generic, and enumerable types.
    /// </summary>
    /// <param name="type">The type to evaluate.</param>
    /// <returns>The singular type for the given enumerable type.</returns>
    public static Type GetSingularType(Type type) {
      var nonNullableTargetType = DeNullType(type);
      var isEnumerable = IsEnumerableType(nonNullableTargetType);
      return isEnumerable ? InnerType(nonNullableTargetType)! : type;
    }

    /// <summary>
    /// Constructs an immutable array of a specified item type from a given collection.
    /// </summary>
    /// <param name="itemType">The type of the items in the immutable array.</param>
    /// <param name="items">The collection of items to be converted.</param>
    /// <returns>An immutable array containing the specified items.</returns>
    public static object ImmutableArrayOfType(Type itemType, IEnumerable items) {
      var itemsType = items.GetType();
      var singularType = GetSingularType(itemsType);
      var singularTypeObject = singularType == typeof(object);
      var isImmutableArray = itemsType == typeof(ImmutableArray<>) || itemsType == typeof(ImmutableArray) || itemsType == typeof(ImmutableArray<object>);
      var isSameSingularType = singularType == itemType;
      if (isImmutableArray && isSameSingularType)
        return items;

      // Create and return an immutable array from the items.
      return EnumerableAdapter.CreateImmutableArray(itemType, items);
    }
    
    /// <summary>
    /// Determines if a given property on a model is considered an association.
    /// </summary>
    /// <param name="modelType">The type of the model containing the property.</param>
    /// <param name="model">The actual model instance.</param>
    /// <param name="propertyName">The name of the property to be checked.</param>
    /// <returns>True if the property is an association, otherwise false.</returns>
    public static bool IsAssociation(Type modelType, object model, string propertyName) {
      var propertyInfo = modelType.GetProperty(propertyName)!;
      if (propertyInfo.PropertyType.IsPrimitive)
        return false;
      return CascadeDataLayer.AssociationAttributes.Any(t => propertyInfo.GetCustomAttributes(t,false).Any());
    }
    
    /// <summary>
    /// Attempts to convert a given value to a specified type using type converters or direct conversion.
    /// Returns a default value if the conversion fails.
    /// </summary>
    /// <param name="type">The target type to convert the value to.</param>
    /// <param name="value">The value to convert.</param>
    /// <param name="defaultValue">Optional default value to return in case of conversion failure.</param>
    /// <returns>The converted value or the default value if conversion fails.</returns>
    public static object? ConvertTo(Type type, object? value, object? defaultValue = null) {
      if (value == null || value == DBNull.Value)
        return defaultValue;
      
      try {
        
        Type targetType = Nullable.GetUnderlyingType(type) ?? type;

        if (targetType.IsInstanceOfType(value))
          return value;
        
        TypeConverter converter = TypeDescriptor.GetConverter(targetType);
        if (converter.CanConvertFrom(value.GetType()))
          return converter.ConvertFrom(value);
        
        return Convert.ChangeType(value, targetType);
        
      } catch (Exception) {
        return defaultValue;
      }
    }
    
    /// <summary>
    /// Retrieves the type of the Cascade ID property from a given model type.
    /// </summary>
    /// <param name="cascadeModelType">The type of the Cascade model.</param>
    /// <returns>The type of the Cascade ID property.</returns>
    public static Type GetCascadeIdType(Type cascadeModelType) {
      return CascadeIdPropertyRequired(cascadeModelType).Type;
    }

    /// <summary>
    /// Retrieves the type of the Cascade ID property from a given model instance.
    /// </summary>
    /// <param name="cascadeModel">The instance of the Cascade model.</param>
    /// <returns>The type of the Cascade ID property.</returns>
    public static Type GetCascadeIdType(object cascadeModel) {
      return CascadeIdPropertyRequired(cascadeModel).Type;
    }

    /// <summary>
    /// Retrieves the value of the Cascade ID from a given model instance.
    /// </summary>
    /// <param name="cascadeModel">The instance of the Cascade model.</param>
    /// <returns>The value of the Cascade ID.</returns>
    public static object GetCascadeId(object cascadeModel) {
      return CascadeIdPropertyRequired(cascadeModel).GetValue(cascadeModel);
    }

    /// <summary>
    /// Sets the Cascade ID value for a specific model instance.
    /// </summary>
    /// <param name="cascadeModel">The model instance to update.</param>
    /// <param name="id">The new value of the Cascade ID.</param>
    public static void SetCascadeId(object cascadeModel, object id) {
      CascadeIdPropertyRequired(cascadeModel).SetValue(cascadeModel,id);
    }
    
    /// <summary>
    /// Attempts to retrieve the Cascade ID value from a model instance without throwing exceptions.
    /// </summary>
    /// <param name="cascadeModel">The model instance to evaluate.</param>
    /// <returns>The value of the Cascade ID if available; otherwise, null.</returns>
    public static object? TryGetCascadeId(object? cascadeModel) {
      var type = cascadeModel?.GetType();
      if (type == null || !type.IsClass || type == typeof(object)) 
        return null;  
      return TryGetCascadeIdProperty(type)?.GetValue(cascadeModel!);
    }

    /// <summary>
    /// Attempts to retrieve the Cascade ID property from a given model type.
    /// </summary>
    /// <param name="type">The type of the Cascade model.</param>
    /// <returns>The Cascade ID property information if available; otherwise, null.</returns>
    public static CascadePropertyInfo? TryGetCascadeIdProperty(Type type) {
      if (!type.IsClass || type == typeof(object)) 
        return null;  
      return FastReflection.GetClassInfo(type).IdProperty;
    }

    /// <summary>
    /// Retrieves the Cascade ID property from a model type.
    /// Throws an exception if the Cascade ID property is not found.
    /// </summary>
    /// <param name="cascadeModelType">The type of the Cascade model.</param>
    /// <returns>The property information for the Cascade ID.</returns>
    /// <exception cref="MissingMemberException">Thrown when the Cascade ID property is not found.</exception>
    public static CascadePropertyInfo CascadeIdPropertyRequired(Type cascadeModelType) {
      return TryGetCascadeIdProperty(cascadeModelType)
        ?? throw new MissingMemberException("The model is missing [CascadeId] on an id property");
    }

    /// <summary>
    /// Retrieves the Cascade ID property from a given model instance.
    /// Throws an exception if the Cascade ID property is not found.
    /// </summary>
    /// <param name="cascadeModel">The Cascade model instance.</param>
    /// <returns>The property information for the Cascade ID.</returns>
    /// <exception cref="MissingMemberException">Thrown when the Cascade ID property is not found.</exception>
    public static CascadePropertyInfo CascadeIdPropertyRequired(object cascadeModel) {
      return CascadeIdPropertyRequired(cascadeModel.GetType());
    }
    
    /// <summary>
    /// Determines if a value is of a floating-point numeric type.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns>True if the value is a floating-point type; otherwise, false.</returns>
    public static bool IsFloatingPoint(object? value) {
      if (value is float) return true;
      if (value is double) return true;
      if (value is decimal) return true;
      return false;
    }

    /// <summary>
    /// Checks if a given value is a numeric type, including both integers and floating-point numbers.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns>True if the value is numeric; otherwise, false.</returns>
    public static bool IsNumber(object? value) {
      if (value is sbyte) return true;
      if (value is byte) return true;
      if (value is short) return true;
      if (value is ushort) return true;
      if (value is int) return true;
      if (value is uint) return true;
      if (value is long) return true;
      if (value is ulong) return true;
      if (value is float) return true;
      if (value is double) return true;
      if (value is decimal) return true;
      return false;
    }

    /// <summary>
    /// List of simple data types that are easily comparable and convertible.
    /// </summary>
    public static readonly Type[] SimpleTypes = new Type[] {
      typeof(Byte), typeof(SByte), typeof(Int16), typeof(UInt16), typeof(Int32), typeof(UInt32), 
      typeof(Int64), typeof(UInt64), typeof(Char), typeof(Double), 
      typeof(Single), typeof(Boolean), typeof(String)
    };
    
    /// <summary>
    /// Determines if a given type is a simple type (one of a basic, primitive type).
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type is simple; otherwise, false.</returns>
    public static bool IsSimple(Type? type) {
      if (type == null)
        return true;
      return SimpleTypes.Contains(type);
    }

    /// <summary>
    /// Compares two objects for equality, accounting for possible numeric conversions and handling nulls.
    /// </summary>
    /// <param name="a">The first object to compare.</param>
    /// <param name="b">The second object to compare.</param>
    /// <returns>True if the objects are considered equal; otherwise, false.</returns>
    public static bool IsEqual(object? a, object? b) {
      if (a == null && b == null) //both are null
        return true;
      if (a == null || b == null) //one is null, the other isn't
        return false;
      
      if (IsNumber(a) && IsNumber(b)) {
        if (IsFloatingPoint(a) || IsFloatingPoint(b)) {
          double da, db;
          if (Double.TryParse(a.ToString(), out da) && Double.TryParse(b.ToString(), out db))
            return Math.Abs(da - db) < 0.000001;
        }
        else {
          if (a.ToString().StartsWith("-") || b.ToString().StartsWith("-"))
            return Convert.ToInt64(a) == Convert.ToInt64(b);
          else
            return Convert.ToUInt64(a) == Convert.ToUInt64(b);
        }
      }
      return a.Equals(b);
    }

    /// <summary>
    /// Compares two enumerables for equality, checking individual elements for equality.
    /// </summary>
    /// <param name="a">The first enumerable to compare.</param>
    /// <param name="b">The second enumerable to compare.</param>
    /// <returns>True if both enumerables contain equivalent elements; otherwise, false.</returns>
    public static bool IsEqualEnumerable(object? a, object? b) {
      if (a == null && b == null) //both are null
        return true;
      if (a == null || b == null) //one is null, the other isn't
        return false;

      var array_a = (a as IEnumerable).Cast<object>().ToArray();
      var array_b = (b as IEnumerable).Cast<object>().ToArray();
      if (array_a.Length != array_b.Length)
        return false;
      var i = 0;
      return array_a.All(v => {
        if (!IsEqual(v, array_b[i]))
          return false;
        i += 1;
        return true;
      });
    }
    
    /// <summary>
    /// Checks if a given type is an integer type.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type is integer; otherwise, false.</returns>
    public static bool IsIntegerType(Type type) {
      return
        type == typeof(int) ||
        type == typeof(long) ||
        type == typeof(byte) ||
        type == typeof(uint) ||
        type == typeof(ulong) ||
        type == typeof(sbyte) ||
        type == typeof(short) ||
        type == typeof(ushort);
    }

    /// <summary>
    /// Determines if the binary representation of a given integer value can fit within the size of the destination type.
    /// </summary>
    /// <param name="value">The integer value to check.</param>
    /// <param name="destinationType">The destination type to check against.</param>
    /// <returns>True if the integer will fit in the destination type; otherwise, false.</returns>
    public static bool IntegerWillFit(object value, Type destinationType) {
      var valueType = value.GetType();
      return Marshal.SizeOf(valueType) <= Marshal.SizeOf(destinationType);
    }

    /// <summary>
    /// Checks if a given value is an integer type and if it will fit within a specified destination type.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <param name="destinationType">The destination type to check against.</param>
    /// <returns>True if the value is an integer type and will fit; otherwise, false.</returns>
    public static bool IsIntegerAndWillFit(object value, Type destinationType) {
      var valueType = value.GetType();
      return IsIntegerType(valueType) && IntegerWillFit(value,destinationType);
    }

    /// <summary>
    /// Determines if a given value is compatible with a specified type, considering conversion rules and inheritance.
    /// </summary>
    /// <param name="value">The value to check compatibility for.</param>
    /// <param name="type">The type to check against.</param>
    /// <returns>True if the value is compatible with the type; otherwise, false.</returns>
    public static bool ValueCompatibleWithType(object value, Type type) {
      var valueType = value.GetType();
      if (valueType == type)
        return true;
      if (value.GetType().IsSubclassOf(type))
        return true;
      if (IsIntegerType(valueType)) {
        if (!IsIntegerType(type))
          return false;
        return IntegerWillFit(value, type);
      }
      return false;
    }
    
    /// <summary>
    /// Gets the default value for given type eg. the value a variable of this type will contain if not intiialized
    /// </summary>
    /// <param name="type">The Type</param>
    /// <returns>A value, often null</returns>
    public static object? GetDefaultValue(Type type) {
      if (type.IsValueType)
        return Activator.CreateInstance(type);
      else
        return null;
    }
  }
}
