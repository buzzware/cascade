// using System.Threading.Tasks;
// using SQLite;
//
// namespace Buzzware.Buzzware.Cascade.Test {
// 	public class TestDatabase : SqliteDatabase {
// 		public TestDatabase(SQLiteAsyncConnection connection) : base(connection) {
// 		}
// 		
// 		public Task EnsureTablesExist() {
// 			return Connection.RunInTransactionAsync(conn => {
// 				conn.CreateTable<Parent>();
// 			});
// 		}
// 		
// 		public Task DeleteAllTables()
// 		{
// 			return Connection.RunInTransactionAsync(async conn => {
// 				await DropAllTables();
// 			});
// 		}
//
// 		public async Task Reset()
// 		{
// 			await DeleteAllTables();
// 			await EnsureTablesExist();
// 		}
// 		
// 		
// 		// public T load<T>(long id) where T : class, new()
// 		// {
// 		// 	lock (_connection) {
// 		// 		var t = tableName<T>();
// 		// 		var pk = typeof(T).GetPrimaryKey().GetColumnName();
// 		// 		return _connection.FindWithQuery<T>("select * from " + t + " where "+pk+" = ?", id);
// 		// 	}
// 		// }
//
//
//
// 		// public string exportDatabase()
// 		// {
// 		// 	var outName = platform.downloadPath(FreeCommon.HISTORYDB_FILENAME.Replace(".db", "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".db"));
// 		// 	// copy platform.combineWritablePath(IntelMain.HISTORYDB_FILENAME) to /sdcard/Download/
// 		// 	var sourceName = platform.combineWritablePath(FreeCommon.HISTORYDB_FILENAME);
// 		// 	if (platform.fileExists(sourceName)) {
// 		// 		platform.copyFile(sourceName, outName);
// 		// 		return outName;
// 		// 	}
// 		// 	return null;
// 		// }
// 		//
// 		// public bool importDatabase()
// 		// {
// 		// 	var sourceName = platform.downloadPath(FreeCommon.HISTORYDB_FILENAME);
// 		// 	var destName = platform.combineWritablePath(FreeCommon.HISTORYDB_FILENAME);
// 		// 	if (platform.fileExists(sourceName)) {
// 		// 		platform.copyFile(sourceName, destName);
// 		// 		return true;
// 		// 	}
// 		// 	return false;
// 		// }
//
//
//
// 	}
// }
