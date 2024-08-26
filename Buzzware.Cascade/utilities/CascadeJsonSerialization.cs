using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Serilog;

namespace Buzzware.Cascade {
	
  /// <summary>
  /// CascadeJsonSerialization handles JSON serialization and deserialization within the Cascade framework.
  /// Provides utilities to serialize and deserialize model objects while considering configurable options 
  /// to ignore certain properties based on defined conditions.
  /// Implemented using System.Text.Json 
  /// </summary>
	public class CascadeJsonSerialization {
		private readonly bool ignoreUnderscoreProperties;
		private readonly bool ignoreAssociations;
		private JsonSerializerOptions dictionaryNormalizedOptions;
		private JsonSerializerOptions jsonSerializerOptions;

    /// <summary>
    /// CascadeJsonSerialization Constructor
    /// </summary>
    /// <param name="ignoreUnderscoreProperties">Specifies whether properties starting with an underscore should be ignored during serialization.</param>
    /// <param name="ignoreAssociations">Specifies whether association properties should be ignored during serialization.</param>
		public CascadeJsonSerialization(
			Boolean ignoreUnderscoreProperties = true,
			Boolean ignoreAssociations = true
		) {
			this.ignoreUnderscoreProperties = ignoreUnderscoreProperties;
			this.ignoreAssociations = ignoreAssociations;

      // Options for normalizing dictionary during JSON serialization
			dictionaryNormalizedOptions = new JsonSerializerOptions() {
				TypeInfoResolver = new DefaultJsonTypeInfoResolver {
					Modifiers = { IgnoreProperties }
				},
				Converters = {
					new DictionaryJsonConverter(),
					new ImmutableDictionaryJsonConverter(),
				}
			};
			
      // General options for JSON serialization
			jsonSerializerOptions = new JsonSerializerOptions() {
				TypeInfoResolver = new DefaultJsonTypeInfoResolver
				{
					Modifiers = { IgnoreProperties }
				},
			};
		}
		
    /// <summary>
    /// Method used to determine whether certain properties should be ignored based on custom logic
    /// </summary>
    /// <param name="typeInfo">The JSON type information used to analyze properties for serialization</param>
		private void IgnoreProperties(JsonTypeInfo typeInfo) {
			if (!typeInfo.Type.IsSubclassOf(typeof(SuperModel)))
				return;

			foreach (JsonPropertyInfo propertyInfo in typeInfo.Properties) {
				propertyInfo.ShouldSerialize = (obj, value) => !(
					ignoreUnderscoreProperties && propertyInfo.Name.StartsWith("_") ||
					ignoreAssociations && (propertyInfo.AttributeProvider != null && CascadeDataLayer.AssociationAttributes.Any(t => propertyInfo.AttributeProvider.GetCustomAttributes(t, false).Any())) ||
					(!CascadeTypeUtils.IsSimple(propertyInfo.PropertyType) && propertyInfo.AttributeProvider == null) || // non-primitive values without attributes
					(propertyInfo.AttributeProvider?.GetCustomAttributes(typeof(FromBlobAttribute), false).Any() ?? false) ||
					(propertyInfo.AttributeProvider?.GetCustomAttributes(typeof(FromPropertyAttribute), false).Any() ?? false)
				);
			}
		}

    /// <summary>
    /// Deserializes a JSON string into an immutable dictionary.
    /// </summary>
    /// <param name="source">The JSON string to be deserialized.</param>
    /// <returns>An immutable dictionary of string keys and object values.</returns>
		public ImmutableDictionary<string,object?> DeserializeImmutableDictionary(string source) {
			try {
				return JsonSerializer.Deserialize<ImmutableDictionary<string,object?>>(source, dictionaryNormalizedOptions)!;
			} catch (Exception e) {
				Log.Warning($"Failed Deserializing as ImmutableDictionary: "+e.Message);
				Log.Debug(source);
				throw;
			}
		}
		
    /// <summary>
    /// Deserializes a JSON string into a dictionary of normal types ie int, string, bool, double Vs eg. the JsonEntity instances returned by System.Text.Json
    /// </summary>
    /// <param name="source">The JSON string to be deserialized.</param>
    /// <returns>A dictionary of string keys and object values.</returns>
		public Dictionary<string,object?> DeserializeDictionaryOfNormalTypes(string source) {
			try {
				return JsonSerializer.Deserialize<Dictionary<string,object?>>(source, dictionaryNormalizedOptions)!;
			} catch (Exception e) {
				Log.Warning($"Failed Deserializing as Dictionary: "+e.Message);
				Log.Debug(source);
				throw;
			}
		}

    /// <summary>
    /// Deserializes a JSON element into a dictionary of normal types ie int, string, bool, double Vs eg. the JsonEntity instances returned by System.Text.Json
    /// </summary>
    /// <param name="source">The JSON element to be deserialized.</param>
    /// <returns>A dictionary of string keys and object values.</returns>
		public Dictionary<string,object?> DeserializeDictionaryOfNormalTypes(JsonElement source) {
			try {
				return JsonSerializer.Deserialize<Dictionary<string,object?>>(source, dictionaryNormalizedOptions)!;
			} catch (Exception e) {
				Log.Warning($"Failed Deserializing as Dictionary: "+e.Message);
				//Log.Debug(source);
				throw;
			}
		}
		
    /// <summary>
    /// Deserializes a JSON string into a specific object type.
    /// </summary>
    /// <param name="type">The Type into which the JSON should be deserialized.</param>
    /// <param name="source">The JSON string to be deserialized.</param>
    /// <returns>The deserialized object of specified type.</returns>
		public object DeserializeType(Type type, string source) {
			try {
				return JsonSerializer.Deserialize(source, type, dictionaryNormalizedOptions)!;
			} catch (Exception e) {
				Log.Warning($"Failed Deserializing as {type.Name}: "+e.Message);
				Log.Debug(source);
				throw;
			}
		}
		
    /// <summary>
    /// Deserializes a JSON element into a specific object type.
    /// </summary>
    /// <param name="type">The Type into which the JSON should be deserialized.</param>
    /// <param name="source">The JSON element to be deserialized.</param>
    /// <returns>The deserialized object of specified type.</returns>
		public object DeserializeType(Type type, JsonElement source) {
			try {
				return JsonSerializer.Deserialize(source, type, dictionaryNormalizedOptions)!;
			} catch (Exception e) {
				Log.Warning($"Failed Deserializing as {type.Name}: "+e.Message);
				//Log.Debug(source);
				throw;
			}
		}
		
    /// <summary>
    /// Deserializes a JSON string into a specific generic type.
    /// </summary>
    /// <typeparam name="T">The type into which the JSON should be deserialized.</typeparam>
    /// <param name="source">The JSON string to be deserialized.</param>
    /// <returns>The deserialized object of type T.</returns>
		public T DeserializeType<T>(string? source) {
			try {
				return JsonSerializer.Deserialize<T>(source, dictionaryNormalizedOptions)!;
			} catch (Exception e) {
				Log.Warning($"Failed Deserializing as {typeof(T).Name}: "+e.Message);
				Log.Debug(source);
				throw;
			}
		}

    /// <summary>
    /// Deserializes a JSON element into a specific generic type.
    /// </summary>
    /// <typeparam name="T">The type into which the JSON should be deserialized.</typeparam>
    /// <param name="element">The JSON element to be deserialized.</param>
    /// <returns>The deserialized object of type T.</returns>
		public T DeserializeType<T>(JsonElement element) {
			try {
				return JsonSerializer.Deserialize<T>(element, dictionaryNormalizedOptions)!;
			} catch (Exception e) {
				Log.Warning($"Failed Deserializing as {typeof(T).Name}: "+e.Message);
				Log.Debug(element.ToString());
				throw;
			}
		}
		
    /// <summary>
    /// Deserializes a JSON string into a JsonElement.
    /// </summary>
    /// <param name="source">The JSON string to be deserialized.</param>
    /// <returns>A JsonElement representing the deserialized JSON.</returns>
		public JsonElement DeserializeElement(string? source) {
			try {
				return JsonSerializer.Deserialize<JsonElement>(source,dictionaryNormalizedOptions);
			} catch (Exception e) {
				Log.Warning($"Failed Deserializing as JsonElement: "+e.Message);
				Log.Debug(source);
				throw;
			}
		}

    /// <summary>
    /// Deserializes an IEnumerable of JsonElements into a collection of type M.
    /// </summary>
    /// <typeparam name="M">The type of objects to be deserialized into, constrained to class types.</typeparam>
    /// <param name="elements">The collection of JsonElements to be deserialized.</param>
    /// <returns>An IEnumerable of deserialized type M objects.</returns>
		public IEnumerable<M> DeserializeEnumerable<M>(IEnumerable<JsonElement> elements) where M : class {
			return elements.Select(element => {
				return DeserializeType<M>(element);
			});
		}
		
    /// <summary>
    /// Serializes an object into a JSON string.
    /// </summary>
    /// <param name="value">The object to be serialized.</param>
    /// <returns>A JSON string representation of the object.</returns>
		public string? Serialize(object value) {
			try {
				return JsonSerializer.Serialize(SerializeToNode(value), dictionaryNormalizedOptions);
			} catch (Exception e) {
				Log.Warning($"Failed Serialize model: "+e.Message);
				throw;
			}
		}
		
    /// <summary>
    /// Serializes an object into a JsonNode.
    /// </summary>
    /// <param name="value">The object to be serialized.</param>
    /// <returns>A JsonNode representing the serialized object.</returns>
		public JsonNode SerializeToNode(object value) {
			try {
				var node = JsonSerializer.SerializeToNode(value, dictionaryNormalizedOptions);
				return node;
			} catch (Exception e) {
				Log.Warning($"Failed SerializeToNode: "+e.Message);
				throw;
			}
		}

    /// <summary>
    /// Serializes an object to a JsonElement.
    /// </summary>
    /// <param name="value">The object to be serialized.</param>
    /// <returns>A JsonElement representing the serialized object.</returns>
		public JsonElement SerializeToElement(object value) {
			try {
				var element = JsonSerializer.SerializeToElement(value, dictionaryNormalizedOptions);
				return element;
			} catch (Exception e) {
				Log.Warning($"Failed SerializeToElement model: "+e.Message);
				throw;
			}
		}
	}
}
