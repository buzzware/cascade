using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Cascade {
	public class ModelClassCache<Model, IdType> : IModelClassCache 
		where Model : class {
		private readonly Dictionary<IdType, Tuple<Model, long>> models = new Dictionary<IdType, Tuple<Model, long>>();
		private readonly Dictionary<string, Tuple<object[], long>> collections = new Dictionary<string, Tuple<object[], long>>();

		public CascadeDataLayer Cascade { get; set; }

		public ModelClassCache() {
			
		}
		
		public async Task<OpResponse> Fetch(RequestOp requestOp) {
			if (requestOp.Type != typeof(Model))
				throw new Exception("requestOp.Type != typeof(Model)");
			switch (requestOp.Verb) {
				case RequestVerb.Get:
					var id = (IdType?) CascadeUtils.ConvertTo(typeof(IdType), requestOp.Id);  //  ((IdType)requestOp.Id)!;
					if (id == null)
						throw new Exception("Unable to get right value for Id");

					if (models.ContainsKey(id)) {
						return new OpResponse(
							requestOp,
							Cascade.NowMs,
							connected: true,
							exists: true,
							result: models[id].Item1,
							arrivedAtMs: models[id].Item2
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
					break;
				case RequestVerb.Query:
					if (collections.ContainsKey(requestOp.Key!)) {
						return new OpResponse(
							requestOp,
							Cascade.NowMs,
							connected: true,
							exists: true,
							result: collections[requestOp.Key!].Item1,
							arrivedAtMs: collections[requestOp.Key!].Item2
						);
					} else {
						return new OpResponse(
							requestOp,
							Cascade.NowMs,
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

		public async Task<Model?> Fetch<Model>(object id, int freshnessSeconds = 0) where Model : class {
			var response = await Fetch(RequestOp.GetOp<Model>(id, Cascade.NowMs, freshnessSeconds: freshnessSeconds));
			return response.Result as Model;
		}

		public Task Store(object id, object model, long arrivedAt) {
			var idTyped = (IdType?) CascadeUtils.ConvertTo(typeof(IdType), id);
			if (idTyped == null)
				throw new Exception("Bad id");
			return Store(idTyped, (Model)model, arrivedAt);
		}

		public async Task Store(IdType id, Model model, long arrivedAt) {
			models[id] = new Tuple<Model, long>(model, arrivedAt);
		}
		
		public async Task StoreCollection(string key, object[] ids, long arrivedAt) {
			collections[key] = new Tuple<object[], long>(ids, arrivedAt);
		}

		public Task Remove(object id) {
			return Remove((IdType)id);
		}

		public async Task Remove(IdType id) {
			models.Remove(id);
		}
		
		public async Task Clear() {
			models.Clear();
		}
	}
}
