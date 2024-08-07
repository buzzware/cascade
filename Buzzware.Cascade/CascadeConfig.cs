using System.IO;

namespace Buzzware.Cascade {
	public class CascadeConfig {
		public int DefaultFreshnessSeconds = 5 * 60;
		public int DefaultPopulateFreshnessSeconds = 12 * 3600;
		public int DefaultFallbackFreshnessSeconds = RequestOp.FALLBACK_NEVER;
		public string StoragePath;

		public string PendingChangesPath => Path.Combine(StoragePath, "PendingChanges");
		public string HoldPath => Path.Combine(StoragePath, "Hold");
		public string MetaPath => Path.Combine(StoragePath, "Meta");
		public string FileCachePath => Path.Combine(StoragePath, "FileCache");
	}
}
