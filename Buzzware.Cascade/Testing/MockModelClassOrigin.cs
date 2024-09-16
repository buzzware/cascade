using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Buzzware.Cascade.Testing {

  /// <summary>
  /// MockModelClassOrigin is a mock implementation of the IModelClassOrigin interface used for testing purposes.
  /// </summary>
  public class MockModelClassOrigin<M> : IModelClassOrigin where M : SuperModel {

    /// <summary>
    /// origin instance
    /// </summary>
    public ICascadeOrigin Origin { get; set; }
    
    /// <summary>
    /// Transforms a JsonElement into a natural C# type, such as string, boolean, array, etc.
    /// Handles conversion of JSON arrays, objects, and primitive types.
    /// </summary>
    /// <param name="e">The JSON element to be converted.</param>
    /// <returns>A natural C# type representation of the JSON element, or null if the element is undefined or null.</returns>
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
    private readonly Dictionary<string, byte[]> blobs = new Dictionary<string, byte[]>();

    // create a new model instance of the generic type, optionally with the proxyFor parameter
    private M CreateModel(M proxyFor = null) {
      if (proxyFor==null)
        return (M)Activator.CreateInstance(typeof(M));
      else
        return (M)Activator.CreateInstance(typeof(M),new object[] {proxyFor}); 
    }
    
    /// <summary>
    /// Queries the stored models based on provided criteria.
    /// The criteria are matched against model properties to filter the collection.
    /// </summary>
    /// <param name="criteria">The criteria used to filter the models.</param>
    /// <returns>An enumerable of models that match the given criteria.</returns>
    public async Task<IEnumerable> Query(object criteria) {
      JsonElement? crit = criteria as JsonElement?;
      if (crit == null)
        crit = JsonSerializer.SerializeToElement(criteria);

      var enumerable1 = models.ToList().FindAll(idModel => {
        var model = idModel.Value!;
        var critList = crit!.Value.EnumerateObject().All(p => {
          var mv = FastReflection.TryGetValue(model,p.Name)?.ToString();
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
    
    /// <summary>
    /// Retrieves a model based on its identifier.
    /// </summary>
    /// <param name="id">The identifier of the model to retrieve.</param>
    /// <returns>The model associated with the given id, or null if not found.</returns>
    public async Task<object?> Get(object id) {
      var idType = CascadeTypeUtils.GetCascadeIdType(typeof(M));
      var id2 = CascadeTypeUtils.ConvertTo(idType!, id);
      models.TryGetValue(id2!, out var result);
      return result;
    }

    /// <summary>
    /// Retrieves blob data from the stored path.
    /// </summary>
    /// <param name="path">The path identifying the blob.</param>
    /// <returns>The blob data as a byte array, or null if no data is found.</returns>
    public async Task<byte[]?> GetBlob(string path) {
      blobs.TryGetValue(path, out var result);
      return result;
    }

    /// <summary>
    /// Replaces blob data on specified path.
    /// Removes existing data if the new data is null.
    /// </summary>
    /// <param name="path">The path to store or update the blob data.</param>
    /// <param name="data">The blob data to store, or null to remove existing data.</param>
    public async Task PutBlob(string path, byte[]? data) {
      if (data == null)
        blobs.Remove(path);
      else
        blobs[path] = data;
    }

    /// <summary>
    /// Creates a new model with the given initial data.
    /// Uses the Origin to generate a new unique identifier for the model.
    /// </summary>
    /// <param name="value">The initial data for the new model.</param>
    /// <returns>The created model object.</returns>
    public async Task<object> Create(object value) {
      var result = OfflineUtils.CreateOffline((SuperModel)value, Origin.NewGuid);
      var id = CascadeTypeUtils.GetCascadeId(result);
      models[id] = (M)result;
      return result;
    }

    /// <summary></summary>
    public async Task<object> Replace(object model) {
      var classInfo = FastReflection.GetClassInfo(model);
      var blank = (Activator.CreateInstance(classInfo.Type) as SuperModel)!;
      FastReflection.CopyProperties(model, blank, classInfo.DataAndIdNames);
      return blank;
    }

    /// <summary></summary>
    public async Task<object> Update(object id, IDictionary<string, object?> changes, object? model) {
      var classInfo = FastReflection.GetClassInfo(model);
      var blank = (Activator.CreateInstance(classInfo.Type) as SuperModel)!;
      FastReflection.CopyProperties(model, blank, classInfo.DataAndIdNames);
      blank.__ApplyChanges(changes);
      return blank;
    }

    /// <summary></summary>
    public Task Destroy(object model) {
      throw new System.NotImplementedException();
    }

    /// <summary>
    /// Ensures that the origin is authenticated.
    /// </summary>
    public async Task EnsureAuthenticated() {
    }

    /// <summary>
    /// Clears the authentication data associated with the origin.
    /// </summary>
    public async Task ClearAuthentication() {
    }

    /// <summary></summary>
    public Task<object> Execute(RequestOp request, bool connectionOnline) {
      throw new System.NotImplementedException();
    }
    
    /// <summary></summary>
    public Task Execute(string action, IDictionary<string, object?> parameters) {
      throw new System.NotImplementedException();
    }

    /// <summary>
    /// Stores the provided model under the given identifier.
    /// </summary>
    /// <param name="id">The identifier under which the model should be stored.</param>
    /// <param name="model">The model to store.</param>
    public async Task Store(object id, M model) {
      var idType = CascadeTypeUtils.GetCascadeIdType(typeof(M));
      var id2 = CascadeTypeUtils.ConvertTo(idType!, id);
      models[id] = model;
    }
  }
}
