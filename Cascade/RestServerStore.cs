//using System;
//using System.Collections.Generic;
//using System.Threading.Tasks;
//
//namespace Cascade {
//	public class RestServerStore : HttpService, ICascadeStore
//	{
//
//		public static bool isTypeof<T>(object aObject) {
//			return (aObject is T);
//		}
//
//		public static bool isTypeof<T>(Type aType) where T : class {
//			return (typeof(T).AssemblyQualifiedName==aType.AssemblyQualifiedName);
//		}
//
//		public static string resourceUrl<T>() where T : class {
//			string url = null;
//			if (isTypeof<Person>(typeof(T))) {
//				url = "people/";
//			} else if (isTypeof<Address>(typeof(T))) {
//				url = "addresses/";
//			}
//			if (url==null)
//				throw new NotImplementedException("T is not yet implemented");
//			return url;
//		}
//
//		public static long resourceId<T>(T aModel) where T : class, ICascadeModel
//		{
//			return CascadeUtils.LongId(aModel.GetResourceId());
//		}
//
//		public static long primaryKey<T>(T aModel) where T : class, ICascadeModel {
//			return (aModel as T).id;
//		}
//		
//		public static string resourceUrl<T>(T aModel) where T : class, ICascadeModel {
//			var id = aModel.GetResourceId();
//			if (id==null || id == "" || id == "0")
//				throw new Exception("id must be set");
//			return resourceUrl<T>() + aModel.GetResourceId();
//		}
//
//		public static string resourceUrl<T>(long aId) where T : class, ICascadeModel
//		{
//			return resourceUrl<T>() + aId;		
//		}
//
//		public async Task<T> Read<T>(long aId) where T : class, ICascadeModel
//		{
//			string url = resourceUrl<T>() + aId;
//			HttpResponseMessage response = await GetAsync(url);
//			if (response.StatusCode == HttpStatusCode.NotFound)
//				return default(T);
//			if (!response.IsSuccessStatusCode)
//				return default(T);
//			return (T)JsonConvert.DeserializeObject(await response.Content.ReadAsStringAsync(), typeof(T), new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
//		}
//
//		public async Task<List<T>> ReadAll<T>() where T : class, ICascadeModel
//		{
//			HttpResponseMessage response = await GetAsync(resourceUrl<T>());
//			if (response.StatusCode==HttpStatusCode.NotFound)
//				return new List<T>();
//			response.EnsureSuccessStatusCode ();			
//			return	(List<T>)JsonConvert.DeserializeObject(await response.Content.ReadAsStringAsync(),typeof(List<T>),new JsonSerializerSettings{NullValueHandling=NullValueHandling.Ignore});
//		}
//
//		public async Task<T> Write<T>(T aModel) where T : class, ICascadeModel {
//			JObject wrapper = new JObject();
//			if (typeof(T) == typeof(Person)) {
//				wrapper["person"] = JToken.FromObject(aModel, Json.serializer);
//			} else if (typeof(T) == typeof(Address)) {
//				wrapper["address"] = JToken.FromObject(aModel, Json.serializer);
//			} else {
//				throw new NotImplementedException("T is not yet implemented");
//			}
//
//			HttpResponseMessage response = null;
//			T result = null;
//						
//			if (!CascadeUtils.IsResourceId(aModel.GetResourceId())) {
//				response = await PostAsync(
//					resourceUrl<T>(),
//					wrapper
//				);
//			} else {
//				response = await PutAsync(
//					resourceUrl(aModel),
//					wrapper
//				);
//				if (response.StatusCode == HttpStatusCode.NotFound)
//					return null;
//			}
//			response.EnsureSuccessStatusCode();
//			result = Json.Deserialize<T>(await response.Content.ReadAsStringAsync());
//			return result;
//		}
//
//		public async Task Delete<T>(long aId) where T : class, ICascadeModel
//		{
//			HttpResponseMessage response = await DeleteAsync(resourceUrl<T>(aId));
//			if (response.StatusCode==HttpStatusCode.NotFound)
//				return;
//			response.EnsureSuccessStatusCode ();
//		}
//
//		public Task<OpResponse<M>> Read<M>(string aResourceId) where M : class, ICascadeModel, new()
//		{
//			return Read<M>(CascadeUtils.LongId(aResourceId));
//		}
//
//		public async Task<OpResponse<M>> Read<M>(long aResourceId) where M : class, ICascadeModel, new()
//		{
//			CascadeUtils.EnsureIsResourceId(aResourceId);
//			var result = new OpResponse<M>();
//			result.value = await Read<M>(aResourceId);
//			result.Connected = true;
//			result.present = result.value != null;
//			return result;
//		}
//
//		public async Task<OpResponse<List<M>>> ReadAll<M>() where M : class, ICascadeModel, new()
//		{
//			var result = new OpResponse<List<M>>();
//			var response = await ReadAll<M>();
//			result.value = response ?? new List<M>();
//			result.Connected = true;
//			result.present = response != null;
//			return result;
//		}
//
//		public async Task<OpResponse<M>> Write<M>(M value) where M : class, ICascadeModel, new()
//		{
//			var result = new OpResponse<M>();
//			result.value = await Write<M>(value);
//			result.Connected = true;
//			result.present = result.value != null;
//			return result;
//		}
//
//		public Task<OpResponse<M>> Destroy<M>(string aResourceId) where M : class, ICascadeModel, new()
//		{
//			return Destroy<M>(CascadeUtils.LongId(aResourceId));
//		}
//
//		public async Task<OpResponse<M>> Destroy<M>(long aResourceId) where M : class, ICascadeModel
//		{
//			CascadeUtils.EnsureIsResourceId(aResourceId);
//			var result = new OpResponse<M>();
//			await Delete<M>(aResourceId);
//			result.value = default(M);
//			result.Connected = true;
//			result.present = false; // not sure actually //response > 0;	// was present
//			return result;
//		}
//
//		public Task<OpResponse<M>> DestroyExcept<M>(IEnumerable<string> ids) where M : class, ICascadeModel, new()
//		{
//			throw new NotImplementedException();
//		}
//	}
//}