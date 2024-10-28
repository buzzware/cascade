using System.Collections.Generic;

namespace Buzzware.Cascade {
	
	public class StrictMixedTypeComparer : IEqualityComparer<object?> {
		
		public new bool Equals(object? x, object? y) {
			// If both null or same reference, they're equal
			if (ReferenceEquals(x, y))
				return true;
        
			// If only one is null, they're not equal
			if (x == null || y == null)
				return false;

			// If they're different types, they're not equal
			if (x.GetType() != y.GetType())
				return false;

			if (x is int intX && y is int intY)
				return intX == intY;

			if (x is long longX && y is long longY)
				return longX == longY;

			// If both are strings
			if (x is string strX && y is string strY)
				return string.Equals(strX, strY);

			// Different types - use default equality
			return x.Equals(y);
		}

		public int GetHashCode(object? obj) {
			if (obj == null)
				return 0;

			unchecked {
				int hash = 17;
				hash = hash * 23 + obj.GetType().GetHashCode();
				hash = hash * 23 + obj.GetHashCode();
				return hash;
			}
		}	
	}

	public class ValueModelsDictionary : Dictionary<object?, List<SuperModel>> {
		public ValueModelsDictionary(StrictMixedTypeComparer strictMixedTypeComparer) : base(strictMixedTypeComparer) {
		}

		public static ValueModelsDictionary CreateWithComparer() {
			return new ValueModelsDictionary(new StrictMixedTypeComparer());
		}
	}
}
