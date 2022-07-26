using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using SQLite;
using Cascade;

namespace Cascade {

	public class CascadeModelMeta {
		[PrimaryKey]
		public string model_id { get; set; }
		public long arrived_at { get; set; }

		public static string GenerateId<Model>(object id=null) {
			return $"{typeof(Model).Name}__{id}";
		}
	}
	
	public class CascadeCollection {
		
		[PrimaryKey]
		public string model_key { get; set; }
		public string ids { get; set; }
		public long arrived_at { get; set; }

		public static string GenerateId<Model>(string key) /*where Model : new()*/ {
			return $"{typeof(Model).Name}__{key}";			
		}
		
		public IEnumerable objectIds {
			get {
				return CascadeTypeUtils.DecodeJsonArray(this.ids);
				// var jarray =  
				// // var jarray = JsonArray.Parse(this.ids).AsArray();
				// var first = jarray[0];
				// // var fv = first.Deserialize<object>(); //  AsValue().  GetValue<object>();
				// // var values = jarray.Select(i=>i.AsObject()).ToImmutableArray(); //.AsObject();
				// 	
				// Console.WriteLine("end");
				// return jarray;
			}
		}
	}
	
	
	public class SqliteClassCache<Model, IdType> : IModelClassCache where Model : new() {
		private readonly SqliteDatabase _database;

		public SqliteClassCache(SqliteDatabase database) {
			_database = database;
		}

		public CascadeDataLayer? Cascade { get; set; }
		
		public async Task<OpResponse> Fetch(RequestOp requestOp) {
			if (requestOp.Type != typeof(Model))
				throw new Exception("requestOp.Type != typeof(Model)");
			switch (requestOp.Verb) {
				case RequestVerb.Get:
					var id = (IdType?)CascadeTypeUtils.ConvertTo(typeof(IdType), requestOp.Id); //  ((IdType)requestOp.Id)!;
					if (id == null)
						throw new Exception("Unable to get right value for Id");

					if (await _database.Exists<Model>(id)) {
						var meta = await _database.Get<CascadeModelMeta>(CascadeModelMeta.GenerateId<Model>(id));
						return new OpResponse(
							requestOp,
							Cascade!.NowMs,
							connected: true,
							exists: true,
							result: await _database.Get<Model>(id),
							arrivedAtMs: meta?.arrived_at
						);
					}
					else {
						return new OpResponse(
							requestOp,
							Cascade!.NowMs,
							connected: true,
							exists: false,
							result: null,
							arrivedAtMs: null
						);
					}
					break;
				case RequestVerb.Query:
					var collection = await _database.Get<CascadeCollection>(CascadeCollection.GenerateId<Model>(requestOp.Key!));
					if (collection != null) {
						return new OpResponse(
							requestOp,
							Cascade!.NowMs,
							connected: true,
							exists: true,
							result: collection.objectIds,
							arrivedAtMs: collection.arrived_at
						);
					} else {
						return new OpResponse(
							requestOp,
							Cascade!.NowMs,
							connected: true,
							exists: false,
							result: null,
							arrivedAtMs: null
						);
					}
					break;
				default:
					throw new NotImplementedException($"Unsupported {requestOp.Verb}");
			}
		}

		public async Task Store(object id, object model, long arrivedAt) {
			await _database.Connection.InsertOrReplaceAsync(model);
			await _database.Connection.InsertOrReplaceAsync(new CascadeModelMeta() {
				model_id = CascadeModelMeta.GenerateId<Model>(id),
				arrived_at = arrivedAt
			});
		}

		public async Task StoreCollection(string key, IEnumerable ids, long arrivedAt) {
			await _database.Connection.InsertOrReplaceAsync(new CascadeCollection() {
				model_key = CascadeCollection.GenerateId<Model>(key),
				ids = JsonSerializer.Serialize(ids),
				arrived_at = arrivedAt
			});
		}

		public async Task Remove(object id) {
			await _database.Delete<Model>(id);
			await _database.Delete<CascadeModelMeta>(CascadeModelMeta.GenerateId<Model>(id));
		}

		public async Task Clear() {
			await _database.Connection.DropTableAsync<Model>();
			await _database.Connection.ExecuteAsync($"DELETE FROM {_database.TableName<CascadeModelMeta>()} WHERE model_id LIKE '{typeof(Model).Name}\\_\\_%' ESCAPE '\\'");
			await Setup();
		}

		public async Task Setup() {
			await _database.Connection.CreateTableAsync<Model>();
			await _database.Connection.CreateTableAsync<CascadeModelMeta>();
			await _database.Connection.CreateTableAsync<CascadeCollection>();
		}
	}
}

