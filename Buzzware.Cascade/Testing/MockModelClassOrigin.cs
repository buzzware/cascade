using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Buzzware.Cascade.Testing {
	public class MockModelClassOrigin<M> : IModelClassOrigin where M : SuperModel {

		public ICascadeOrigin Origin { get; set; }
		
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

		public async Task<IEnumerable> Query(object criteria) {
			JsonElement? crit = criteria as JsonElement?;
			if (crit == null)
				crit = JsonSerializer.SerializeToElement(criteria);
			var enumerable1 = models.ToList().FindAll(idModel => {
				var model = idModel.Value!;
				var critList = crit!.Value.EnumerateObject().All(p => {
					var mv = model!.GetType().GetProperty(p.Name)!.GetValue(model)?.ToString();
					//var v = kv.Value!.GetValue<object>();
					var v = GetNaturalValueFrom(p.Value)?.ToString();
					var equal = CascadeTypeUtils.IsEqual(mv, v);   //(v == null && mv == null) || (v != null && v.Equals(mv)); 
					return equal;
				});
				return critList;
			}).ToImmutableArray();
			var enumerable2 = enumerable1.Select(k => k.Value).ToImmutableArray<M>();
			return enumerable2;
		}
		
		public async Task<object?> Get(object id) {
			var idType = CascadeTypeUtils.GetCascadeIdType(typeof(M));
			var id2 = CascadeTypeUtils.ConvertTo(idType!, id);
			models.TryGetValue(id2!, out var result);
			return result;
		}

		public async Task<object> Create(object value) {
			var result = OfflineUtils.CreateOffline((SuperModel)value, Origin.NewGuid);
			var id = CascadeTypeUtils.GetCascadeId(result);
			models[id] = (M)result;
			return result;
		}


		public Task<object> Replace(object value) {
			throw new System.NotImplementedException();
		}

		public Task<object> Update(object id, IDictionary<string, object> changes, object? model) {
			throw new System.NotImplementedException();
		}

		public Task Destroy(object model) {
			throw new System.NotImplementedException();
		}

		public async Task EnsureAuthenticated() {
		}

		public async Task ClearAuthentication() {
		}

		public Task<object> Execute(RequestOp request, bool connectionOnline) {
			throw new System.NotImplementedException();
		}
		
		public Task Execute(string action, IDictionary<string, object> parameters) {
			throw new System.NotImplementedException();
		}

		public async Task Store(object id, M model) {
			var idType = CascadeTypeUtils.GetCascadeIdType(typeof(M));
			var id2 = CascadeTypeUtils.ConvertTo(idType!, id);
			models[id] = model;
		}
	}
}
