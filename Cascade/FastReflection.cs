using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

//using Serilog;

namespace Cascade {

// Best method for creating objects from types 
//
// 1. https://vagifabilov.wordpress.com/2010/04/02/dont-use-activator-createinstance-or-constructorinfo-invoke-use-compiled-lambda-expressions/
// which links to https://rogeralsing.com/2008/02/28/linq-expressions-creating-objects/
// similar http://www.java2s.com/Code/CSharp/Reflection/Reflector.htm
// extension method https://rboeije.wordpress.com/2012/04/19/extension-method-on-type-as-alternative-for-activator-createinstance/
// http://geekswithblogs.net/mrsteve/archive/2012/02/19/a-fast-c-sharp-extension-method-using-expression-trees-create-instance-from-type-again.aspx


// 2. see http://mironabramson.com/blog/post/2008/08/Fast-version-of-the-ActivatorCreateInstance-method-using-IL.aspx


// https://github.com/KSemenenko/CreateInstance



	public class ClassInfo {
		public Dictionary<String,PropertyInfo> propertyInfos = null;
		Type type;

		public ClassInfo(Type aType) {
			this.type = aType;
			this.propertyInfos = new Dictionary<String,PropertyInfo>();		// maybe should use ConcurrentDictionary
			collectClassInfo();
		}

		void collectClassInfo() {
			foreach (PropertyInfo prop in type.GetRuntimeProperties()) {
				if (!prop.CanRead)
					continue;
				if (prop.Name.Contains("."))
					continue;
				//if (prop.IsDefined(typeof(JsonIgnoreAttribute)))
				//	continue;
				propertyInfos[prop.Name] = prop;
			}			
		}

		//public object propertyGet(object aInstance, string aName) {
		//	var pi = propertyInfos  type.GetRuntimeProperty(aName);
		//	if (pi == null)
		//		return null;
		//	return pi.GetValue(aInstance);
		//}
	}

	public static class FastReflection {

		static Dictionary<Type, ClassInfo> classInfos = null;

		public static void reset() {
			classInfos = null;
		}

		public static Dictionary<String,PropertyInfo> getProperties(Type aType) {
			var ci = ensureClassInfo(aType);
			return ci.propertyInfos;
		}

		public static Dictionary<String,PropertyInfo> getWriteableProperties(Type aType) {
			var result = new Dictionary<String, PropertyInfo>();
			foreach (var pair in getProperties(aType))
				if (pair.Value.CanWrite)
					result.Add(pair.Key, pair.Value);
			return result;
		}

		public static Dictionary<String,PropertyInfo> getReadableProperties(Type aType) {
			var result = new Dictionary<String, PropertyInfo>();
			foreach (var pair in getProperties(aType))
				if (pair.Value.CanWrite)
					result.Add(pair.Key, pair.Value);
			return result;
		}

		public static object getDefault(Type aType) {
			if(aType.GetTypeInfo().IsValueType)
   			return Activator.CreateInstance(aType);
   		return null;
		}				

		public static PropertyInfo getPropertyInfo(Type aType, string aName) {
			var ci = ensureClassInfo(aType);
			if (ci == null)
				throw new ArgumentException("Unknown Type");
			var pi = ci.propertyInfos.ContainsKey(aName) ? ci.propertyInfos[aName] : null;
			return pi;
		}

		static ClassInfo ensureClassInfo(Type aType) {
			if (classInfos == null)
				classInfos = new Dictionary<Type, ClassInfo>();
			var ci = classInfos.ContainsKey(aType) ? classInfos[aType] : null;
			if (ci == null) {
				ci = new ClassInfo(aType);
				classInfos[aType] = ci;
			}
			return ci;
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

		public static object invokeGetter(object aInstance, string aName) {
			var pi = getPropertyInfo(aInstance.GetType(), aName);
			if (pi == null)
				throw new ArgumentException("Unknown or inaccesible property "+aName);
			return pi.GetValue(aInstance);
		}

		public static void invokeSetter(object aInstance, string aName, object aValue) {
			var ci = ensureClassInfo(aInstance.GetType());
			if (ci == null)
				throw new ArgumentException("Unknown Type");
			var pi = ci.propertyInfos[aName];
			if (pi == null)
				throw new ArgumentException("Unknown or inaccesible property "+aName);
			try {
				pi.SetValue(aInstance, aValue);
			} catch (Exception e) {
			//#if DEBUG
			//	Log.Debug("Failed setting " + aName);
			//#endif
			}
		}

		public static void convertAndSet(object aInstance, string aName, object aValue) {
			var ci = ensureClassInfo(aInstance.GetType());
			if (ci == null)
				throw new ArgumentException("Unknown Type");
			var pi = ci.propertyInfos[aName];
			if (pi == null)
				throw new ArgumentException("Unknown or inaccesible property " + aName);

			Type t = pi.PropertyType;
			if (t != null && aValue == null && t.GetType().GetTypeInfo().IsPrimitive && (t==typeof(Double) || t==typeof(float) || t==typeof(Decimal) || t==typeof(Single)))
				aValue = Double.NaN;
			else
				aValue = FastReflection.convertToType(aValue, pi.PropertyType);
			pi.SetValue(aInstance, aValue);
		}			

		public static object convertToType(object aValue, Type aType) {
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
					return getDefault(aType);
				}
			}
		}

}
}

