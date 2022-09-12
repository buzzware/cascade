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
		public static long NowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();


		// "Where Collections" are collections whose key begins with "WHERE__" and is defined using this function.
		// Their key fully describes what they are - a collection of the given type where the given property has the given value.
		// In future, the framework could parse and evaluate these names as a query eg. to refresh them
		// Other collections should not begin with "WHERE__", and property names or values should not contain more than one consecutive underscore
		// This function is primarily intended for collections created for associations, especially HasMany.
		public static string WhereCollectionKey(string typeName,string property,string value) {
			return $"WHERE__{typeName}__{property}__{value}";
		}

		public static string CollectionKeyFromName(string typeName, string collectionName) {
			return typeName+"__"+collectionName;
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
