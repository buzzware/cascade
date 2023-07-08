using System;

namespace Cascade {

	public static class OfflineUtils {

		public static SuperModel CreateOffline(SuperModel value, Func<string> newGuid) {
			SuperModel result = value.Clone();
			var id = CascadeTypeUtils.GetCascadeId(value);
			var idType = CascadeTypeUtils.GetCascadeIdType(value.GetType());
			if (idType == typeof(int)) {
				if ((long)id == 0) {
					CascadeTypeUtils.SetCascadeId(result, id = RandomUtils.IntNegativeId());
				}
			}
			else if (idType == typeof(long)) {
				if ((long)id == 0)
					CascadeTypeUtils.SetCascadeId(result, id = RandomUtils.LongNegativeId());
			}
			else if (idType == typeof(string)) {
				if (id == null) {
					id = newGuid();
					CascadeTypeUtils.SetCascadeId(result, id);
				}
			}
			else {
				throw new Exception("Unsupported IdType");
			}

			return result;
		}
	}
}
