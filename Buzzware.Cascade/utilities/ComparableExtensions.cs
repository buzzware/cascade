#nullable disable
using System;

namespace Buzzware.Cascade
{
  /// <summary>
  /// Provides extension methods for any type implementing IComparable<T> to simplify comparison logic.
  /// </summary>
  public static class ComparableExtensions
  {
    /// <summary>
    /// Checks if the value is greater than the specified other value.
    /// </summary>
    /// <typeparam name="T">The type of the comparable values.</typeparam>
    /// <param name="value">The value to compare.</param>
    /// <param name="other">The other value to compare against.</param>
    /// <returns>True if the value is greater than the other value; otherwise, false.</returns>
    public static bool IsGreaterThan<T>(this T value, T other) where T : IComparable<T>
    {
      return value.CompareTo(other) > 0;
    }

    /// <summary>
    /// Checks if the value is greater than or equal to the specified other value.
    /// </summary>
    /// <typeparam name="T">The type of the comparable values.</typeparam>
    /// <param name="value">The value to compare.</param>
    /// <param name="other">The other value to compare against.</param>
    /// <returns>True if the value is greater than or equal to the other value; otherwise, false.</returns>
    public static bool IsGreaterOrEqual<T>(this T value, T other) where T : IComparable<T>
    {
      return value.CompareTo(other) >= 0;
    }

    /// <summary>
    /// Checks if the value is less than the specified other value.
    /// </summary>
    /// <typeparam name="T">The type of the comparable values.</typeparam>
    /// <param name="value">The value to compare.</param>
    /// <param name="other">The other value to compare against.</param>
    /// <returns>True if the value is less than the other value; otherwise, false.</returns>
    public static bool IsLessThan<T>(this T value, T other) where T : IComparable<T>
    {
      return value.CompareTo(other) < 0;
    }

    /// <summary>
    /// Checks if the value is less than or equal to the specified other value.
    /// </summary>
    /// <typeparam name="T">The type of the comparable values.</typeparam>
    /// <param name="value">The value to compare.</param>
    /// <param name="other">The other value to compare against.</param>
    /// <returns>True if the value is less than or equal to the other value; otherwise, false.</returns>
    public static bool IsLessThanOrEqual<T>(this T value, T other) where T : IComparable<T>
    {
      return value.CompareTo(other) <= 0;
    }

    /// <summary>
    /// Checks if the value is between the specified lower and upper bounds.
    /// </summary>
    /// <typeparam name="T">The type of the comparable values.</typeparam>
    /// <param name="value">The value to compare.</param>
    /// <param name="lower">The lower bound.</param>
    /// <param name="upper">The upper bound.</param>
    /// <param name="inclusive">If true, includes the bounds in the comparison; otherwise, excludes them.</param>
    /// <returns>True if the value is between the lower and upper bounds; otherwise, false.</returns>
    public static bool IsBetween<T>(this T value, T lower, T upper, bool inclusive) where T : IComparable<T>
    {
      // Check if value is within the lower bound based on inclusivity.
      if ((inclusive ? (lower.IsLessThanOrEqual<T>(value) ? 1 : 0) : (lower.IsLessThan<T>(value) ? 1 : 0)) == 0)
        return false;

      // Check if value is within the upper bound based on inclusivity.
      return !inclusive ? value.IsLessThan<T>(upper) : value.IsLessThanOrEqual<T>(upper);
    }
  }
}