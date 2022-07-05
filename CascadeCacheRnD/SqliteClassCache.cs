using System;
using System.Threading.Tasks;
using Cascade;
using SQLite;

namespace Test {

	public class CascadeModelMeta {
		[PrimaryKey]
		public string ModelId { get; set; }
		public long ArrivedAt { get; set; }

		public static string GenerateId<Model>(object id=null) {
			return $"{typeof(Model).Name}__{id}";
		}
	}
	
	public class SqliteClassCache<Model, IdType> : IModelClassCache where Model : new() {
		private readonly SqliteDatabase _database;

		public SqliteClassCache(SqliteDatabase database) {
			_database = database;
		}

		public CascadeDataLayer Cascade { get; set; }
		
		public async Task<OpResponse> Fetch(RequestOp requestOp) {
			if (requestOp.Type != typeof(Model))
				throw new Exception("requestOp.Type != typeof(Model)");
			var id = (IdType) CascadeUtils.ConvertTo(typeof(IdType), requestOp.Id); //  ((IdType)requestOp.Id)!;
			if (id == null)
				throw new Exception("Unable to get right value for Id");
			
			if (await _database.Exists<Model>(id)) {
				var meta = await _database.Get<CascadeModelMeta>(CascadeModelMeta.GenerateId<Model>(id));
				return new OpResponse(
					requestOp,
					Cascade.NowMs,
					connected: true,
					exists: true,
					result: await _database.Get<Model>(id),
					arrivedAtMs: meta?.ArrivedAt
				);
			}
			else {
				return new OpResponse(
					requestOp,
					Cascade.NowMs,
					connected: true,
					exists: false,
					result: null,
					arrivedAtMs: null
				);
			}
		}

		public async Task Store(object id, object model, long arrivedAt) {
			await _database.Connection.InsertOrReplaceAsync(model);
			await _database.Connection.InsertOrReplaceAsync(new CascadeModelMeta() {
				ModelId = CascadeModelMeta.GenerateId<Model>(id),
				ArrivedAt = arrivedAt
			});
		}

		public async Task Remove(object id) {
			await _database.Delete<Model>(id);
			await _database.Delete<CascadeModelMeta>(CascadeModelMeta.GenerateId<Model>(id));
		}

		public async Task Clear() {
			await _database.Connection.DropTableAsync<Model>();
			await _database.Connection.ExecuteAsync($"DELETE FROM {_database.TableName<CascadeModelMeta>()} WHERE ModelId LIKE '{typeof(Model).Name}\\_\\_%' ESCAPE '\\'");
			await Setup();
		}

		public async Task Setup() {
			await _database.Connection.CreateTableAsync<Model>();
			await _database.Connection.CreateTableAsync<CascadeModelMeta>();
		}
	}
}
