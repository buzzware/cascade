using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Easy.Common.Extensions;

// This file is for future use. The idea is to cache and speed up reflection calls which can be costly in performance 

namespace Buzzware.Cascade {

// Best method for creating objects from types 
//
// 1. https://vagifabilov.wordpress.com/2010/04/02/dont-use-activator-createinstance-or-constructorinfo-invoke-use-compiled-lambda-expressions/
// which links to https://rogeralsing.com/2008/02/28/linq-expressions-creating-objects/
// similar http://www.java2s.com/Code/CSharp/Reflection/Reflector.htm
// extension method https://rboeije.wordpress.com/2012/04/19/extension-method-on-type-as-alternative-for-activator-createinstance/
// http://geekswithblogs.net/mrsteve/archive/2012/02/19/a-fast-c-sharp-extension-method-using-expression-trees-create-instance-from-type-again.aspx


// 2. see http://mironabramson.com/blog/post/2008/08/Fast-version-of-the-ActivatorCreateInstance-method-using-IL.aspx


// https://github.com/KSemenenko/CreateInstance

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

	public class CascadePropertyInfo {

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
		
		private readonly PropertyInfo propertyInfo;
		public string Name => propertyInfo.Name;
		public Type Type => propertyInfo.PropertyType;
		private Type notNullType;
		public Type NotNullType => notNullType;
		public Type? InnerType { get; }
		public Type? InnerNotNullType { get; }
		public bool IsTypeEnumerable { get; }
		public Attribute? KindAttribute { get; }
		public bool CanRead => propertyInfo.CanRead;
		public bool CanWrite => propertyInfo.CanWrite;
		public readonly CascadePropertyKind Kind;
		
		public CascadePropertyInfo(PropertyInfo pi, CascadePropertyKind kind, Attribute? attr) {
			propertyInfo = pi;
			Kind = kind;
			KindAttribute = attr;
			notNullType = CascadeTypeUtils.DeNullType(pi.PropertyType);
			IsTypeEnumerable = notNullType.Implements<IEnumerable>() && notNullType != typeof(string);
			if (IsTypeEnumerable) {
				InnerType = CascadeTypeUtils.InnerType(notNullType);
				InnerNotNullType = CascadeTypeUtils.DeNullType(InnerType!);
			}
		}
		
		public object GetValue(object aInstance) {
			return propertyInfo.GetValue(aInstance, null);
		}

		public void SetValue(object aInstance, object? aValue) {
			propertyInfo.SetValue(aInstance, aValue, null);
		}
	}
	
	public class ClassInfo {
		private ImmutableDictionary<String,CascadePropertyInfo> allPropertyInfos;
		public ImmutableDictionary<string, CascadePropertyInfo> AllPropertyInfos => allPropertyInfos;
		private ImmutableDictionary<String,CascadePropertyInfo> dataAndIdPropertyInfos;
		public ImmutableDictionary<string, CascadePropertyInfo> DataAndIdPropertyInfos => dataAndIdPropertyInfos;
		private ImmutableArray<string> dataAndIdNames;
		public ImmutableArray<string> DataAndIdNames => dataAndIdNames; 
		private ImmutableDictionary<String,CascadePropertyInfo> associationinfos;
		public ImmutableDictionary<string, CascadePropertyInfo> Associationinfos => associationinfos;
		private ImmutableDictionary<string, CascadePropertyInfo> dataAndAssociationInfos;
		public ImmutableDictionary<string, CascadePropertyInfo> DataAndAssociationInfos => dataAndAssociationInfos; 
		
		private CascadePropertyInfo? idProperty;
		public CascadePropertyInfo? IdProperty => idProperty;
		
		private Type type;
		public Type Type => type;

		public ClassInfo(Type aType) {
			this.type = aType;
			CollectClassInfo();
			dataAndIdPropertyInfos = ImmutableDictionary.CreateRange(AllPropertyInfos.Where(kvp => kvp.Value.Kind == CascadePropertyKind.Data || kvp.Value.Kind == CascadePropertyKind.Id));
			dataAndIdNames = dataAndIdPropertyInfos.Select(p=>p.Value.Name).ToImmutableArray();
			associationinfos = ImmutableDictionary.CreateRange(AllPropertyInfos.Where(
				kvp => kvp.Value.Kind == CascadePropertyKind.HasOne || 
				       kvp.Value.Kind == CascadePropertyKind.HasMany ||
				       kvp.Value.Kind == CascadePropertyKind.BelongsTo ||
				       kvp.Value.Kind == CascadePropertyKind.FromBlob ||
				       kvp.Value.Kind == CascadePropertyKind.FromProperty
			));
			dataAndAssociationInfos = dataAndIdPropertyInfos.AddRange(associationinfos);
			idProperty = AllPropertyInfos.Values.FirstOrDefault(pi => pi.Kind == CascadePropertyKind.Id);
		}
		
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

		public CascadePropertyInfo? GetPropertyInfo(string name) {
			return allPropertyInfos.TryGetValue(name, out CascadePropertyInfo info) ? info : null;
		}

		public object? GetValue(object aInstance, string aName) {
			return GetPropertyInfo(aName)?.GetValue(aInstance);
		}

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

	public static class FastReflection {

		static Dictionary<Type, ClassInfo>? classInfos = null;

		public static void Reset() {
			classInfos = null;
		}

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
		
		public static ClassInfo GetClassInfo(object obj) {
			return GetClassInfo(obj.GetType());
		}
		
		public static IReadOnlyDictionary<String,CascadePropertyInfo> GetProperties(Type aType) {
			var ci = GetClassInfo(aType);
			return ci.AllPropertyInfos;
		}
		
		public static object GetDefault(Type aType) {
			if(aType.GetTypeInfo().IsValueType)
   			return Activator.CreateInstance(aType);
   		return null;
		}				

		public static CascadePropertyInfo? GetPropertyInfo(Type? aType, string aName) {
			return aType!=null ? GetClassInfo(aType).GetPropertyInfo(aName) : null;
		}
		
		// from https://github.com/NancyFx/Nancy/blob/master/src/Nancy/ViewEngines/Extensions.cs
		public static bool IsAnonymousType(object source) {
    	return source != null && IsAnonymousType(source.GetType());
    }

		public static bool IsAnonymousType(Type type) {
			if (type == null) {
				return false;
			}
			return type.GetTypeInfo().IsGenericType
						 && (type.GetTypeInfo().Attributes & TypeAttributes.NotPublic) == TypeAttributes.NotPublic
						 && (type.Name.StartsWith("<>", StringComparison.OrdinalIgnoreCase) || type.Name.StartsWith("VB$", StringComparison.OrdinalIgnoreCase))
						 && (type.Name.Contains("AnonymousType") || type.Name.Contains("AnonType"))
						 && type.GetTypeInfo().GetCustomAttributes(typeof(CompilerGeneratedAttribute)).Any();
		}

		public static object? GetValue(object aInstance, string aName) {
			var pi = GetPropertyInfo(aInstance.GetType(), aName);
			if (pi == null)
				throw new ArgumentException("Unknown or inaccesible property "+aName);
			return pi.GetValue(aInstance);
		}
		
		public static object? TryGetValue(object aInstance, string aName) {
			return GetPropertyInfo(aInstance.GetType(), aName)?.GetValue(aInstance);
		}
		
		public static void SetValue(object aInstance, string aName, object aValue) {
			var pi = GetPropertyInfo(aInstance.GetType(), aName);
			if (pi == null)
				throw new ArgumentException("Unknown Type or inaccesible property");
			pi.SetValue(aInstance, aValue);
		}

		public static void ConvertAndSet(object aInstance, string aName, object aValue) {
			var ci = GetClassInfo(aInstance.GetType());
			if (ci == null)
				throw new ArgumentException("Unknown Type");
			var pi = ci.AllPropertyInfos.TryGetValue(aName, out var cpi) ? cpi : (CascadePropertyInfo?)null;
			if (pi == null)
				throw new ArgumentException("Unknown or inaccesible property " + aName);

			Type t = pi.Type;
			if (t != null && aValue == null && t.GetType().GetTypeInfo().IsPrimitive && (t==typeof(Double) || t==typeof(float) || t==typeof(Decimal) || t==typeof(Single)))
				aValue = Double.NaN;
			else
				aValue = FastReflection.ConvertToType(aValue, pi.Type);
			pi.SetValue(aInstance, aValue);
		}			

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

