using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace Buzzware.Cascade {
	public class ModelClassCache<Model, IdType> : IModelClassCache 
		where Model : class {
		private readonly ConcurrentDictionary<IdType, Tuple<Model, long>> models = new ConcurrentDictionary<IdType, Tuple<Model, long>>();
		private readonly ConcurrentDictionary<string, Tuple<IEnumerable, long>> collections = new ConcurrentDictionary<string, Tuple<IEnumerable, long>>();

		public CascadeDataLayer Cascade { get; set; }

		public ModelClassCache() {
			
		}
		
		public async Task<OpResponse> Fetch(RequestOp requestOp) {
			if (requestOp.Type != typeof(Model))
				throw new Exception("requestOp.Type != typeof(Model)");
			switch (requestOp.Verb) {
				case RequestVerb.Get:
					var id = (IdType?)CascadeTypeUtils.ConvertTo(typeof(IdType), requestOp.Id); //  ((IdType)requestOp.Id)!;
					if (id == null)
						throw new Exception("Unable to get right value for Id");
					
					models.TryGetValue(id, out var modelEntry);
					
					if (
						modelEntry != null && 
						(requestOp.FreshnessSeconds>=0) && 
						(requestOp.FreshnessSeconds==CascadeDataLayer.FRESHNESS_ANY || ((Cascade.NowMs-modelEntry.Item2) <= requestOp.FreshnessSeconds*1000))
					) {
						return new OpResponse(
							requestOp,
							Cascade.NowMs,
							connected: true,
							exists: true,
							result: modelEntry.Item1,
							arrivedAtMs: modelEntry.Item2
						);
					} else {
						return OpResponse.None(requestOp,Cascade.NowMs, this.GetType().Name);
					}
					break;
				case RequestVerb.Query:
				case RequestVerb.GetCollection:
					
					collections.TryGetValue(requestOp.Key!, out var collEntry);
					
					if (
						collEntry != null && 
						(requestOp.FreshnessSeconds>=0) && 
						(requestOp.FreshnessSeconds==CascadeDataLayer.FRESHNESS_ANY || ((Cascade.NowMs-collEntry.Item2) <= requestOp.FreshnessSeconds*1000))
					) {
						return new OpResponse(
							requestOp,
							Cascade.NowMs,
							connected: true,
							exists: true,
							result: collections[requestOp.Key!].Item1,
							arrivedAtMs: collections[requestOp.Key!].Item2
						);
					} else {
						return OpResponse.None(requestOp,Cascade.NowMs, this.GetType().Name);
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

		public async Task Store(object id, object model, long arrivedAt) {
			var idTyped = (IdType?) CascadeTypeUtils.ConvertTo(typeof(IdType), id);
			if (idTyped == null)
				throw new Exception("Bad id");
			models[idTyped] = new Tuple<Model, long>((Model)model, arrivedAt);
			// return Store(idTyped, (Model)model, arrivedAt);
		}

		// public async Task Store(IdType id, Model model, long arrivedAt) {
		// 	models[id] = new Tuple<Model, long>(model, arrivedAt);
		// }
		
		public async Task StoreCollection(string key, IEnumerable ids, long arrivedAt) {
			collections[key] = new Tuple<IEnumerable, long>(ids, arrivedAt);
		}

		public Task Remove(object id) {
			return Remove((IdType)id);
		}

		public async Task Remove(IdType id) {
			models.TryRemove(id, out var value);
		}
		
		public async Task ClearAll(bool exceptHeld, DateTime? olderThan = null) {
			if (exceptHeld || olderThan!=null) {
				// models
				var heldModelIds = Cascade.ListHeldIds<Model>().ToArray();
				var idsToRemove = models.Where(kv => {
						//var id = CascadeTypeUtils.GetCascadeId(kv.Value.Item1);
						var id = kv.Key;
						var contains = heldModelIds.Contains(id);
						return !contains;
					})
					.Select(kv => kv.Key)
					.ToArray();
				foreach (var id in idsToRemove.Reverse())
					models.TryRemove(id, out var v);
				
				// collections
				var heldCollectionNames = Cascade.ListHeldCollections(typeof(Model)).ToArray();
				var namesToRemove = collections.Where(kv => !heldCollectionNames.Contains(kv.Key))
					.Select(kv => kv.Key)
					.ToArray();
				foreach (var name in namesToRemove)
					collections.TryRemove(name, out var v);
			} else {
				models.Clear();
				collections.Clear();
			}
		}
	}
}
