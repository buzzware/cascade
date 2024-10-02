using System;
using System.Collections.Generic;

namespace Buzzware.Cascade.Test {
    
    
    public class FriendlyDictionary<TKey, TValue> : Dictionary<TKey, TValue> where TValue : class {
        
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
