using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Buzzware.Cascade {
	
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A dictionary for id-like keys of possibly mixed types. Supports null keys, and uses MixedTypeComparer
/// so that integer keys of different types (eg. int and long) with equal values are treated as the same key.
/// </summary>
/// <typeparam name="TValue">The reference type of values stored in the dictionary.</typeparam>
public class IdKeyDictionary<TValue> : IDictionary<object?, TValue> where TValue : class
{
    private readonly Dictionary<object, TValue> _dictionary;
    private static readonly object NullKey = new object();

    /// <summary>
    /// IdKeyDictionary Constructor. Creates the underlying dictionary using a MixedTypeComparer for keys.
    /// </summary>
    public IdKeyDictionary() {
        _dictionary = new Dictionary<object, TValue>(new MixedTypeComparer());
    }
    
    /// <summary>
    /// Converts an external, possibly null key to the internal key, substituting the NullKey sentinel for null.
    /// </summary>
    /// <param name="key">The external key, which may be null.</param>
    /// <returns>The key itself, or the NullKey sentinel object when the key is null.</returns>
    private object GetInternalKey(object? key) => key ?? NullKey;

    /// <summary>
    /// Converts an internal key back to the external key, mapping the NullKey sentinel to null.
    /// </summary>
    /// <param name="key">The internal key.</param>
    /// <returns>Null when the key is the NullKey sentinel; otherwise the key itself.</returns>
    private object? GetExternalKey(object key) => ReferenceEquals(key, NullKey) ? null : key;

    /// <summary>
    /// Gets or sets the value associated with the given key, which may be null.
    /// </summary>
    /// <param name="key">The key of the value to get or set.</param>
    public TValue this[object? key]
    {
        get => _dictionary[GetInternalKey(key)];
        set => _dictionary[GetInternalKey(key)] = value;
    }

    /// <summary>
    /// A newly created list of the external keys, with the NullKey sentinel converted back to null.
    /// </summary>
    public ICollection<object?> Keys => _dictionary.Keys.Select(k => GetExternalKey(k)).ToList();
    /// <summary>
    /// The collection of values in the dictionary.
    /// </summary>
    public ICollection<TValue> Values => _dictionary.Values;
    /// <summary>
    /// The number of key/value pairs in the dictionary.
    /// </summary>
    public int Count => _dictionary.Count;
    /// <summary>
    /// Always false - the dictionary is writable.
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// Adds a key/value pair, allowing a null key. Throws when the key already exists.
    /// </summary>
    /// <param name="key">The key to add, which may be null.</param>
    /// <param name="value">The value to associate with the key.</param>
    public void Add(object? key, TValue value)
    {
        _dictionary.Add(GetInternalKey(key), value);
    }

    /// <summary>
    /// Adds the given key/value pair.
    /// </summary>
    /// <param name="item">The key/value pair to add.</param>
    public void Add(KeyValuePair<object?, TValue> item)
    {
        Add(item.Key, item.Value);
    }

    /// <summary>
    /// Removes all keys and values from the dictionary.
    /// </summary>
    public void Clear()
    {
        _dictionary.Clear();
    }

    /// <summary>
    /// Determines whether the dictionary contains the given pair's key with a value equal to the pair's value.
    /// </summary>
    /// <param name="item">The key/value pair to locate.</param>
    /// <returns>True when the key exists and its value equals the pair's value; otherwise false.</returns>
    public bool Contains(KeyValuePair<object?, TValue> item)
    {
        return TryGetValue(item.Key, out var value) && EqualityComparer<TValue>.Default.Equals(value, item.Value);
    }

    /// <summary>
    /// Determines whether the dictionary contains the given key, which may be null.
    /// </summary>
    /// <param name="key">The key to locate.</param>
    /// <returns>True when the key exists; otherwise false.</returns>
    public bool ContainsKey(object? key)
    {
        return _dictionary.ContainsKey(GetInternalKey(key));
    }

    /// <summary>
    /// Copies all key/value pairs into the given array starting at the given index.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based index in the array at which copying begins.</param>
    public void CopyTo(KeyValuePair<object?, TValue>[] array, int arrayIndex)
    {
        if (array == null)
            throw new ArgumentNullException(nameof(array));

        if (arrayIndex < 0 || arrayIndex > array.Length)
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));

        if (array.Length - arrayIndex < Count)
            throw new ArgumentException("Destination array is not long enough");

        int i = arrayIndex;
        foreach (var pair in this)
        {
            array[i] = pair;
            i++;
        }
    }

    /// <summary>
    /// Enumerates the key/value pairs, converting the internal NullKey sentinel back to a null key.
    /// </summary>
    /// <returns>An enumerator over the key/value pairs with external keys.</returns>
    public IEnumerator<KeyValuePair<object?, TValue>> GetEnumerator()
    {
        foreach (var pair in _dictionary)
        {
            yield return new KeyValuePair<object?, TValue>(
                GetExternalKey(pair.Key),
                pair.Value
            );
        }
    }

    /// <summary>
    /// Removes the value with the given key, which may be null.
    /// </summary>
    /// <param name="key">The key to remove.</param>
    /// <returns>True when the key was found and removed; otherwise false.</returns>
    public bool Remove(object? key)
    {
        return _dictionary.Remove(GetInternalKey(key));
    }

    /// <summary>
    /// Removes the given pair only when both its key and value match an existing entry.
    /// </summary>
    /// <param name="item">The key/value pair to remove.</param>
    /// <returns>True when the pair was found and removed; otherwise false.</returns>
    public bool Remove(KeyValuePair<object?, TValue> item)
    {
        if (Contains(item))
        {
            return Remove(item.Key);
        }
        return false;
    }

    /// <summary>
    /// Attempts to get the value associated with the given key, which may be null.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="value">The value found, or the default value when not found.</param>
    /// <returns>True when the key was found; otherwise false.</returns>
    public bool TryGetValue(object? key, out TValue value)
    {
        return _dictionary.TryGetValue(GetInternalKey(key), out value);
    }

    /// <summary>
    /// Returns a non-generic enumerator over the key/value pairs.
    /// </summary>
    /// <returns>An IEnumerator over the key/value pairs.</returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    /// Adds each of the given key/value pairs to the dictionary.
    /// </summary>
    /// <param name="items">The key/value pairs to add.</param>
    public void AddRange(IEnumerable<KeyValuePair<object?, TValue>> items)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));

        foreach (var item in items)
        {
            Add(item.Key, item.Value);
        }
    }

    /// <summary>
    /// Wraps this dictionary in a read-only view.
    /// </summary>
    /// <returns>A ReadOnlyDictionary wrapping this dictionary.</returns>
    public IReadOnlyDictionary<object?, TValue> AsReadOnly()
    {
        return new ReadOnlyDictionary<object?, TValue>(this);
    }
}	
	
	
	
	
	
	
	/// <summary>
	/// An equality comparer for keys of possibly mixed types. Integer values (int, long, short, byte, uint)
	/// are compared and hashed by numeric value regardless of their exact type; other types use default equality.
	/// </summary>
	public class MixedTypeComparer : IEqualityComparer<object?> {

		/// <summary>
		/// Compares two objects numerically when either is of an integer type (int, long, short, byte or uint).
		/// </summary>
		/// <param name="x">The first object to compare.</param>
		/// <param name="y">The second object to compare.</param>
		/// <returns>Null when neither x nor y is of an integer type; otherwise true only when both are integers of equal numeric value.</returns>
		public static bool? compareObjectIntegers(object x, object y) {
			long? xLong = x switch {
				int i => i,
				long l => l,
				short s => s,
				byte b => b,
				uint u => u,
				_ => null
			};
			long? yLong = y switch {
				int i => i,
				long l => l,
				short s => s,
				byte b => b,
				uint u => u,
				_ => null
			};
			return xLong==null && yLong==null ? null : xLong == yLong;
		} 
		
		/// <summary>
		/// Determines equality of two keys : reference/null equality first, string comparison for strings,
		/// numeric comparison when either is of an integer type, otherwise default equality for matching types.
		/// </summary>
		/// <param name="x">The first key to compare, which may be null.</param>
		/// <param name="y">The second key to compare, which may be null.</param>
		/// <returns>True when the keys are considered equal; otherwise false.</returns>
		public new bool Equals(object? x, object? y) {
			// If both null or same reference, they're equal
			if (ReferenceEquals(x, y))
				return true;
        
			// If only one is null, they're not equal
			if (x == null || y == null)
				return false;

			if (x is string strX && y is string strY)
				return string.Equals(strX, strY);
			
			var intComparison = compareObjectIntegers(x, y);
			if (intComparison!=null)
				return intComparison.Value;
			
			// If they're different types, they're not equal
			if (x.GetType() != y.GetType())
				return false;

			// if (x is int intX && y is int intY)
			// 	return intX == intY;
			//
			// if (x is long longX && y is long longY)
			// 	return longX == longY;

			// If both are strings

			// Different types - use default equality
			return x.Equals(y);
		}

		/// <summary>
		/// Computes a hash code, hashing integer typed values (int, long, short, byte, uint) via their long value
		/// so that numerically equal integers of different types hash identically.
		/// </summary>
		/// <param name="obj">The key to hash, which may be null.</param>
		/// <returns>The computed hash code, or 0 for null.</returns>
		public int GetHashCode(object? obj) {
			if (obj == null)
				return 0;

			unchecked {
				int hash = 17;
				//hash = hash * 23 + obj.GetType().GetHashCode();
				long? longObj = obj switch {
					int i => i,
					long l => l,
					short s => s,
					byte b => b,
					uint u => u,
					_ => null
				};
				int objHash = longObj?.GetHashCode() ?? obj.GetHashCode();
				hash = hash * 23 + objHash;
				return hash;
			}
		}	
	}
}
