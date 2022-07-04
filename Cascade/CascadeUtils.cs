using System;
using deniszykov.TypeConversion;

namespace Cascade {
	public static class CascadeUtils {
		private static TypeConversionProvider? _converter;

		public static long NowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

		public static object? ConvertTo(Type type, object value, object? defaultValue = null) {
			if (_converter==null)
				_converter = new TypeConversionProvider();
			object? result;
			var success = _converter.GetConverter(value.GetType(), type).TryConvert(value, out result);
			return success ? result : defaultValue;
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
	}
}
