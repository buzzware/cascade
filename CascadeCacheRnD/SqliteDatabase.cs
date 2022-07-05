using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Serilog;
using SQLite;

namespace Test {


	public class TestDatabase : SqliteDatabase {
		public TestDatabase(SQLiteAsyncConnection connection) : base(connection) {
		}
		
		public Task EnsureTablesExist() {
			return Connection.RunInTransactionAsync(conn => {
				conn.CreateTable<Thing>();
			});
		}
		
		public Task DeleteAllTables()
		{
			return Connection.RunInTransactionAsync(conn => {
				conn.DropTable<Thing>();
			});
		}

		public async Task Reset()
		{
			await DeleteAllTables();
			await EnsureTablesExist();
		}
		
		
		// public T load<T>(long id) where T : class, new()
		// {
		// 	lock (_connection) {
		// 		var t = tableName<T>();
		// 		var pk = typeof(T).GetPrimaryKey().GetColumnName();
		// 		return _connection.FindWithQuery<T>("select * from " + t + " where "+pk+" = ?", id);
		// 	}
		// }



		// public string exportDatabase()
		// {
		// 	var outName = platform.downloadPath(FreeCommon.HISTORYDB_FILENAME.Replace(".db", "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".db"));
		// 	// copy platform.combineWritablePath(IntelMain.HISTORYDB_FILENAME) to /sdcard/Download/
		// 	var sourceName = platform.combineWritablePath(FreeCommon.HISTORYDB_FILENAME);
		// 	if (platform.fileExists(sourceName)) {
		// 		platform.copyFile(sourceName, outName);
		// 		return outName;
		// 	}
		// 	return null;
		// }
		//
		// public bool importDatabase()
		// {
		// 	var sourceName = platform.downloadPath(FreeCommon.HISTORYDB_FILENAME);
		// 	var destName = platform.combineWritablePath(FreeCommon.HISTORYDB_FILENAME);
		// 	if (platform.fileExists(sourceName)) {
		// 		platform.copyFile(sourceName, destName);
		// 		return true;
		// 	}
		// 	return false;
		// }


		public async Task<T> Create<T>(T model) {
			await Connection.InsertAsync(model);
			return model;
		}

		public async Task<T> Get<T>(long id) where T : new() {
			return await Connection.GetAsync<T>(id);
		}
		public async Task<T> Get<T>(int id) where T : new() {
			return await Connection.GetAsync<T>(id);
		}
		public async Task<T> Get<T>(String id) where T : new() {
			return await Connection.GetAsync<T>(id);
		}
	}
	
	
	public class SqliteDatabase {
		
		// use withConnection instead of this
		// if you use this directly then locking is your responsibility
		public SQLiteAsyncConnection Connection { get; }

		private readonly bool _debug;
		
		public SqliteDatabase(
			SQLite.SQLiteAsyncConnection connection,
			bool debug = false
		) {
			Log.Debug("Setup HistoryDatabase with file " + connection.DatabasePath);
			Connection = connection;
			_debug = debug;

			if (_debug) {
				Connection.Tracer = (s) => Log.Debug(s);
				Connection.Trace = true;
				Connection.TimeExecution = true;
			}
		}



		//public Query<T>("select * from packets where kin=0 and journey_id=? order by timems", aJourney.id);

		// public Task<List<T>> Query<T>(string query, params object[] args) where T : class, new()
		// {
		// 	lock (_connection) {
		// 		return _connection.QueryAsync<T>(query, args);
		// 	}
		// }
		//
		// public Task<int> Execute(string query, params object[] args)
		// {
		// 	lock (_connection) {
		// 		return _connection.ExecuteAsync(query, args);
		// 	}
		// }
		//
		// // public long GetPrimaryKey(object aModel) {
		// // 	var propertyInfo = aModel.GetType().GetPrimaryKey();
		// // 	if (propertyInfo == null)
		// // 		throw new Exception("This model doesn't have a [PrimaryKey] attribute");
		// // 	return Convert.ToInt64(propertyInfo.GetValue(aModel));
		// // }
		//
		// public async Task<bool> Update(object obj, bool aEnsure = true) {
		// 	if (obj==null)
		// 		throw new ArgumentNullException();
		// 	lock (_connection) {
		// 		int rowsAffected = 0;
		// 		try
		// 		{
		// 			rowsAffected = await _connection.UpdateAsync(obj);
		// 			Log.Debug($"HistoryDatabase: Updated {rowsAffected} rows");
		// 		} catch(Exception e) {
		// 			Log.Debug(e.Message);
		// 			throw e;
		// 		}
		// 		var success = rowsAffected > 0;
		// 		if (!success && aEnsure)
		// 			throw new Exception("Record not found to update " + obj.GetType().Name); //+ " " + GetPrimaryKey(obj).ToString());
		// 		return success;
		// 	}
		// }
		//
		// public void Insert(object obj) {
		// 	if (obj==null)
		// 		throw new ArgumentNullException();
		// 	lock (_connection) {
		// 		int rowsAffected = 0;
		// 		// try
		// 		// {
		// 			rowsAffected = _connection.Insert(obj);
		// 			Log.Debug($"HistoryDatabase: Inserted {rowsAffected} rows");
		// 		// }
		// 		// catch (SQLite.SQLiteException e)
		// 		// {
		// 		// 	FreeCommon.reportException(e,new Dictionary<string,string>() {
		// 		// 		{ "model", FreeCommon.Json.Serialize(obj as ModelBase) },
		// 		// 		{ "InnerException", e.InnerException?.ToString()},
		// 		// 		{ "Source", e.Source },
		// 		// 		{	"HelpLink",e.HelpLink }
		// 		// 	});
		// 		// }
		//
		// 		if (rowsAffected == 0)
		// 			throw new Exception("Record failed to insert " + obj.GetType().Name); //+ " " + GetPrimaryKey(obj).ToString());
		// 	}
		// }
		//
		// public void Upsert(object obj)
		// {
		// 	lock (_connection) {
		// 		var success = Update(obj, false);
		// 		if (!success)
		// 			Insert(obj);
		// 	}
		// }
		//
		// // public string tableName<T>()
		// // {
		// // 	var n = typeof(T).Name;
		// // 	switch (n) {
		// // 		case "JourneyLeg":
		// // 			return "journey_legs";
		// // 	}
		// // 	return n.ToLower() + "s";
		// // }

		// public T WithConnection<T>(Func<SQLiteConnection,T> func) {
		// 	lock (Connection) {
		// 		return func(Connection);
		// 	}
		// }
		//
		// public void WithConnection(Action<SQLiteConnection> func) {
		// 	lock (Connection) {
		// 		func(Connection);
		// 	}
		// }

		public Task Close() {
			// if (Connection == null)
			// 	return;
			// lock (Connection) {
				return Connection.CloseAsync();
			// }
		}
	}
}
