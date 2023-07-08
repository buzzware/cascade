using System;

namespace Cascade {
	
	public static class RandomUtils {
		private static readonly Random RandomInstance = new Random();

		public static int NewInt(int min = int.MinValue, int max = int.MaxValue) {
			return RandomInstance.Next(min, max);
		}

		public static long NewLong() {
			byte[] buffer = new byte[8];
			RandomInstance.NextBytes(buffer);
			return BitConverter.ToInt64(buffer, 0);
		}

		public static int IntNegativeId() {
			var result = NewInt(0xFFFFFF);
			return result;
		}

		public static long LongNegativeId() {
			var result = NewLong();
			if (result > 0)
				result = -result;
			return result;
		}
	}
}
