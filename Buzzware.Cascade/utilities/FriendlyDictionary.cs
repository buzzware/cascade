using System;
using System.Collections.Generic;

namespace Buzzware.Cascade.Test {
    
    
    /// <summary>
    /// A Dictionary whose indexer returns null instead of throwing when the key is missing,
    /// and removes the key when a null value is assigned.
    /// </summary>
    /// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The reference type of values in the dictionary.</typeparam>
    public class FriendlyDictionary<TKey, TValue> : Dictionary<TKey, TValue> where TValue : class {
        
        /// <summary>
        /// Gets the value for the given key, or null when the key is not present.
        /// Setting null removes the key; otherwise the value is stored.
        /// </summary>
        /// <param name="key">The key to get or set the value for.</param>
        public new TValue? this[TKey key] {
            get => TryGetValue(key, out var value) ? value : null;
            set
            {
                if (value == null)
                {
                    Remove(key);
                }
                else
                {
                    base[key] = value;
                }
            }
        }
    }
}
