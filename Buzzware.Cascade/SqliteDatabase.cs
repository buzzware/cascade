// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Threading.Tasks;
// using Serilog;
// using SQLite;
//
// namespace Buzzware.Cascade {
// 	public class SqliteDatabase {
// 		
// 		// use withConnection instead of this
// 		// if you use this directly then locking is your responsibility
// 		public SQLiteAsyncConnection Connection { get; }
//
// 		private readonly bool _debug;
// 		
// 		public SqliteDatabase(
// 			SQLite.SQLiteAsyncConnection connection,
// 			bool debug = false
// 		) {
// 			Log.Debug("Setup HistoryDatabase with file " + connection.DatabasePath);
// 			Connection = connection;
// 			_debug = debug;
//
// 			if (_debug) {
// 				Connection.Tracer = (s) => Log.Debug(s);
// 				Connection.Trace = true;
// 				Connection.TimeExecution = true;
// 			}
// 		}
//
//
// 		public async Task<T> Create<T>(T model) {
// 			await Connection.InsertAsync(model);
// 			return model;
// 		}
//
// 		public async Task<T?> Get<T>(object id) where T : new() {
// 			return await Connection.FindAsync<T>(id);
// 		}
// 		// public async Task<T?> Get<T>(int id) where T : new() {
// 		// 	return await Connection.FindAsync<T>(id);
// 		// }
// 		// public async Task<T?> Get<T>(String id) where T : new() {
// 		// 	return await Connection.FindAsync<T>(id);
// 		// }
// 		
// 		public async Task<bool> Update(object obj, bool aEnsure = true) {
// 			if (obj==null)
// 				throw new ArgumentNullException();
// 			//lock (_connection) {
// 			int rowsAffected = 0;
// 			try
// 			{
// 				rowsAffected = await Connection.UpdateAsync(obj);
// 				Log.Debug($"HistoryDatabase: Updated {rowsAffected} rows");
// 			} catch(Exception e) {
// 				Log.Error(e.Message);
// 				throw e;
// 			}
// 			var success = rowsAffected > 0;
// 			if (!success && aEnsure)
// 				throw new Exception("Record not found to update " + obj.GetType().Name); //+ " " + GetPrimaryKey(obj).ToString());
// 			return success;
// 			//}
// 		}
// 		
// 		public async Task Upsert(object obj) {
// 			var success = await Update(obj, false);
// 			if (!success)
// 				await Connection.InsertAsync(obj);
// 		}
// 		
// 		
// 		// public async Task<T> Update<T>(object id, Dictionary<String, object> changes) {
// 		// 	
// 		// 	
// 		//   
// 		// 	//var results = Connection.UpdateAsync(changes);
// 		// 	var results = Connection.ExecuteAsync("UPDATE ? SET Price = ? Where Id = ?", 1000000, 2);
// 		// 	
// 		// 	
// 		// }
//
// 		public async Task Delete<Model>(object id) {
// 			await Connection.DeleteAsync<Model>(id);
// 		}
//
// 		public async Task Delete(object model) {
// 			await Connection.DeleteAsync(model);
// 		}
// 		
// 		public async Task<bool> Exists<Model>(object id) {
// 			bool exists = false;
// 			try
// 			{
// 				exists = await Connection.ExecuteScalarAsync<bool>("SELECT EXISTS(SELECT 1 FROM " + TableName<Model>() + " WHERE ID=?)", id);
// 			}
// 			catch (Exception ex)
// 			{
// 				exists = false;
// 			}
// 			return exists;
// 		}
//
// 		public string TableName<Model>() {
// 			return Connection.TableMappings.First(m => m.MappedType == typeof(Model)).TableName;
// 		}
//
// 		//public Query<T>("select * from packets where kin=0 and journey_id=? order by timems", aJourney.id);
//
// 		// public Task<List<T>> Query<T>(string query, params object[] args) where T : class, new()
// 		// {
// 		// 	lock (_connection) {
// 		// 		return _connection.QueryAsync<T>(query, args);
// 		// 	}
// 		// }
// 		//
// 		// public Task<int> Execute(string query, params object[] args)
// 		// {
// 		// 	lock (_connection) {
// 		// 		return _connection.ExecuteAsync(query, args);
// 		// 	}
// 		// }
// 		//
// 		// // public long GetPrimaryKey(object aModel) {
// 		// // 	var propertyInfo = aModel.GetType().GetPrimaryKey();
// 		// // 	if (propertyInfo == null)
// 		// // 		throw new Exception("This model doesn't have a [PrimaryKey] attribute");
// 		// // 	return Convert.ToInt64(propertyInfo.GetValue(aModel));
// 		// // }
// 		//
// 		// public async Task<bool> Update(object obj, bool aEnsure = true) {
// 		// 	if (obj==null)
// 		// 		throw new ArgumentNullException();
// 		// 	lock (_connection) {
// 		// 		int rowsAffected = 0;
// 		// 		try
// 		// 		{
// 		// 			rowsAffected = await _connection.UpdateAsync(obj);
// 		// 			Log.Debug($"HistoryDatabase: Updated {rowsAffected} rows");
// 		// 		} catch(Exception e) {
// 		// 			Log.Debug(e.Message);
// 		// 			throw e;
// 		// 		}
// 		// 		var success = rowsAffected > 0;
// 		// 		if (!success && aEnsure)
// 		// 			throw new Exception("Record not found to update " + obj.GetType().Name); //+ " " + GetPrimaryKey(obj).ToString());
// 		// 		return success;
// 		// 	}
// 		// }
// 		//
// 		// public void Insert(object obj) {
// 		// 	if (obj==null)
// 		// 		throw new ArgumentNullException();
// 		// 	lock (_connection) {
// 		// 		int rowsAffected = 0;
// 		// 		// try
// 		// 		// {
// 		// 			rowsAffected = _connection.Insert(obj);
// 		// 			Log.Debug($"HistoryDatabase: Inserted {rowsAffected} rows");
// 		// 		// }
// 		// 		// catch (SQLite.SQLiteException e)
// 		// 		// {
// 		// 		// 	FreeCommon.reportException(e,new Dictionary<string,string>() {
// 		// 		// 		{ "model", FreeCommon.Json.Serialize(obj as ModelBase) },
// 		// 		// 		{ "InnerException", e.InnerException?.ToString()},
// 		// 		// 		{ "Source", e.Source },
// 		// 		// 		{	"HelpLink",e.HelpLink }
// 		// 		// 	});
// 		// 		// }
// 		//
// 		// 		if (rowsAffected == 0)
// 		// 			throw new Exception("Record failed to insert " + obj.GetType().Name); //+ " " + GetPrimaryKey(obj).ToString());
// 		// 	}
// 		// }
// 		//
// 		// public void Upsert(object obj)
// 		// {
// 		// 	lock (_connection) {
// 		// 		var success = Update(obj, false);
// 		// 		if (!success)
// 		// 			Insert(obj);
// 		// 	}
// 		// }
// 		//
// 		// // public string tableName<T>()
// 		// // {
// 		// // 	var n = typeof(T).Name;
// 		// // 	switch (n) {
// 		// // 		case "JourneyLeg":
// 		// // 			return "journey_legs";
// 		// // 	}
// 		// // 	return n.ToLower() + "s";
// 		// // }
//
// 		// public T WithConnection<T>(Func<SQLiteConnection,T> func) {
// 		// 	lock (Connection) {
// 		// 		return func(Connection);
// 		// 	}
// 		// }
// 		//
// 		// public void WithConnection(Action<SQLiteConnection> func) {
// 		// 	lock (Connection) {
// 		// 		func(Connection);
// 		// 	}
// 		// }
//
// 		public Task Close() {
// 			// if (Connection == null)
// 			// 	return;
// 			// lock (Connection) {
// 				return Connection.CloseAsync();
// 			// }
// 		}
//
// 		public async Task DropAllTables() {
// 			foreach (var tm in Connection.TableMappings) {
// 				await Connection.DropTableAsync(tm);
// 			}
// 		}
// 	}
// }
