using System;

namespace Buzzware.Cascade {

  /// <summary>
  /// FNVHash provides methods for generating hash values using the [Fowler-Noll-Vo hashing algorithm](https://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function).
  /// It is chosen for its speed (10-100X SHA-256) and low collision rate.
  /// It supports generating hash values of varying bit lengths.
  /// FNV is not a cryptographic hash.
  /// </summary>
  public class FNVHash {

    private static readonly ulong PRIME32 = 16777619;
    private static readonly ulong PRIME64 = 1099511628211UL;
    private static readonly ulong OFFSET32 = 2166136261;
    private static readonly ulong OFFSET64 = 14695981039346656037UL;

    private static readonly ulong MOD32 = 4294967296;
    private static readonly ulong MOD64 = long.MaxValue; // should actually end in 6 but too big

    /// <summary>
    /// Generates a hash value for a given string using a specified bit length.
    /// This method utilizes the FNV hashing algorithm and supports 32-bit and 64-bit hashing.
    /// </summary>
    /// <param name="aString">The input string to hash.</param>
    /// <param name="aHashBits">The bit length of the hash</param>
    /// <returns>Hash value of specified bit length.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an invalid bit size is provided.</exception>
    public static ulong GetHash(string? aString, int aHashBits) {
      ulong prime;
      ulong offset;
      ulong modulo;

      // Determine the prime, offset, and modulo based on the hash size.
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
      
      // Process each character in the string and perform the hash calculation.
      for (int i = 0; i < aString.Length; i++) {
        hash ^= (uint)aString[i];
        if (modulo != MOD64)
          hash %= modulo;
        hash *= prime;
      }

      // Adjust the hash if the hash bit size is not a power of two.
      if (!((aHashBits & (aHashBits - 1)) == 0)) {
        ulong mask = Convert.ToUInt64(new string('f', (aHashBits / 4) + (aHashBits % 4 != 0 ? 1 : 0)), 16);  
        hash = (hash >> aHashBits) ^ (mask & hash);
      }
      return hash;
    }
  }
}
