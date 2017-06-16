using System;

namespace Cascade {
	public static class CascadeUtils
	{
		public static long LongId(string aResourceId) {
			EnsureIsResourceId(aResourceId);
			return Convert.ToInt64(aResourceId);
		}

		public static string EnsureIsResourceId(string aResourceId) {
			if (!IsResourceId(aResourceId))
				throw new Exception("aResourceId is not a valid resource id");
			return aResourceId;
		}

		public static long EnsureIsResourceId(long aResourceId) {
			if (aResourceId==0)
				throw new Exception("aResourceId is not a valid resource id");
			return aResourceId;
		}

		public static bool IsResourceId(string aResourceId) {
			return !(aResourceId == null || aResourceId == "0" || aResourceId == "");
		}		

		public static bool IsResourceId(int aResourceId) {
			return !(aResourceId == 0);
		}

		public static bool IsResourceId(long aResourceId) {
			return !(aResourceId == 0);
		}


	}
}