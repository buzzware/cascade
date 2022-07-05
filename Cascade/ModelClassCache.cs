using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Cascade {
	public class ModelClassCache<Model, IdType> : IModelClassCache 
		where Model : class {
		private readonly Dictionary<IdType, Tuple<Model, long>> models = new Dictionary<IdType, Tuple<Model, long>>();

		public CascadeDataLayer Cascade { get; set; }

		public ModelClassCache() {
			
		}
		
		public async Task<OpResponse> Fetch(RequestOp requestOp) {
			if (requestOp.Type != typeof(Model))
				throw new Exception("requestOp.Type != typeof(Model)");
			var id = (IdType) CascadeUtils.ConvertTo(typeof(IdType), requestOp.Id); //  ((IdType)requestOp.Id)!;
			if (id == null)
				throw new Exception("Unable to get right value for Id");

			if (models.ContainsKey(id)) {
				return new OpResponse(
					requestOp,
					Cascade.NowMs,
					connected: true,
					present: true,
					result: models[id].Item1,
					arrivedAtMs: models[id].Item2
				);
			}
			else {
				return new OpResponse(
					requestOp,
					Cascade.NowMs,
					connected: true,
					present: false,
					result: null,
					arrivedAtMs: null
				);
			}
			
		}

		public Task Store(object id, object model, long arrivedAt) {
			return Store((IdType)id, (Model)model, arrivedAt);
		}

		public Task Remove(object id) {
			return Remove((IdType)id);
		}

		public async Task Clear() {
			models.Clear();
		}

		public async Task Store(IdType id, Model model, long arrivedAt) {
			models[id] = new Tuple<Model, long>(model, arrivedAt);
		}

		public async Task Remove(IdType id) {
			models.Remove(id);
		}

		public async Task Store(OpResponse opResponse) {
			if (!opResponse.Connected)
				throw new Exception("Don't attempt to store responses from a disconnected store");
			IdType id = (IdType) CascadeUtils.ConvertTo(typeof(IdType), opResponse.RequestOp.Id);
			if (id == null)
				throw new Exception("Unable to get right value for Id");
			long arrivedAt = opResponse.ArrivedAtMs ?? Cascade.NowMs;
			if (!opResponse.Present) {
				await Remove(id);
			} else {
				if (opResponse.Result is null)
					throw new Exception("When Present is true, Result cannot be null");
				Model model = (opResponse.Result as Model)!;
				await Store(id, model, arrivedAt);
			}
		}
	}
}
