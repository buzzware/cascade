using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Easy.Common.Extensions;

namespace Buzzware.Cascade {

  // Enum for classifying properties
  public enum CascadePropertyKind {
    None,
    Unknown,
    Internal,
    Id,
    Data,
    HasOne,
    HasMany,
    BelongsTo,
    FromBlob,
    FromProperty
  }

  // This class holds metadata and utility methods for a property of a Cascade model class
  public class CascadePropertyInfo {
    
    /// <summary>
    /// Generates a CascadePropertyInfo object from a PropertyInfo object.
    /// Determines the kind of property it is based on attributes and naming conventions.
    /// The dotnet PropertyInfo instance is encapsulated and private so we know what we need from it in case we want to not store it.
    /// </summary>
    /// <param name="pi">The PropertyInfo to extract CascadePropertyInfo from.</param>
    /// <returns>A CascadePropertyInfo object representing properties of the parameter.</returns>
    public static CascadePropertyInfo FromPropertyInfo(PropertyInfo pi) {
      CascadePropertyKind kind = CascadePropertyKind.Unknown;

      var attrs = pi.GetCustomAttributes(true).ToArray();
      Attribute? attr;

      if ((attr = attrs.FirstOrDefault(a => a is FromPropertyAttribute) as Attribute) != null) {
        kind = CascadePropertyKind.FromProperty;
      } else if (pi.Name.StartsWith("_")) {
        kind = CascadePropertyKind.Internal;
      } else if (CascadeTypeUtils.IsSimple(pi.PropertyType)) {
        if ((attr = attrs.FirstOrDefault(a => a is CascadeIdAttribute) as Attribute) != null) {
          kind = CascadePropertyKind.Id;
        } else {
          kind = CascadePropertyKind.Data;
        }
      } else if (attrs.Any()) {
        if ((attr = attrs.FirstOrDefault(a => a is BelongsToAttribute) as Attribute) != null) {
          kind = CascadePropertyKind.BelongsTo;
        } else if ((attr = attrs.FirstOrDefault(a => a is HasManyAttribute) as Attribute) != null) {
          kind = CascadePropertyKind.HasMany;
        } else if ((attr = attrs.FirstOrDefault(a => a is HasOneAttribute) as Attribute) != null) {
          kind = CascadePropertyKind.HasOne;
        } else if ((attr = attrs.FirstOrDefault(a => a is FromBlobAttribute) as Attribute) != null) {
          kind = CascadePropertyKind.FromBlob;
        }
      }
      return new CascadePropertyInfo(
        pi,
        kind,
        attr
      );
    }
    
    // private instance of the dotnet class
    private readonly PropertyInfo propertyInfo;

    // Name of the property
    public string Name => propertyInfo.Name;

    // Type of the property
    public Type Type => propertyInfo.PropertyType;

    // Denulled Type of the property
    public Type NotNullType => notNullType;
    private Type notNullType;

    // Inner type of enumerable properties, if applicable
    public Type? InnerType { get; }

    // Inner Denulled type of enumerable properties, if applicable
    public Type? InnerNotNullType { get; }

    // True if the property type is enumerable, excluding strings
    public bool IsTypeEnumerable { get; }

    // The key Attribute for this kind of Cascade property, if any
    public Attribute? KindAttribute { get; }

    // If this property can be read
    public bool CanRead => propertyInfo.CanRead;

    // If this property can be written to
    public bool CanWrite => propertyInfo.CanWrite;

    // the classified kind of this property
    public readonly CascadePropertyKind Kind;
    
    /// <summary>
    /// CascadePropertyInfo Constructor
    /// </summary>
    /// <param name="pi">PropertyInfo object from which this instance is constructed.</param>
    /// <param name="kind">The kind of property as determined by attributes or metadata.</param>
    /// <param name="attr">The attribute associated with the kind of this property.</param>
    public CascadePropertyInfo(PropertyInfo pi, CascadePropertyKind kind, Attribute? attr) {
      propertyInfo = pi;
      Kind = kind;
      KindAttribute = attr;
      // Find the not-null version of the property type
      notNullType = CascadeTypeUtils.DeNullType(pi.PropertyType);
      // Determine if this type is an enumeration (excluding strings)
      IsTypeEnumerable = notNullType.Implements<IEnumerable>() && notNullType != typeof(string);
      if (IsTypeEnumerable) {
        // If enumerable, set the inner and non-null inner types
        InnerType = CascadeTypeUtils.InnerType(notNullType);
        InnerNotNullType = CascadeTypeUtils.DeNullType(InnerType!);
      }
    }
    
    /// <summary>
    /// Gets the value of the property from an instance of the class.
    /// </summary>
    /// <param name="aInstance">The object instance from which to get the property value.</param>
    /// <returns>The value of the property from the instance.</returns>
    public object GetValue(object aInstance) {
      return propertyInfo.GetValue(aInstance, null);
    }

    /// <summary>
    /// Sets the value of the property for an instance of a class.
    /// </summary>
    /// <param name="aInstance">The object instance for which to set the property value.</param>
    /// <param name="aValue">The value to set to the property in the instance.</param>
    public void SetValue(object aInstance, object? aValue) {
      propertyInfo.SetValue(aInstance, aValue, null);
    }
  }
  
  // ClassInfo encapsulates details about a class and its properties within the Cascade framework.
  public class ClassInfo {
    private ImmutableDictionary<String,CascadePropertyInfo> allPropertyInfos;
  
    // All properties contained in the class.
    public ImmutableDictionary<string, CascadePropertyInfo> AllPropertyInfos => allPropertyInfos;
  
    private ImmutableDictionary<String,CascadePropertyInfo> dataAndIdPropertyInfos;
  
    // Subset of properties containing 'Data' and 'Id' kinds.
    public ImmutableDictionary<string, CascadePropertyInfo> DataAndIdPropertyInfos => dataAndIdPropertyInfos;
  
    private ImmutableArray<string> dataAndIdNames;
    
    // The names of all properties marked as 'Data' or 'Id' kinds.
    public ImmutableArray<string> DataAndIdNames => dataAndIdNames; 
  
    private ImmutableDictionary<String,CascadePropertyInfo> associationinfos;
  
    // Properties involved as associations (HasOne, HasMany, etc.).
    public ImmutableDictionary<string, CascadePropertyInfo> Associationinfos => associationinfos;
  
    private ImmutableDictionary<string, CascadePropertyInfo> dataAndAssociationInfos;
  
    // Combined data, id and associations properties
    public ImmutableDictionary<string, CascadePropertyInfo> DataAndAssociationInfos => dataAndAssociationInfos; 
    
    private CascadePropertyInfo? idProperty;
  
    // The primary 'Id' property of the class, if available.
    public CascadePropertyInfo? IdProperty => idProperty;
    
    private Type type;
    
    // Type of the class.
    public Type Type => type;

    /// <summary>
    /// ClassInfo Constructor
    /// </summary>
    /// <param name="aType">The class/type to extract metadata and properties for.</param>
    public ClassInfo(Type aType) {
      this.type = aType;
      CollectClassInfo();
      // Separate and categorize properties by kinds of interest
      dataAndIdPropertyInfos = ImmutableDictionary.CreateRange(AllPropertyInfos.Where(kvp => kvp.Value.Kind == CascadePropertyKind.Data || kvp.Value.Kind == CascadePropertyKind.Id));
      dataAndIdNames = dataAndIdPropertyInfos.Select(p=>p.Value.Name).ToImmutableArray();
      // Select associations from all properties
      associationinfos = ImmutableDictionary.CreateRange(AllPropertyInfos.Where(
        kvp => kvp.Value.Kind == CascadePropertyKind.HasOne || 
               kvp.Value.Kind == CascadePropertyKind.HasMany ||
               kvp.Value.Kind == CascadePropertyKind.BelongsTo ||
               kvp.Value.Kind == CascadePropertyKind.FromBlob ||
               kvp.Value.Kind == CascadePropertyKind.FromProperty
      ));
      // Combine data and associations
      dataAndAssociationInfos = dataAndIdPropertyInfos.AddRange(associationinfos);
      // Find and set primary id property
      idProperty = AllPropertyInfos.Values.FirstOrDefault(pi => pi.Kind == CascadePropertyKind.Id);
    }
    
    /// <summary>
    /// Collects all property information for a type, storing as CascadePropertyInfo.
    /// </summary>
    void CollectClassInfo() {
      var pis = new Dictionary<String, CascadePropertyInfo>();
      foreach (PropertyInfo prop in Type.GetRuntimeProperties()) {
        if (!prop.CanRead)
          continue;
        if (prop.Name.Contains("."))
          continue;
        pis[prop.Name] = CascadePropertyInfo.FromPropertyInfo(prop);
      }
      allPropertyInfos = pis.ToImmutableDictionary();
    }

    /// <summary>
    /// Tries to get a specific property's CascadePropertyInfo by its name.
    /// </summary>
    /// <param name="name">The name of the property to retrieve information for.</param>
    /// <returns>A CascadePropertyInfo object if the property exists; otherwise, null.</returns>
    public CascadePropertyInfo? GetPropertyInfo(string name) {
      return allPropertyInfos.TryGetValue(name, out CascadePropertyInfo info) ? info : null;
    }

    /// <summary>
    /// Retrieves the value of a specified property from an instance.
    /// </summary>
    /// <param name="aInstance">The object instance to get the property value from.</param>
    /// <param name="aName">The name of the property to retrieve.</param>
    /// <returns>The value of the specified property or null if the property does not exist.</returns>
    public object? GetValue(object aInstance, string aName) {
      return GetPropertyInfo(aName)?.GetValue(aInstance);
    }

    /// <summary>
    /// Sets a value for a specified property in a given object instance.
    /// </summary>
    /// <param name="aInstance">The object in which to set the property value.</param>
    /// <param name="aName">The name of the property to set.</param>
    /// <param name="aValue">The value to assign to the property.</param>
    public void SetValue(object aInstance, string aName, object? aValue) {
      GetPropertyInfo(aName)?.SetValue(aInstance,aValue);
    }

    //public object propertyGet(object aInstance, string aName) {
    //	var pi = propertyInfos  type.GetRuntimeProperty(aName);
    //	if (pi == null)
    //		return null;
    //	return pi.GetValue(aInstance);
    //}
  }

  // FastReflection provides optimized, cached reflection utilities for interacting with object types & properties.
  public static class FastReflection {

    static Dictionary<Type, ClassInfo>? classInfos = null;

    /// <summary>
    /// Resets the class info cache, clearing stored reflection metadata.
    /// </summary>
    public static void Reset() {
      classInfos = null;
    }

    /// <summary>
    /// Retrieves or constructs ClassInfo for a specified type.
    /// Ensures that only class types are used.
    /// </summary>
    /// <param name="aType">The class/type to get ClassInfo for.</param>
    /// <returns>ClassInfo containing metadata and properties of the class.</returns>
    public static ClassInfo GetClassInfo(Type aType) {
      if (classInfos == null)
        classInfos = new Dictionary<Type, ClassInfo>();
      var ci = classInfos.TryGetValue(aType, out var info) ? info : null;
      if (ci == null) {
        if (!aType.IsClass)
          throw new ArgumentException("The specified type is not a class.");
        ci = new ClassInfo(aType);
        classInfos[aType] = ci;
      }
      return ci;
    }
    
    /// <summary>
    /// Retrieves ClassInfo for the type of the provided object instance.
    /// </summary>
    /// <param name="obj">The object instance to get ClassInfo for based on its type.</param>
    /// <returns>ClassInfo containing metadata and properties of the class.</returns>
    public static ClassInfo GetClassInfo(object obj) {
      return GetClassInfo(obj.GetType());
    }
    
    /// <summary>
    /// Gets all properties (name and CascadePropertyInfo) of a specified type.
    /// </summary>
    /// <param name="aType">The class/type to extract properties from.</param>
    /// <returns>A dictionary of property names and their corresponding CascadePropertyInfo.</returns>
    public static IReadOnlyDictionary<String,CascadePropertyInfo> GetProperties(Type aType) {
      var ci = GetClassInfo(aType);
      return ci.AllPropertyInfos;
    }
    
    /// <summary>
    /// Gets the default value of a specified type. 
    /// If it is a value type, creates a new instance; otherwise returns null.
    /// </summary>
    /// <param name="aType">The type for which to obtain a default value.</param>
    /// <returns>The default value for the given type.</returns>
    public static object GetDefault(Type aType) {
      if(aType.GetTypeInfo().IsValueType)
       return Activator.CreateInstance(aType);
     return null;
    }        

    /// <summary>
    /// Retrieves the CascadePropertyInfo for a specific property of a type.
    /// </summary>
    /// <param name="aType">The class/type to look into for the property.</param>
    /// <param name="aName">The name of the property to retrieve information for.</param>
    /// <returns>CascadePropertyInfo if the property is found; otherwise, null.</returns>
    public static CascadePropertyInfo? GetPropertyInfo(Type? aType, string aName) {
      return aType!=null ? GetClassInfo(aType).GetPropertyInfo(aName) : null;
    }
    
    /// <summary>
    /// Determines if an object is of an anonymous type.
    /// </summary>
    /// <param name="source">The object to check.</param>
    /// <returns>True if the object is of anonymous type; otherwise, false.</returns>
    public static bool IsAnonymousType(object source) {
      return source != null && IsAnonymousType(source.GetType());
    }

    /// <summary>
    /// Determines if a type is an anonymous type.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type is anonymous; otherwise, false.</returns>
    public static bool IsAnonymousType(Type type) {
      if (type == null) {
        return false;
      }
      // Check generic type parameters, naming conventions, and specific attributes
      return type.GetTypeInfo().IsGenericType
             && (type.GetTypeInfo().Attributes & TypeAttributes.NotPublic) == TypeAttributes.NotPublic
             && (type.Name.StartsWith("<>", StringComparison.OrdinalIgnoreCase) || type.Name.StartsWith("VB$", StringComparison.OrdinalIgnoreCase))
             && (type.Name.Contains("AnonymousType") || type.Name.Contains("AnonType"))
             && type.GetTypeInfo().GetCustomAttributes(typeof(CompilerGeneratedAttribute)).Any();
    }

    /// <summary>
    /// Gets the value of a specified property from an object instance.
    /// Throws exception if property is not accessible or doesn't exist.
    /// </summary>
    /// <param name="aInstance">The object instance to get the property value from.</param>
    /// <param name="aName">The name of the property to retrieve.</param>
    /// <returns>The value of the specified property.</returns>
    /// <exception cref="ArgumentException">Thrown when the property is unknown or inaccessible.</exception>
    public static object? GetValue(object aInstance, string aName) {
      var pi = GetPropertyInfo(aInstance.GetType(), aName);
      if (pi == null)
        throw new ArgumentException("Unknown or inaccesible property " + aName);
      return pi.GetValue(aInstance);
    }
    
    /// <summary>
    /// Attempts to get the value of a property from an object instance.
    /// Returns null if the property isn't found or accessible.
    /// </summary>
    /// <param name="aInstance">The object instance to query for the property.</param>
    /// <param name="aName">The name of the property to get value for.</param>
    /// <returns>The value of the property, or null if not found or inaccessible.</returns>
    public static object? TryGetValue(object aInstance, string aName) {
      return GetPropertyInfo(aInstance.GetType(), aName)?.GetValue(aInstance);
    }
    
    /// <summary>
    /// Sets the value of a specified property in an object instance.
    /// Throws an exception if the property is unknown or inaccessible.
    /// </summary>
    /// <param name="aInstance">The object instance to set the property value for.</param>
    /// <param name="aName">The name of the property to be set.</param>
    /// <param name="aValue">The new value to assign to the property.</param>
    /// <exception cref="ArgumentException">Thrown when the property is unknown or inaccessible.</exception>
    public static void SetValue(object aInstance, string aName, object aValue) {
      var pi = GetPropertyInfo(aInstance.GetType(), aName);
      if (pi == null)
        throw new ArgumentException("Unknown Type or inaccesible property");
      pi.SetValue(aInstance, aValue);
    }

    /// <summary>
    /// Converts and sets a specified property to a new value, handling type conversion when necessary.
    /// </summary>
    /// <param name="aInstance">The instance of the object whose property value will be set.</param>
    /// <param name="aName">The name of the property to set.</param>
    /// <param name="aValue">The value to set after possible conversion.</param>
    /// <exception cref="ArgumentException">Thrown when the instance type or property is unknown or inaccessible.</exception>
    public static void ConvertAndSet(object aInstance, string aName, object aValue) {
      var ci = GetClassInfo(aInstance.GetType());
      if (ci == null)
        throw new ArgumentException("Unknown Type");
      var pi = ci.AllPropertyInfos.TryGetValue(aName, out var cpi) ? cpi : (CascadePropertyInfo?)null;
      if (pi == null)
        throw new ArgumentException("Unknown or inaccesible property " + aName);

      Type t = pi.Type;
      if (t != null && aValue == null && t.GetType().GetTypeInfo().IsPrimitive && (t == typeof(Double) || t == typeof(float) || t == typeof(Decimal) || t == typeof(Single)))
        aValue = Double.NaN;
      else
        aValue = FastReflection.ConvertToType(aValue, pi.Type);
      pi.SetValue(aInstance, aValue);
    }        

    /// <summary>
    /// Converts a value to a specified type, providing additional handling for nulls and interface types.
    /// </summary>
    /// <param name="aValue">The value to be converted to the target type.</param>
    /// <param name="aType">The target type for the conversion.</param>
    /// <returns>The converted value matching the target type, or a default type value if conversion fails.</returns>
    public static object ConvertToType(object aValue, Type aType) {
      if (aType.GetType().GetTypeInfo().IsInterface) {  // should check if implements interface, but how?
        if (aValue.GetType().GetTypeInfo().ImplementedInterfaces.Contains(aType))
          return aValue;
        else
          return null;
      } else
        //  Boolean, Byte, SByte, Int16, UInt16, Int32, UInt32, Int64, UInt64, IntPtr, UIntPtr, Char, Double, and Single.      
      try {
        return Convert.ChangeType(aValue, aType);
      } catch (Exception e) {
        if (aValue == null && (aType == typeof(Double) || aType == typeof(float) || aType == typeof(Decimal) || aType == typeof(Single))) {
          return Double.NaN;
        } else {
          return GetDefault(aType);
        }
      }
    }
  }
}
