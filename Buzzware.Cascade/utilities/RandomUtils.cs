using System;

namespace Buzzware.Cascade {
	
  /// <summary>
  /// Utility class providing methods for generating random values.
  /// </summary>
  public static class RandomUtils {
    /// <summary>
    /// A single shared instance of the Random class to provide random number generation.
    /// </summary>
    private static readonly Random RandomInstance = new Random();

    /// <summary>
    /// Generates a new random integer within a specified range.
    /// </summary>
    /// <param name="min">The inclusive lower bound of the random number returned.</param>
    /// <param name="max">The exclusive upper bound of the random number returned.</param>
    /// <returns>An integer value between 'min' and 'max - 1'.</returns>
    public static int NewInt(int min = int.MinValue, int max = int.MaxValue) {
      return RandomInstance.Next(min, max);
    }

    /// <summary>
    /// Generates a new random long (Int64) value.
    /// </summary>
    /// <returns>A randomly generated long value.</returns>
    public static long NewLong() {
      // Prepare a buffer to hold a random 8-byte array.
      byte[] buffer = new byte[8];
      
      // Fill the buffer with random bytes.
      RandomInstance.NextBytes(buffer);
      
      // Convert the byte array to a long integer and return it.
      return BitConverter.ToInt64(buffer, 0);
    }

    /// <summary>
    /// Generates a random non-negative integer that is less than 0xFFFFFF.
    /// </summary>
    /// <returns>A random integer less than 0xFFFFFF.</returns>
    public static int IntNegativeId() {
      // Generate and return a random integer within the specified range.
      var result = NewInt(0xFFFFFF);
      return result;
    }

    /// <summary>
    /// Generates a random negative long (Int64) value.
    /// </summary>
    /// <returns>A random negative long value.</returns>
    public static long LongNegativeId() {
      // Generate a new random long value.
      var result = NewLong();
      
      // If the generated value is positive, negate it.
      if (result > 0)
        result = -result;
      
      // Return the negative long value.
      return result;
    }
  }
}
