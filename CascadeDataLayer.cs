using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serilog;

namespace Cascade
{

	public static class CascadeUtils
	{
		public static long LongId(string aResourceId) {
			EnsureIsResourceId(aResourceId);
			return Convert.ToInt64(aResourceId);
		}

		public static string EnsureIsResourceId(string aResourceId) {
			if (!IsResourceId(aResourceId))
				throw new Exception("aResourceId is not a valid resource id");
			return aResourceId;
		}

		public static long EnsureIsResourceId(long aResourceId) {
			if (aResourceId==0)
				throw new Exception("aResourceId is not a valid resource id");
			return aResourceId;
		}

		public static bool IsResourceId(string aResourceId) {
			return !(aResourceId == null || aResourceId == "0" || aResourceId == "");
		}		

		public static bool IsResourceId(int aResourceId) {
			return !(aResourceId == 0);
		}

		public static bool IsResourceId(long aResourceId) {
			return !(aResourceId == 0);
		}


	}

	public class CascadeStoreResponse<M>
	{
		public bool present = false;
		public bool connected = false;
		public M value = default(M);
	}

	public interface ICascadeModel
	{
		string GetResourceId();
	}

	public interface ICascadeStore
	{
		// may leak exceptions for connection etc
		// do not leak exceptions for not found
		Task<CascadeStoreResponse<M>> Read<M>(string aResourceId) where M : class, ICascadeModel, new();
		Task<CascadeStoreResponse<M>> Read<M>(long aResourceId) where M : class, ICascadeModel, new();
		Task<CascadeStoreResponse<List<M>>> ReadAll<M>() where M : class, ICascadeModel, new();
		Task<CascadeStoreResponse<M>> Write<M>(M value) where M : class, ICascadeModel, new();
		Task<CascadeStoreResponse<M>> Destroy<M>(string aResourceId) where M : class, ICascadeModel, new();
		Task<CascadeStoreResponse<M>> DestroyExcept<M>(IEnumerable<string> aResourceIds) where M : class, ICascadeModel, new();
	}

	public class RestServerStore : HttpService, ICascadeStore
	{

		public static bool isTypeof<T>(object aObject) {
			return (aObject is T);
		}

		public static bool isTypeof<T>(Type aType) where T : class {
			return (typeof(T).AssemblyQualifiedName==aType.AssemblyQualifiedName);
		}

		public static string resourceUrl<T>() where T : class {
			string url = null;
			if (isTypeof<Person>(typeof(T))) {
				url = "people/";
			} else if (isTypeof<Address>(typeof(T))) {
				url = "addresses/";
			}
			if (url==null)
				throw new NotImplementedException("T is not yet implemented");
			return url;
		}

		public static long resourceId<T>(T aModel) where T : class, ICascadeModel
		{
			return CascadeUtils.LongId(aModel.GetResourceId());
		}

		public static long primaryKey<T>(T aModel) where T : class, ICascadeModel {
			return (aModel as T).id;
		}
		
		public static string resourceUrl<T>(T aModel) where T : class, ICascadeModel {
			var id = aModel.GetResourceId();
			if (id==null || id == "" || id == "0")
				throw new Exception("id must be set");
			return resourceUrl<T>() + aModel.GetResourceId();
		}

		public static string resourceUrl<T>(long aId) where T : class, ICascadeModel
		{
			return resourceUrl<T>() + aId;		
		}

		public async Task<T> Read<T>(long aId) where T : class, ICascadeModel
		{
			string url = resourceUrl<T>() + aId;
			HttpResponseMessage response = await GetAsync(url);
			if (response.StatusCode == HttpStatusCode.NotFound)
				return default(T);
			if (!response.IsSuccessStatusCode)
				return default(T);
			return (T)JsonConvert.DeserializeObject(await response.Content.ReadAsStringAsync(), typeof(T), new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
		}

		public async Task<List<T>> ReadAll<T>() where T : class, ICascadeModel
		{
			HttpResponseMessage response = await GetAsync(resourceUrl<T>());
			if (response.StatusCode==HttpStatusCode.NotFound)
				return new List<T>();
			response.EnsureSuccessStatusCode ();			
			return	(List<T>)JsonConvert.DeserializeObject(await response.Content.ReadAsStringAsync(),typeof(List<T>),new JsonSerializerSettings{NullValueHandling=NullValueHandling.Ignore});
		}

		public async Task<T> Write<T>(T aModel) where T : class, ICascadeModel {
			JObject wrapper = new JObject();
			if (typeof(T) == typeof(Person)) {
				wrapper["person"] = JToken.FromObject(aModel, Json.serializer);
			} else if (typeof(T) == typeof(Address)) {
				wrapper["address"] = JToken.FromObject(aModel, Json.serializer);
			} else {
				throw new NotImplementedException("T is not yet implemented");
			}

			HttpResponseMessage response = null;
			T result = null;
						
			if (!CascadeUtils.IsResourceId(aModel.GetResourceId())) {
				response = await PostAsync(
					resourceUrl<T>(),
					wrapper
				);
			} else {
				response = await PutAsync(
					resourceUrl(aModel),
					wrapper
				);
				if (response.StatusCode == HttpStatusCode.NotFound)
					return null;
			}
			response.EnsureSuccessStatusCode();
			result = Json.Deserialize<T>(await response.Content.ReadAsStringAsync());
			return result;
		}

		public async Task Delete<T>(long aId) where T : class, ICascadeModel
		{
			HttpResponseMessage response = await DeleteAsync(resourceUrl<T>(aId));
			if (response.StatusCode==HttpStatusCode.NotFound)
				return;
			response.EnsureSuccessStatusCode ();
		}

		public Task<CascadeStoreResponse<M>> Read<M>(string aResourceId) where M : class, ICascadeModel, new()
		{
			return Read<M>(CascadeUtils.LongId(aResourceId));
		}

		public async Task<CascadeStoreResponse<M>> Read<M>(long aResourceId) where M : class, ICascadeModel, new()
		{
			CascadeUtils.EnsureIsResourceId(aResourceId);
			var result = new CascadeStoreResponse<M>();
			result.value = await Read<M>(aResourceId);
			result.connected = true;
			result.present = result.value != null;
			return result;
		}

		public async Task<CascadeStoreResponse<List<M>>> ReadAll<M>() where M : class, ICascadeModel, new()
		{
			var result = new CascadeStoreResponse<List<M>>();
			var response = await ReadAll<M>();
			result.value = response ?? new List<M>();
			result.connected = true;
			result.present = response != null;
			return result;
		}

		public async Task<CascadeStoreResponse<M>> Write<M>(M value) where M : class, ICascadeModel, new()
		{
			var result = new CascadeStoreResponse<M>();
			result.value = await Write<M>(value);
			result.connected = true;
			result.present = result.value != null;
			return result;
		}

		public Task<CascadeStoreResponse<M>> Destroy<M>(string aResourceId) where M : class, ICascadeModel, new()
		{
			return Destroy<M>(CascadeUtils.LongId(aResourceId));
		}

		public async Task<CascadeStoreResponse<M>> Destroy<M>(long aResourceId) where M : class, ICascadeModel
		{
			CascadeUtils.EnsureIsResourceId(aResourceId);
			var result = new CascadeStoreResponse<M>();
			await Delete<M>(aResourceId);
			result.value = default(M);
			result.connected = true;
			result.present = false; // not sure actually //response > 0;	// was present
			return result;
		}

		public Task<CascadeStoreResponse<M>> DestroyExcept<M>(IEnumerable<string> ids) where M : class, ICascadeModel, new()
		{
			throw new NotImplementedException();
		}
	}

	public class SqliteNetStore : ICascadeStore
	{

		public string tableName<T>() {
			var n = typeof(T).Name;
			switch (n) {
				case "Person":
					return "people";
				case "Address":
					return "addresses";
			}
			return n.ToLower() + "s";
		}

		public List<T> FindMany<T>(string query = null, params object[] args) where T : class, new()
		{
			lock (connection) {
				if (query == null) {
					var t = tableName<T>();
					return connection.Query<T>("select * from " + t);
				} else {
					return connection.Query<T>(query, args);
				}
			}
		}

		public T FindOne<T>(string query, params object[] args) where T : class, new()
		{
			lock (connection) {
				return connection.FindWithQuery<T>(query, args);
			}
		}

		public T FindOne<T>(long aId) where T : class, new()
		{
			lock (connection) {
				return connection.Find<T>(aId);
			}
		}

		public long GetPrimaryKey(object aModel) {
			var propertyInfo = aModel.GetType().GetPrimaryKey();
			if (propertyInfo == null)
				throw new Exception("This model doesn't have a [PrimaryKey] attribute");
			return Convert.ToInt64(propertyInfo.GetValue(aModel));
		}

		public bool Update(object obj, bool aEnsure = true) {
			if (obj==null)
				throw new ArgumentNullException();
			lock (connection) {
				var rowsAffected = connection.Update(obj);
				var success = rowsAffected > 0;
				if (!success && aEnsure) {
					var id = GetPrimaryKey(obj);
					if (id == 0)
						throw new Exception("Cannot update with primary key == 0");
					throw new Exception("Record not found to update " + obj.GetType().Name + " " + id.ToString());
				}

				return success;
			}
		}

		public void Insert(object obj) {
			if (obj==null)
				throw new ArgumentNullException();
			lock (connection) {
				var rowsAffected = connection.Insert(obj);
				if (rowsAffected==0) {
					var id = GetPrimaryKey(obj);
					throw new Exception("Record failed to insert " + obj.GetType().Name + " " + id.ToString());
				}
			}
		}

		public void Upsert(object obj)
		{
			lock (connection) {
				var success = Update(obj, false);
				if (!success)
					Insert(obj);	// check this when primary key already set
			}
		}

		public Task<CascadeStoreResponse<M>> Read<M>(string aResourceId) where M : class, ICascadeModel, new()
		{
			return Read<M>(CascadeUtils.LongId(aResourceId));
		}

		public async Task<CascadeStoreResponse<M>> Read<M>(long aResourceId) where M : class, ICascadeModel, new()
		{
			CascadeUtils.EnsureIsResourceId(aResourceId);
			var result = new CascadeStoreResponse<M>();
			result.value = FindOne<M>(aResourceId) as M;
			result.connected = true;
			result.present = result.value != null;
			return result;
		}

		public async Task<CascadeStoreResponse<List<M>>> ReadAll<M>() where M : class, ICascadeModel, new()
		{
			var result = new CascadeStoreResponse<List<M>>();
			result.value = FindMany<M>().ToList<M>();
			result.connected = true;
			result.present = result.value != null;
			return result;
		}

		public async Task<CascadeStoreResponse<M>> Write<M>(M value) where M : class, ICascadeModel, new()
		{
			var result = new CascadeStoreResponse<M>();
			Upsert(value);
			long new_id = GetPrimaryKey(value);
			CascadeUtils.EnsureIsResourceId(new_id);			
			result.value = new_id==0 ? null : FindOne<M>(new_id) as M;
			result.connected = true;
			result.present = result.value != null;
			return result;
		}

		public Task<CascadeStoreResponse<M>> Destroy<M>(string aResourceId) where M : class, ICascadeModel, new()
		{
			return Destroy<M>(CascadeUtils.LongId(aResourceId));
		}

		public async Task<CascadeStoreResponse<M>> Destroy<M>(long aResourceId) where M : class, ICascadeModel
		{
			var result = new CascadeStoreResponse<M>();
			var response = connection.Delete<M>(aResourceId);
			result.value = default(M);
			result.connected = true;
			result.present = response > 0; // was present
			return result;
		}

		public async Task<CascadeStoreResponse<M>> DestroyExcept<M>(IEnumerable<string> aResourceIds) where M : class, ICascadeModel, new()
		{
			var result = new CascadeStoreResponse<M>();

			var tn = connection.Table<M>().Table.TableName;
			int response;
			if (aResourceIds.Count() == 0)
				response = connection.Execute("delete from " + tn);
			else
				response =
					connection.Execute("delete from " + tn + " where id NOT IN (" +
					                                String.Join(",", aResourceIds.Select(i => i.ToString())) + ")");
			result.value = default(M);
			result.connected = true;
			result.present = response > 0; // was present
			return result;
		}
	}


	public class CascadeDataLayer {

		ICascadeStore localStore;
		ICascadeStore remoteStore;

		public CascadeDataLayer()
		{
			localStore = new SqliteNetStore();
			remoteStore = new RestServerStore();
		}

		public Task<M> Read<M>(long aResourceId, bool aFresh = true, bool aFallback = true) where M : class, ICascadeModel, new()
		{
			return Read<M>(aResourceId.ToString(),aFresh,aFallback);
		}

		public async Task<M> Read<M>(string aResourceId, bool aFresh = true, bool aFallback = true)
			where M : class, ICascadeModel, new()
		{
			CascadeUtils.EnsureIsResourceId(aResourceId);
			CascadeStoreResponse<M> localResponse = null;
			CascadeStoreResponse<M> remoteResponse = null;
			M result = default(M);
			//M remote = null;
			//M local = null;
			//bool remoteConnected = false;
			//bool remotePresent = false;
			//bool localPresent = false;

			if (aFresh)
			{
				try
				{
					remoteResponse = await remoteStore.Read<M>(aResourceId);
				}
				catch (Exception e)
				{
					Log.Debug("Exception: " + e.Message);
					if (!aFallback)
						throw;
				}

				if (remoteResponse!=null && remoteResponse.connected)
				{
					if (remoteResponse.present)
					{
						localStore.Write<M>(remoteResponse.value);
						result = remoteResponse.value;
					}
					else
					{
						localStore.Destroy<M>(aResourceId);
						result = default(M);
					}
				}
				else
				{
					if (aFallback)
					{
						localResponse = await localStore.Read<M>(aResourceId);
						result = localResponse.value;
					}
					else
					{
						result = default(M); // remoteResponse.value;
					}
				}
			}
			else
			{

				try
				{
					localResponse = await localStore.Read<M>(aResourceId);
				}
				catch (Exception e)
				{
					Log.Debug("Exception: " + e.Message);
					if (!aFallback)
						throw;
				}

				if (localResponse!=null && localResponse.present)
				{
					result = localResponse.value;
				}
				else
				{
					if (aFallback)
					{
						remoteResponse = await remoteStore.Read<M>(aResourceId);
						result = remoteResponse.value;
					}
					else
					{
						result = default(M); // remoteResponse.value;
					}
				}
			}
			return result;
		}

		public async Task<List<M>> ReadAll<M>(bool aFresh = true, bool aFallback = true, bool aExclusive = false)
			where M : class,
			ICascadeModel, new()
		{
			CascadeStoreResponse<List<M>> localResponse = null;
			CascadeStoreResponse<List<M>> remoteResponse = null;

			List<M> result = null;

			if (aFresh)
			{
				try
				{
					remoteResponse = await remoteStore.ReadAll<M>();
				}
				catch (Exception e)
				{
					Log.Debug("Exception: " + e.Message);
					if (!aFallback)
						throw;
				}

				if (remoteResponse.connected)
				{
					if (remoteResponse.present)
					{
						var items = remoteResponse.value;
						foreach (var r in items)
						{
							await localStore.Write<M>(r);
						}
						if (aExclusive)
						{
							IEnumerable<string> ids = items.Select(i => i.GetResourceId()).ToArray();
							await localStore.DestroyExcept<M>(ids);
						}
					}
					result = remoteResponse.value ?? new List<M>();
				}
				else
				{
					if (aFallback)
					{
						localResponse = await localStore.ReadAll<M>();
						result = localResponse.value;
					}
					else
					{
						result = new List<M>();
					}
				}
			}
			else
			{

				try
				{
					localResponse = await localStore.ReadAll<M>();
				}
				catch (Exception e)
				{
					Log.Debug("Exception: " + e.Message);
					if (!aFallback)
						throw;
				}

				if (localResponse.present)
				{
					result = localResponse.value;
				}
				else
				{
					if (aFallback)
					{
						remoteResponse = await remoteStore.ReadAll<M>();
						result = remoteResponse.value;
					}
					else
					{
						result = null; // remoteResponse.value;
					}
				}
			}
			return result;
		}

		public async Task<M> Write<M>(M value) where M : class, ICascadeModel, new()
		{
			M result = default(M);
			CascadeStoreResponse<M> localResponse = null;
			CascadeStoreResponse<M> remoteResponse = null;

			remoteResponse = await remoteStore.Write<M>(value);
			if (remoteResponse.connected) {
				if (remoteResponse.present) {
					localStore.Write<M>(remoteResponse.value);
					result = remoteResponse.value;
				} else {
					localStore.Destroy<M>(value.GetResourceId());
					result = null;
				}
			} else {
				throw new HttpService.NoNetworkException("Failed to reach remote store");
			}
			return result;
		}
	}
}