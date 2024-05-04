using System;

namespace Buzzware.Cascade {
	
	public class FNVHash {

		private static readonly ulong PRIME32 = 16777619;
		private static readonly ulong PRIME64 = 1099511628211UL;
		private static readonly ulong OFFSET32 = 2166136261;
		private static readonly ulong OFFSET64 = 14695981039346656037UL;

		private static readonly ulong MOD32 = 4294967296;
		private static readonly ulong MOD64 = long.MaxValue; // should actually end in 6 but too big

		public static ulong GetHash(string? aString, int aHashBits) {
			ulong prime;
			ulong offset;
			ulong modulo;
			if (aHashBits <= 32) {
				prime = PRIME32;
				offset = OFFSET32;
				modulo = MOD32;
			} else if (aHashBits <= 64) {
				prime = PRIME64;
				offset = OFFSET64;
				modulo = MOD64;
			} else {
				throw new ArgumentOutOfRangeException("hashBitSize");
			}

			ulong hash = offset;
			for (int i = 0; i < aString.Length; i++) {
				hash ^= (uint)aString[i];
				if (modulo!=MOD64)
					hash %= modulo;
				hash *= prime;
			}
			if (!((aHashBits & (aHashBits - 1)) == 0)) {
				ulong mask = Convert.ToUInt64(new string('f', (aHashBits / 4) + (aHashBits % 4 != 0 ? 1 : 0)),16);  
				hash = (hash >> aHashBits) ^ (mask & hash);
			}
			return hash;
		}

		public static long Negative52BitHash(string? aString) {
			long result = (long)GetHash(aString,52);
			if (result>0)
				result = -result;
			return result;
		}
	}
}
