using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Cascade
{
	public class CascadeDataLayer {

		ICascadeStore localStore;
		ICascadeStore remoteStore;

		public CascadeDataLayer()
		{
			localStore = new SqliteNetStore();
			remoteStore = new RestServerStore();
		}

		public Task<M> Read<M>(long aResourceId, bool aFresh = true, bool aFallback = true) where M : class, ICascadeModel, new()
		{
			return Read<M>(aResourceId.ToString(),aFresh,aFallback);
		}

		public async Task<M> Read<M>(string aResourceId, bool aFresh = true, bool aFallback = true)
			where M : class, ICascadeModel, new()
		{
			CascadeUtils.EnsureIsResourceId(aResourceId);
			CascadeStoreResponse<M> localResponse = null;
			CascadeStoreResponse<M> remoteResponse = null;
			M result = default(M);
			//M remote = null;
			//M local = null;
			//bool remoteConnected = false;
			//bool remotePresent = false;
			//bool localPresent = false;

			if (aFresh)
			{
				try
				{
					remoteResponse = await remoteStore.Read<M>(aResourceId);
				}
				catch (Exception e)
				{
					Log.Debug("Exception: " + e.Message);
					if (!aFallback)
						throw;
				}

				if (remoteResponse!=null && remoteResponse.connected)
				{
					if (remoteResponse.present)
					{
						localStore.Write<M>(remoteResponse.value);
						result = remoteResponse.value;
					}
					else
					{
						localStore.Destroy<M>(aResourceId);
						result = default(M);
					}
				}
				else
				{
					if (aFallback)
					{
						localResponse = await localStore.Read<M>(aResourceId);
						result = localResponse.value;
					}
					else
					{
						result = default(M); // remoteResponse.value;
					}
				}
			}
			else
			{

				try
				{
					localResponse = await localStore.Read<M>(aResourceId);
				}
				catch (Exception e)
				{
					Log.Debug("Exception: " + e.Message);
					if (!aFallback)
						throw;
				}

				if (localResponse!=null && localResponse.present)
				{
					result = localResponse.value;
				}
				else
				{
					if (aFallback)
					{
						remoteResponse = await remoteStore.Read<M>(aResourceId);
						result = remoteResponse.value;
					}
					else
					{
						result = default(M); // remoteResponse.value;
					}
				}
			}
			return result;
		}

		public async Task<List<M>> ReadAll<M>(bool aFresh = true, bool aFallback = true, bool aExclusive = false)
			where M : class,
			ICascadeModel, new()
		{
			CascadeStoreResponse<List<M>> localResponse = null;
			CascadeStoreResponse<List<M>> remoteResponse = null;

			List<M> result = null;

			if (aFresh)
			{
				try
				{
					remoteResponse = await remoteStore.ReadAll<M>();
				}
				catch (Exception e)
				{
					Log.Debug("Exception: " + e.Message);
					if (!aFallback)
						throw;
				}

				if (remoteResponse.connected)
				{
					if (remoteResponse.present)
					{
						var items = remoteResponse.value;
						foreach (var r in items)
						{
							await localStore.Write<M>(r);
						}
						if (aExclusive)
						{
							IEnumerable<string> ids = items.Select(i => i.GetResourceId()).ToArray();
							await localStore.DestroyExcept<M>(ids);
						}
					}
					result = remoteResponse.value ?? new List<M>();
				}
				else
				{
					if (aFallback)
					{
						localResponse = await localStore.ReadAll<M>();
						result = localResponse.value;
					}
					else
					{
						result = new List<M>();
					}
				}
			}
			else
			{

				try
				{
					localResponse = await localStore.ReadAll<M>();
				}
				catch (Exception e)
				{
					Log.Debug("Exception: " + e.Message);
					if (!aFallback)
						throw;
				}

				if (localResponse.present)
				{
					result = localResponse.value;
				}
				else
				{
					if (aFallback)
					{
						remoteResponse = await remoteStore.ReadAll<M>();
						result = remoteResponse.value;
					}
					else
					{
						result = null; // remoteResponse.value;
					}
				}
			}
			return result;
		}

		public async Task<M> Write<M>(M value) where M : class, ICascadeModel, new()
		{
			M result = default(M);
			CascadeStoreResponse<M> localResponse = null;
			CascadeStoreResponse<M> remoteResponse = null;

			remoteResponse = await remoteStore.Write<M>(value);
			if (remoteResponse.connected) {
				if (remoteResponse.present) {
					localStore.Write<M>(remoteResponse.value);
					result = remoteResponse.value;
				} else {
					localStore.Destroy<M>(value.GetResourceId());
					result = null;
				}
			} else {
				throw new HttpService.NoNetworkException("Failed to reach remote store");
			}
			return result;
		}
	}
}