//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading.Tasks;
//
//namespace Cascade {
//	public class SqliteNetStore : ICascadeStore
//	{
//
//		public string tableName<T>() {
//			var n = typeof(T).Name;
//			switch (n) {
//				case "Person":
//					return "people";
//				case "Address":
//					return "addresses";
//			}
//			return n.ToLower() + "s";
//		}
//
//		public List<T> FindMany<T>(string query = null, params object[] args) where T : class, new()
//		{
//			lock (connection) {
//				if (query == null) {
//					var t = tableName<T>();
//					return connection.Query<T>("select * from " + t);
//				} else {
//					return connection.Query<T>(query, args);
//				}
//			}
//		}
//
//		public T FindOne<T>(string query, params object[] args) where T : class, new()
//		{
//			lock (connection) {
//				return connection.FindWithQuery<T>(query, args);
//			}
//		}
//
//		public T FindOne<T>(long aId) where T : class, new()
//		{
//			lock (connection) {
//				return connection.Find<T>(aId);
//			}
//		}
//
//		public long GetPrimaryKey(object aModel) {
//			var propertyInfo = aModel.GetType().GetPrimaryKey();
//			if (propertyInfo == null)
//				throw new Exception("This model doesn't have a [PrimaryKey] attribute");
//			return Convert.ToInt64(propertyInfo.GetValue(aModel));
//		}
//
//		public bool Update(object obj, bool aEnsure = true) {
//			if (obj==null)
//				throw new ArgumentNullException();
//			lock (connection) {
//				var rowsAffected = connection.Update(obj);
//				var success = rowsAffected > 0;
//				if (!success && aEnsure) {
//					var id = GetPrimaryKey(obj);
//					if (id == 0)
//						throw new Exception("Cannot update with primary key == 0");
//					throw new Exception("Record not found to update " + obj.GetType().Name + " " + id.ToString());
//				}
//
//				return success;
//			}
//		}
//
//		public void Insert(object obj) {
//			if (obj==null)
//				throw new ArgumentNullException();
//			lock (connection) {
//				var rowsAffected = connection.Insert(obj);
//				if (rowsAffected==0) {
//					var id = GetPrimaryKey(obj);
//					throw new Exception("Record failed to insert " + obj.GetType().Name + " " + id.ToString());
//				}
//			}
//		}
//
//		public void Upsert(object obj)
//		{
//			lock (connection) {
//				var success = Update(obj, false);
//				if (!success)
//					Insert(obj);	// check this when primary key already set
//			}
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
//			result.value = FindOne<M>(aResourceId) as M;
//			result.Connected = true;
//			result.present = result.value != null;
//			return result;
//		}
//
//		public async Task<OpResponse<List<M>>> ReadAll<M>() where M : class, ICascadeModel, new()
//		{
//			var result = new OpResponse<List<M>>();
//			result.value = FindMany<M>().ToList<M>();
//			result.Connected = true;
//			result.present = result.value != null;
//			return result;
//		}
//
//		public async Task<OpResponse<M>> Write<M>(M value) where M : class, ICascadeModel, new()
//		{
//			var result = new OpResponse<M>();
//			Upsert(value);
//			long new_id = GetPrimaryKey(value);
//			CascadeUtils.EnsureIsResourceId(new_id);			
//			result.value = new_id==0 ? null : FindOne<M>(new_id) as M;
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
//			var result = new OpResponse<M>();
//			var response = connection.Delete<M>(aResourceId);
//			result.value = default(M);
//			result.Connected = true;
//			result.present = response > 0; // was present
//			return result;
//		}
//
//		public async Task<OpResponse<M>> DestroyExcept<M>(IEnumerable<string> aResourceIds) where M : class, ICascadeModel, new()
//		{
//			var result = new OpResponse<M>();
//
//			var tn = connection.Table<M>().Table.TableName;
//			int response;
//			if (aResourceIds.Count() == 0)
//				response = connection.Execute("delete from " + tn);
//			else
//				response =
//					connection.Execute("delete from " + tn + " where id NOT IN (" +
//					                   String.Join(",", aResourceIds.Select(i => i.ToString())) + ")");
//			result.value = default(M);
//			result.Connected = true;
//			result.present = response > 0; // was present
//			return result;
//		}
//	}
//}