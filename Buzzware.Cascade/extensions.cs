// using System.Collections.Generic;
// using System.Collections.Immutable;
//
//
// namespace System.Linq {
// 	public static partial class Enumerable {
// 		public static ImmutableArray<TSource> ToImmutableArray<TSource>(this IEnumerable<TSource> source) {
// 			if (source == null) {
// 				throw new NullReferenceException(nameof(source));
// 			}
//
// 			return ImmutableArray.
// 			
// 			return source is IIListProvider<TSource> arrayProvider
// 				? arrayProvider.ToArray()
// 				: EnumerableHelpers.ToArray(source);
// 		}
// 	}
// }
