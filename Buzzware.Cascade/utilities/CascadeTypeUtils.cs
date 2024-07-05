using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Easy.Common.Extensions;
//using TypeConverter;
//using TypeConverter.Converters;

namespace Buzzware.Cascade {
	
	public static class CascadeTypeUtils {
		public static Type[] GetTypeLayers(Type type) {
			List<Type>? result = new List<Type>();
			// return type.GetNestedTypes();
			Type? curr = type;
			do {
				result.Add(curr);
				curr = curr.GenericTypeArguments.FirstOrDefault();
			} while (curr != null);
			return result.ToArray();
		}

		public static bool IsId(object? id) {
			return (id is String) || (id?.GetType().IsPrimitive ?? false);
		}
		
		public static bool IsModel(object? id) {
			if (IsId(id))
				return false;
			return id?.GetType()?.IsClass ?? false;
		}
		
		public static Type DeNullType(Type type) {
			return Nullable.GetUnderlyingType(type) ?? type;
		}

		public static Type? InnerType(Type type) {
			return type.GenericTypeArguments.FirstOrDefault();
		}

		public static bool IsNullableType(Type type) {
			return type.IsGenericType && type.GetGenericTypeDefinition().Equals(typeof(Nullable<>));
		}

		public static bool IsEnumerableType(Type type) {
			return (type?.Implements<IEnumerable>() ?? false) && type != typeof(string);
		}

		public static readonly Type BlobType = typeof(byte[]);
		
		public static bool IsBlobType(Type type) {
			return type == BlobType;
		}
		
		public static bool IsBlob(object? obj) {
			return obj!=null && IsBlobType(obj.GetType());
		}
		
		// public static void SetPropertyValue(object target, string propertyName, object value)
		// {
		// 	// find out the type
		// 	Type type = target.GetType();
		//
		// 	// get the property information based on the type
		// 	PropertyInfo property = type.GetProperty(propertyName);
		//
		// 	// Convert.ChangeType does not handle conversion to nullable types
		// 	// if the property type is nullable, we need to get the underlying type of the property
		// 	SetModelCollectionProperty(target, property, value);
		// }

		public static Type GetSingularType(Type type) {
			var nonNullableTargetType = DeNullType(type);
			var isEnumerable = IsEnumerableType(nonNullableTargetType);
			return isEnumerable ? InnerType(nonNullableTargetType)! : type;
		}

		public static object ImmutableArrayOfType(Type itemType, IEnumerable items) {
			var itemsType = items.GetType();
			var singularType = GetSingularType(itemsType);
			var singularTypeObject = singularType == typeof(object);
			var isImmutableArray = itemsType == typeof(ImmutableArray<>) || itemsType == typeof(ImmutableArray) || itemsType == typeof(ImmutableArray<object>);
			var isSameSingularType = singularType == itemType;
			if (isImmutableArray && isSameSingularType)
				return items;
			//return EnumerableAdapter.Create(singularType, items).ToImmutableArray();
			return EnumerableAdapter.CreateImmutableArray(itemType, items);
			
			
			throw new ArgumentException("Currently items must be already of type ImmutableArray<ModelType>");
			
			// Type constructedType = typeof(ImmutableArray).MakeGenericType(new Type[] { itemType });
			// var itemsArray = items.Cast<object>().ToArray();
			// typeof(ImmutableArray).GetMethod("CreateRange", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
			//return Activator.  CreateInstance(constructedType, new object[] { itemsArray });

			// ImmutableArray.CreateRange<int>(new int[] {1,2,3});

			//var result = ((IEnumerable)items).Cast<int>();
			
			// var method = typeof(ImmutableArray).GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(m => m.Name == "CreateRange" && m.GetParameters().Length == 1);
			// 	
			//
			// var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType)); //,new object[] { items });
			// list.AddRange()
			// 	// "CreateRange",
			// 	// BindingFlags.Public | BindingFlags.Static,
			// 	// null, 
			// 	// new Type[] { typeof(IEnumerable<>).MakeGenericType(new Type[] { itemType }) },
			// 	// null
			// 	// );
			// Type[] genericArguments = new Type[] { itemType };
			// MethodInfo genericMethod = method!.MakeGenericMethod(genericArguments);
			// var result = genericMethod.Invoke(null, new object[] { list });
			// var result = typeof(ImmutableArray).InvokeMember(
			// 	"CreateRange",
			// 	BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Static,
			// 	null,
			// 	null,
			// 	new object[] { null, items }
			// );
			//return result;
		}

		// public static object CreateImmutableList(Type type)
		// {
		// 	MethodInfo method = typeof(ImmutableList).GetMethod(nameof(ImmutableList.Create)).MakeGenericMethod(type);
		// 	return method.Invoke(null, new object[] { });
		// }
		//
		// public static object CreateImmutableArray(Type type)
		// {
		// 	MethodInfo method = typeof(ImmutableArray).GetMethod(nameof(ImmutableArray.Create)).MakeGenericMethod(type);
		// 	return method.Invoke(null, new object[] { });
		// }
		
		
		// private static void registerBothWays<X, Y>(ConverterRegistry r, Type converter) {
		// 	r.RegisterConverter<X,Y>(converter);
		// 	r.RegisterConverter<Y,X>(converter);
		// }
		//
		//
		// private static ConverterRegistry buildConverter() {
		// 	ConverterRegistry r = new ConverterRegistry();
		// 	// registerBothWays<string,int>(r,typeof(StringToIntegerConverter));
		// 	// registerBothWays<string,bool>(r,typeof(StringToBoolConverter));
		// 	return r;
		// } 
		//
		//
		// private static ConverterRegistry? _converter;

		// public static object? ConvertTo(Type type, object? value, object? defaultValue = null) {
		// 	if (value == null)
		// 		return null;
		// 	if (_converter == null) _converter = buildConverter();
		// 	var sourceType = value == null ? null : value.GetType();
		// 	var tryConvert = _converter.TryConvert(sourceType,type,value,defaultValue);
		// 	//var tryConvert = _converter.Convert(value.GetType(),value);
		// 	return tryConvert;
		// }

		
		public static object? ConvertTo(Type type, object? value, object? defaultValue = null)
		{
			if (value == null || value == DBNull.Value)
			{
				return defaultValue;
			}

			try
			{
				// If the value is already of the target type, return it directly.
				if (type.IsInstanceOfType(value))
				{
					return value;
				}

				// Use TypeConverter for general type conversion.
				System.ComponentModel.TypeConverter converter = TypeDescriptor.GetConverter(type);
				if (converter != null && converter.CanConvertFrom(value.GetType()))
				{
					return converter.ConvertFrom(value);
				}

				// Fallback to Convert.ChangeType for basic types.
				return Convert.ChangeType(value, type);
			}
			catch (Exception)
			{
				// In case of any conversion error, return the default value.
				return defaultValue;
			}
		}		
		
		public static Type GetCascadeIdType(Type cascadeModelType) {
			return CascadeIdPropertyRequired(cascadeModelType).PropertyType;
		}

		public static Type GetCascadeIdType(object cascadeModel) {
			return CascadeIdPropertyRequired(cascadeModel).PropertyType;
		}

		public static object GetCascadeId(object cascadeModel) {
			return CascadeIdPropertyRequired(cascadeModel).GetValue(cascadeModel);
		}

		public static void SetCascadeId(object cascadeModel, object id) {
			CascadeIdPropertyRequired(cascadeModel).SetValue(cascadeModel,id);
		}
		
		public static object? TryGetCascadeId(object? cascadeModel) {
			if (cascadeModel == null)
				return null;
			return TryGetCascadeIdProperty(cascadeModel.GetType())?.GetValue(cascadeModel);
		}

		public static PropertyInfo? TryGetCascadeIdProperty(Type cascadeModelType) {
			var propertyInfos = cascadeModelType.GetProperties();
			return propertyInfos.FirstOrDefault(pi => {
				return Attribute.IsDefined(pi, typeof(CascadeIdAttribute));
			});
		}

		public static PropertyInfo CascadeIdPropertyRequired(Type cascadeModelType) {
			return TryGetCascadeIdProperty(cascadeModelType)
			  ?? throw new MissingMemberException("The model is missing [CascadeId] on an id property");
		}

		public static PropertyInfo CascadeIdPropertyRequired(object cascadeModel) {
			return CascadeIdPropertyRequired(cascadeModel.GetType());
		}

		
		// Should use instance of CascadeJsonSerialization
		// public static IEnumerable DecodeJsonArray(string stringArray) {
		// 	var elements = JsonSerializer.Deserialize<List<JsonElement>>(stringArray)!;
		// 	var objects = elements.Select<JsonElement,object>(e => {
		// 		switch (e.ValueKind) {
		// 			case JsonValueKind.Number:
		// 				return e.GetInt64()!;
		// 				break;
		// 			case JsonValueKind.String:
		// 				return e.GetString()!;
		// 				break;
		// 			default:
		// 				throw new NotImplementedException(e.GetRawText());
		// 		}
		// 	}).ToImmutableArray();
		// 	return objects;
		// }

		public static bool IsFloatingPoint(object? value) {
			if (value is float) return true;
			if (value is double) return true;
			if (value is decimal) return true;
			return false;
		}

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

		public static readonly Type[] SimpleTypes = new Type[] {
	    typeof(Byte), typeof(SByte), typeof(Int16), typeof(UInt16), typeof(Int32), typeof(UInt32), 
	    typeof(Int64), typeof(UInt64), typeof(Char), typeof(Double), 
	    typeof(Single), typeof(Boolean), typeof(String)
		};
		
		public static bool IsSimple(Type? type) {
			if (type == null)
				return true;
			return SimpleTypes.Contains(type);
		}

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

		public static bool IntegerWillFit(object value, Type destinationType) {
			var valueType = value.GetType();
			return Marshal.SizeOf(valueType) <= Marshal.SizeOf(destinationType);
		}

		public static bool IsIntegerAndWillFit(object value, Type destinationType) {
			var valueType = value.GetType();
			return IsIntegerType(valueType) && IntegerWillFit(value,destinationType);
		}

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
	}
}
