using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Cascade;

namespace Cascade.testing {
	public class MockModelClassOrigin<M> : IModelClassOrigin {

		// static Dictionary<string, object?> DictionaryFromJsonObject(JsonObject jsono) {
		// 	var result = new Dictionary<string, object?>();
		// 	foreach (var pair in jsono) {
		// 		result[pair.Key] = GetNaturalValueFromJson(pair.Value);
		// 	}
		// 	return result;
		// }
		//
		public static object? GetNaturalValueFrom(JsonElement e) {
			switch (e.ValueKind) {
				case JsonValueKind.Array:
					return e.EnumerateArray().Select(GetNaturalValueFrom).ToImmutableArray();
					break;
				case JsonValueKind.Object:
					return e.EnumerateObject().ToImmutableDictionary<JsonProperty, string, object?>(property => property.Name, p => GetNaturalValueFrom(p.Value));
					break;
				case JsonValueKind.Null:
					return null;
					break;
				case JsonValueKind.False: return false;
				case JsonValueKind.True: return true;
				case JsonValueKind.String: return e.GetString();
				case JsonValueKind.Number: return e.GetDouble();
				default:
				case JsonValueKind.Undefined: 
					return null;
			}
		}


		private readonly Dictionary<object, M> models = new Dictionary<object, M>();

		public async Task<IEnumerable<object>> Query(object criteria,string key) {
			JsonElement? crit = criteria as JsonElement?;
			if (crit == null)
				crit = JsonSerializer.SerializeToElement(criteria);
			var enumerable1 = models.ToList().FindAll(idModel => {
				var model = idModel.Value!;
				var critList = crit!.Value.EnumerateObject().All(p => {
					var mv = model!.GetType().GetProperty(p.Name)!.GetValue(model);
					//var v = kv.Value!.GetValue<object>();
					var v = GetNaturalValueFrom(p.Value);
					var equal = CascadeUtils.IsEqual(mv, v);   //(v == null && mv == null) || (v != null && v.Equals(mv)); 
					return equal;
				});
				return critList;
			}).ToImmutableArray();
			var enumerable2 = enumerable1.Select(k => ((object?) k.Value)!).ToImmutableArray();
			return enumerable2;
		}
		
		public async Task<object?> Get(object id) {
			models.TryGetValue(id, out var result);
			return result;
		}

		public async Task Store(object id, M model) {
			models[id] = model;
		}
	}
}
