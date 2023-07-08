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

		public CascadeJsonSerialization(
			Boolean ignoreUnderscoreProperties = true,
			Boolean ignoreAssociations = true
		) {
			this.ignoreUnderscoreProperties = ignoreUnderscoreProperties;
			this.ignoreAssociations = ignoreAssociations;
		}

		public object DeserializeDictionaryOfNormalTypes(string source) {
			try {
				return JsonSerializer.Deserialize<Dictionary<string,object>>(source, new JsonSerializerOptions() {
					TypeInfoResolver = new DefaultJsonTypeInfoResolver {
						Modifiers = { IgnoreProperties }
					},
					Converters = {
						new DictionaryJsonConverter()
					}
				})!;
			} catch (Exception e) {
				Log.Warning($"Failed Deserializing as Dictionary: "+e.Message);
				Log.Debug(source);
				throw;
			}
		}

		public object DeserializeDictionaryOfNormalTypes(JsonElement source) {
			try {
				return JsonSerializer.Deserialize<Dictionary<string,object>>(source, new JsonSerializerOptions() {
					TypeInfoResolver = new DefaultJsonTypeInfoResolver {
						Modifiers = { IgnoreProperties }
					},
					Converters = {
						new DictionaryJsonConverter()
					}
				})!;
			} catch (Exception e) {
				Log.Warning($"Failed Deserializing as Dictionary: "+e.Message);
				//Log.Debug(source);
				throw;
			}
		}
		
		public object DeserializeType(Type type, string source) {
			try {
				return JsonSerializer.Deserialize(source, type, new JsonSerializerOptions() {
					TypeInfoResolver = new DefaultJsonTypeInfoResolver {
						Modifiers = { IgnoreProperties }
					},
					// Converters = {
					// 	new DictionaryJsonConverter()
					// }
				})!;
			} catch (Exception e) {
				Log.Warning($"Failed Deserializing as ${type.Name}: "+e.Message);
				Log.Debug(source);
				throw;
			}
		}
		
		public object DeserializeType(Type type, JsonElement source) {
			try {
				return JsonSerializer.Deserialize(source, type, new JsonSerializerOptions() {
					TypeInfoResolver = new DefaultJsonTypeInfoResolver {
						Modifiers = { IgnoreProperties }
					},
					// Converters = {
					// 	new DictionaryJsonConverter()
					// }
				})!;
			} catch (Exception e) {
				Log.Warning($"Failed Deserializing as ${type.Name}: "+e.Message);
				//Log.Debug(source);
				throw;
			}
		}
		
		public T DeserializeType<T>(string source) {
			try {
				return JsonSerializer.Deserialize<T>(source, new JsonSerializerOptions() {
					TypeInfoResolver = new DefaultJsonTypeInfoResolver {
						Modifiers = { IgnoreProperties }
					},
					// Converters = {
					// 	new DictionaryJsonConverter()
					// }
				})!;
			} catch (Exception e) {
				Log.Warning($"Failed Deserializing as ${typeof(T).Name}: "+e.Message);
				Log.Debug(source);
				throw;
			}
		}

		public T DeserializeType<T>(JsonElement element) {
			try {
				return JsonSerializer.Deserialize<T>(element, new JsonSerializerOptions() {
					TypeInfoResolver = new DefaultJsonTypeInfoResolver {
						Modifiers = { IgnoreProperties }
					},
					// Converters = {
					// 	new DictionaryJsonConverter()
					// }
				})!;
			} catch (Exception e) {
				Log.Warning($"Failed Deserializing as ${typeof(T).Name}: "+e.Message);
				Log.Debug(element.ToString());
				throw;
			}
		}
		
		public JsonElement DeserializeElement(string source) {
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
		
		
		// public Dictionary<string,object> DeserializeDictionary(string source) {
		// 	
		// }
		//
		// public Dictionary<string,object> DeserializeDictionaryArray(string source) {
		// 	
		// }
		public string Serialize(object model) {
			return JsonSerializer.Serialize(SerializeToNode(model));
		}

		public JsonNode SerializeToNode(object model, int maxDepth = 1) {

			bool isAssociation(Type modelType, object model, string propertyName) {
				var propertyInfo = modelType.GetProperty(propertyName)!;
				if (propertyInfo.PropertyType.IsPrimitive)
					return false;
				return CascadeDataLayer.AssociationAttributes.Any(t => propertyInfo.GetCustomAttributes(t,false).Any());
			}

			//var modelType = model.GetType();
			var node = JsonSerializer.SerializeToNode(model, options: new JsonSerializerOptions {
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
			});
			return node;
		}

		private void IgnoreProperties(JsonTypeInfo typeInfo) {
			if (!typeInfo.Type.IsSubclassOf(typeof(SuperModel))) // != typeof(MyPoco))
				return;

			foreach (JsonPropertyInfo propertyInfo in typeInfo.Properties) {
				propertyInfo.ShouldSerialize = (obj, value) => !(
					ignoreUnderscoreProperties && propertyInfo.Name.StartsWith("_") ||
					ignoreAssociations && !propertyInfo.PropertyType.IsPrimitive && propertyInfo.AttributeProvider != null && CascadeDataLayer.AssociationAttributes.Any(t => propertyInfo.AttributeProvider.GetCustomAttributes(t,false).Any())
				);
			}
		}
	}
}
