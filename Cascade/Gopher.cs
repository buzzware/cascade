using System.Linq;
using System.Threading.Tasks;

namespace Cascade {
	public class Gopher {
		private CascadeDataLayer cascadeDataLayer;
		private RequestOp requestOp;

		public Gopher(CascadeDataLayer aCascadeDataLayer, RequestOp aRequestOp) {
			cascadeDataLayer = aCascadeDataLayer;
			requestOp = aRequestOp;
		}

		public async Task<OpResponse> Run() {
			OpResponse opResponse;
			if (RequestOp.IsWriteVerb(requestOp.Verb) || requestOp.Fresh) {
				opResponse = await cascadeDataLayer.Do(cascadeDataLayer.originStore, requestOp);
				opResponse.FromOrigin = true;
				if (opResponse.Connected && (opResponse.ResultObject != null || requestOp.Verb == RequestOp.Verbs.Destroy) &&
				    opResponse.Error == null) {
					if (cascadeDataLayer.Layers.Count > 1)
						await CascadeWriteDown(opResponse, 1);
				}
			} else {
				opResponse = await CascadeReadUp(requestOp);
			}
			return opResponse;
		}

//			if (aRequestOp.Fresh)
//			{
//				try
//				{
//					remoteResponse = await originStore.Read(aRequestOp);
//				}
//				catch (Exception e)
//				{
//					//Log.Debug("Exception: " + e.Message);
//					if (!aRequestOp.Fallback)
//						throw;
//				}
//
//				if (remoteResponse!=null && remoteResponse.Connected)
//				{
//					if (remoteResponse.Present)
//					{
//						await localStore.Replace(remoteResponse.ResultObject);
//						result = remoteResponse.ResultObject;
//					}
//					else
//					{
//						localStore.Destroy<M>(aRequestOp.Id);
//						result = default(M);
//					}
//				}
//				else
//				{
//					if (aRequestOp.Fallback)
//					{
//						localResponse = await localStore.Read<M>(aRequestOp.Id);
//						result = localResponse.ResultObject;
//					}
//					else
//					{
//						result = default(M); // remoteResponse.value;
//					}
//				}
//			}
//			else
//			{
//
//				try
//				{
//					localResponse = await localStore.Read<M>(aRequestOp.Id);
//				}
//				catch (Exception e)
//				{
//					//Log.Debug("Exception: " + e.Message);
//					if (!aRequestOp.Fallback)
//						throw;
//				}
//
//				if (localResponse!=null && localResponse.Present)
//				{
//					result = localResponse.ResultObject;
//				}
//				else
//				{
//					if (aRequestOp.Fallback)
//					{
//						remoteResponse = await originStore.Read<M>(aRequestOp.Id);
//						result = remoteResponse.ResultObject;
//					}
//					else
//					{
//						result = default(M); // remoteResponse.value;
//					}
//				}
//			}
//			return result;

		async Task CascadeWriteDown(OpResponse opResponse, int iFirstLayer) {
			for (var i = iFirstLayer; i < cascadeDataLayer.Layers.Count; i++) {
				var layer = cascadeDataLayer.Layers[i];
				if (opResponse.ResultObject is ICascadeModel)
					layer.Replace(opResponse.ResultObject as ICascadeModel);
				else
					layer.KeySet(opResponse.ResultKey, cascadeDataLayer.JsonSerialize(opResponse.ResultObject));
			}
		}

		async Task<OpResponse> CascadeReadUp(RequestOp requestOp1) {
			OpResponse result;
			var seq = cascadeDataLayer.Layers.ToList();
			seq.Reverse();
			foreach (var layer in seq) {
				var response = await cascadeDataLayer.Do(layer, requestOp1);
				response.FromOrigin = layer.Origin;
				if (response.Connected && response.Present) {
					CascadeWriteDown(response, cascadeDataLayer.Layers.IndexOf(layer) + 1);
					return response;
				}
			}
			return null;
		}
	}
}

