using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Serilog;

namespace Cascade {
	
	public class CascadeJsonSerialization {
		private readonly bool ignoreUnderscoreProperties;
		private readonly bool ignoreAssociations;
		private JsonSerializerOptions dictionaryNormalizedOptions;
		private JsonSerializerOptions jsonSerializerOptions;

		public CascadeJsonSerialization(
			Boolean ignoreUnderscoreProperties = true,
			Boolean ignoreAssociations = true
		) {
			this.ignoreUnderscoreProperties = ignoreUnderscoreProperties;
			this.ignoreAssociations = ignoreAssociations;
			this.dictionaryNormalizedOptions = new JsonSerializerOptions() {
				TypeInfoResolver = new DefaultJsonTypeInfoResolver {
					Modifiers = { IgnoreProperties }
				},
				Converters = {
					new DictionaryJsonConverter()
				}
			};
			jsonSerializerOptions = new JsonSerializerOptions() {
				//MaxDepth = maxDepth,
				//Converters = { new LambdaIgnoreConverter(name => ) }
				TypeInfoResolver = new DefaultJsonTypeInfoResolver
				{
					Modifiers = { IgnoreProperties }
				},
				// AllowTrailingCommas = false,
				// DefaultBufferSize = 0,
				// Encoder = null,
				// DictionaryKeyPolicy = null,
				//DefaultIgnoreCondition = JsonIgnoreCondition.Never,
				// NumberHandling = JsonNumberHandling.Strict,
				// IgnoreReadOnlyProperties = false,
				// IgnoreReadOnlyFields = false,
				// IncludeFields = false,
				// PropertyNamingPolicy = null,
				// PropertyNameCaseInsensitive = false,
				// ReadCommentHandling = JsonCommentHandling.Disallow,
				// UnknownTypeHandling = JsonUnknownTypeHandling.JsonElement,
				// WriteIndented = false,
				// ReferenceHandler = null
			};
		}

		private void IgnoreProperties(JsonTypeInfo typeInfo) {
			if (!typeInfo.Type.IsSubclassOf(typeof(SuperModel)))
				return;

			foreach (JsonPropertyInfo propertyInfo in typeInfo.Properties) {
				propertyInfo.ShouldSerialize = (obj, value) => !(
					ignoreUnderscoreProperties && propertyInfo.Name.StartsWith("_") ||
					ignoreAssociations && !propertyInfo.PropertyType.IsPrimitive && propertyInfo.AttributeProvider != null && CascadeDataLayer.AssociationAttributes.Any(t => propertyInfo.AttributeProvider.GetCustomAttributes(t,false).Any())
				);
			}
		}
		
		public object DeserializeDictionaryOfNormalTypes(string source) {
			try {
				return JsonSerializer.Deserialize<Dictionary<string,object>>(source, dictionaryNormalizedOptions)!;
			} catch (Exception e) {
				Log.Warning($"Failed Deserializing as Dictionary: "+e.Message);
				Log.Debug(source);
				throw;
			}
		}

		public object DeserializeDictionaryOfNormalTypes(JsonElement source) {
			try {
				return JsonSerializer.Deserialize<Dictionary<string,object>>(source, dictionaryNormalizedOptions)!;
			} catch (Exception e) {
				Log.Warning($"Failed Deserializing as Dictionary: "+e.Message);
				//Log.Debug(source);
				throw;
			}
		}
		
		public object DeserializeType(Type type, string source) {
			try {
				return JsonSerializer.Deserialize(source, type, jsonSerializerOptions)!;
			} catch (Exception e) {
				Log.Warning($"Failed Deserializing as ${type.Name}: "+e.Message);
				Log.Debug(source);
				throw;
			}
		}
		
		public object DeserializeType(Type type, JsonElement source) {
			try {
				return JsonSerializer.Deserialize(source, type, jsonSerializerOptions)!;
			} catch (Exception e) {
				Log.Warning($"Failed Deserializing as ${type.Name}: "+e.Message);
				//Log.Debug(source);
				throw;
			}
		}
		
		public T DeserializeType<T>(string? source) {
			try {
				return JsonSerializer.Deserialize<T>(source, jsonSerializerOptions)!;
			} catch (Exception e) {
				Log.Warning($"Failed Deserializing as ${typeof(T).Name}: "+e.Message);
				Log.Debug(source);
				throw;
			}
		}

		public T DeserializeType<T>(JsonElement element) {
			try {
				return JsonSerializer.Deserialize<T>(element, jsonSerializerOptions)!;
			} catch (Exception e) {
				Log.Warning($"Failed Deserializing as ${typeof(T).Name}: "+e.Message);
				Log.Debug(element.ToString());
				throw;
			}
		}
		
		public JsonElement DeserializeElement(string? source) {
			try {
				return JsonSerializer.Deserialize<JsonElement>(source);
			} catch (Exception e) {
				Log.Warning($"Failed Deserializing as JsonElement: "+e.Message);
				Log.Debug(source);
				throw;
			}
		}

		public IEnumerable<M> DeserializeEnumerable<M>(IEnumerable<JsonElement> elements) where M : class {
			return elements.Select(element => {
				return DeserializeType<M>(element);
			});
		}
		
		public string? Serialize(object model) {
			try {
				return JsonSerializer.Serialize(SerializeToNode(model));
			} catch (Exception e) {
				Log.Warning($"Failed Serialize model: "+e.Message);
				throw;
			}
		}
		
		public JsonNode SerializeToNode(object model) {
			try {
				var node = JsonSerializer.SerializeToNode(model, jsonSerializerOptions);
				return node;
			} catch (Exception e) {
				Log.Warning($"Failed SerializeToNode: "+e.Message);
				throw;
			}
		}

		public JsonElement SerializeToElement(object model) {
			try {
				var element = JsonSerializer.SerializeToElement(model, jsonSerializerOptions);
				return element;
			} catch (Exception e) {
				Log.Warning($"Failed SerializeToElement model: "+e.Message);
				throw;
			}
		}
		
		bool isAssociation(Type modelType, object model, string propertyName) {
			var propertyInfo = modelType.GetProperty(propertyName)!;
			if (propertyInfo.PropertyType.IsPrimitive)
				return false;
			return CascadeDataLayer.AssociationAttributes.Any(t => propertyInfo.GetCustomAttributes(t,false).Any());
		}
	}
}
