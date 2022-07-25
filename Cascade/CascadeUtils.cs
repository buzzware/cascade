using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using deniszykov.TypeConversion;

namespace Cascade {
	public static class CascadeUtils {
		private static TypeConversionProvider? _converter;

		public static long NowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

		public static object? ConvertTo(Type type, object? value, object? defaultValue = null) {
			if (value == null)
				return null;
			if (_converter==null)
				_converter = new TypeConversionProvider();
			object? result;
			var success = _converter.GetConverter(value.GetType(), type).TryConvert(value, out result);
			return success ? result : defaultValue;
		}

		
		// public async Task Populate(ICascadeModel model, string property) {
		// 	var modelType = model.GetType();
		// 	var propertyInfo = modelType.GetProperty(property);
		//
		// 	if (propertyInfo?.GetCustomAttributes(typeof(HasManyAttribute),true).FirstOrDefault() is HasManyAttribute hasMany) {
		// 		await processHasMany(model, modelType, propertyInfo!, hasMany);
		// 	} else if (propertyInfo?.GetCustomAttributes(typeof(BelongsToAttribute),true).FirstOrDefault() is BelongsToAttribute belongsTo) {
		// 		await processBelongsTo(model, modelType, propertyInfo!, belongsTo);
		// 	}
		// }
		
		public static Type GetCascadeIdType(Type cascadeModelType) {
			return CascadeIdPropertyRequired(cascadeModelType).PropertyType;
		}

		public static Type GetCascadeIdType(object cascadeModel) {
			return CascadeIdPropertyRequired(cascadeModel).PropertyType;
		}

		public static PropertyInfo CascadeIdPropertyRequired(Type cascadeModelType) {
			return cascadeModelType.GetProperties().FirstOrDefault(pi => Attribute.IsDefined(pi, typeof(CascadeIdAttribute))) 
			       ?? throw new MissingMemberException("The model is missing [CascadeId] on an id property");
		}

		public static PropertyInfo CascadeIdPropertyRequired(object cascadeModel) {
			return CascadeIdPropertyRequired(cascadeModel.GetType());
		}

		public static IEnumerable DecodeJsonArray(string stringArray) {
			var elements = JsonSerializer.Deserialize<List<JsonElement>>(stringArray)!;
			var objects = elements.Select<JsonElement,object>(e => {
				switch (e.ValueKind) {
						case JsonValueKind.Number:
							return e.GetInt64()!;
						break;
						case JsonValueKind.String:
							return e.GetString()!;
						break;
						default:
							throw new NotImplementedException(e.GetRawText());
				}
			}).ToImmutableArray();
			return objects;
		}
		
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
		
		
		// public static string[] SplitKey(string aKey) {
		// 	return aKey.Split(new string[] {"__"},StringSplitOptions.RemoveEmptyEntries);
		// }
		//
		// public static string ExtractResource(string aKey) {
		// 	if (aKey==null)
		// 		return null;
		// 	var parts = SplitKey(aKey);
		// 	return parts.Length == 0 ? null : parts[0];
		// }
		//
		// public static string ExtractId(string aKey) {
		// 	if (aKey==null)
		// 		return null;
		// 	var parts = SplitKey(aKey);
		// 	return parts.Length < 2 ? null : parts[1];
		// }
		//
		// public static long ExtractLongId(string aKey) {
		// 	if (aKey==null)
		// 		return 0;
		// 	var id = ExtractId(aKey);
		// 	return id == null ? 0 : LongId(id);
		// }
		//
		// public static long LongId(string aResourceId) {
		// 	EnsureIsResourceId(aResourceId);
		// 	return Convert.ToInt64(aResourceId);
		// }
		//
		// public static string EnsureIsResourceId(string aResourceId) {
		// 	if (!IsResourceId(aResourceId))
		// 		throw new Exception("aResourceId is not a valid resource id");
		// 	return aResourceId;
		// }
		//
		// public static long EnsureIsResourceId(long aResourceId) {
		// 	if (aResourceId==0)
		// 		throw new Exception("aResourceId is not a valid resource id");
		// 	return aResourceId;
		// }
		//
		// public static bool IsResourceId(string aResourceId) {
		// 	return !(aResourceId == null || aResourceId == "0" || aResourceId == "");
		// }		
		//
		// public static bool IsResourceId(int aResourceId) {
		// 	return !(aResourceId == 0);
		// }
		//
		// public static bool IsResourceId(long aResourceId) {
		// 	return !(aResourceId == 0);
		// }
		//
		//
		// public static string JoinKey(string resource, string id) {
		// 	if (resource==null)
		// 		throw new ArgumentException("A key needs a resource");
		// 	if (id == null)
		// 		return resource;
		// 	return resource + "__" + id;
		// }
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
				if (!CascadeUtils.IsEqual(v, array_b[i]))
					return false;
				i += 1;
				return true;
			});
		}
	}
}
