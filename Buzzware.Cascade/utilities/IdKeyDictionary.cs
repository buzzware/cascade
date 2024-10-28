using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Buzzware.Cascade {
	
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class IdKeyDictionary<TValue> : IDictionary<object?, TValue> where TValue : class
{
    private readonly Dictionary<object, TValue> _dictionary;
    private static readonly object NullKey = new object();

    public IdKeyDictionary() {
        _dictionary = new Dictionary<object, TValue>(new MixedTypeComparer());
    }
    
    private object GetInternalKey(object? key) => key ?? NullKey;

    private object? GetExternalKey(object key) => ReferenceEquals(key, NullKey) ? null : key;

    public TValue this[object? key]
    {
        get => _dictionary[GetInternalKey(key)];
        set => _dictionary[GetInternalKey(key)] = value;
    }

    public ICollection<object?> Keys => _dictionary.Keys.Select(k => GetExternalKey(k)).ToList();
    public ICollection<TValue> Values => _dictionary.Values;
    public int Count => _dictionary.Count;
    public bool IsReadOnly => false;

    public void Add(object? key, TValue value)
    {
        _dictionary.Add(GetInternalKey(key), value);
    }

    public void Add(KeyValuePair<object?, TValue> item)
    {
        Add(item.Key, item.Value);
    }

    public void Clear()
    {
        _dictionary.Clear();
    }

    public bool Contains(KeyValuePair<object?, TValue> item)
    {
        return TryGetValue(item.Key, out var value) && EqualityComparer<TValue>.Default.Equals(value, item.Value);
    }

    public bool ContainsKey(object? key)
    {
        return _dictionary.ContainsKey(GetInternalKey(key));
    }

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

    public bool Remove(object? key)
    {
        return _dictionary.Remove(GetInternalKey(key));
    }

    public bool Remove(KeyValuePair<object?, TValue> item)
    {
        if (Contains(item))
        {
            return Remove(item.Key);
        }
        return false;
    }

    public bool TryGetValue(object? key, out TValue value)
    {
        return _dictionary.TryGetValue(GetInternalKey(key), out value);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void AddRange(IEnumerable<KeyValuePair<object?, TValue>> items)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));

        foreach (var item in items)
        {
            Add(item.Key, item.Value);
        }
    }

    public IReadOnlyDictionary<object?, TValue> AsReadOnly()
    {
        return new ReadOnlyDictionary<object?, TValue>(this);
    }
}	
	
	
	
	
	
	
	public class MixedTypeComparer : IEqualityComparer<object?> {

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

	// public class ValueModelsDictionary : Dictionary<object?, List<SuperModel>> {
	// 	public ValueModelsDictionary(StrictMixedTypeComparer strictMixedTypeComparer) : base(strictMixedTypeComparer) {
	// 	}
	//
	// 	public static ValueModelsDictionary CreateWithComparer() {
	// 		return new ValueModelsDictionary(new StrictMixedTypeComparer());
	// 	}
	// }
}
