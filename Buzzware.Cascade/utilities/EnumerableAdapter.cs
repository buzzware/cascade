using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace Buzzware.Cascade {

  /// <summary>
  /// This class implements IEnumerable<T> to adapt a non-generic IEnumerable 
  /// to work with a specified type T by casting the elements of the source 
  /// enumeration to type T.
  /// </summary>
	public class EnumerableAdapter<T> : IEnumerable<T> {
    
    /// <summary>
    /// The non-generic source enumeration that will be adapted to IEnumerable<T>
    /// </summary>
		public IEnumerable Source { get; }

    /// <summary>
    /// EnumerableAdapter Constructor. Initializes a new EnumerableAdapter
    /// with a specified non-generic source enumeration.
    /// </summary>
    /// <param name="source">The non-generic IEnumerable source to be adapted.</param>
		public EnumerableAdapter(IEnumerable source) {
			Source = source;
		}

    /// <summary>
    /// Returns an enumerator that iterates through the collection, converting each element 
    /// to the target generic type T.
    /// </summary>
    /// <returns>An IEnumerator of type T to iterate through the collection.</returns>
		public IEnumerator<T> GetEnumerator() {
			foreach (var item in Source)
				yield return (T)item;
		}

    /// <summary>
    /// Returns an enumerator that iterates through a collection.
    /// Implements the non-generic IEnumerable GetEnumerator method.
    /// </summary>
    /// <returns>An IEnumerator to iterate through the collection.</returns>
		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}

    /// <summary>
    /// Converts the sequence of type T elements to an ImmutableArray<T>.
    /// </summary>
    /// <returns>An ImmutableArray containing the elements of the sequence.</returns>
		ImmutableArray<T> ToImmutableArray() {
			return ImmutableArray.CreateRange(this);
		}
	}

  /// <summary>
  /// Static class providing utility methods for creating generic and non-generic
  /// sequences from a non-generic source using the EnumerableAdapter.
  /// </summary>
	public static class EnumerableAdapter {

    /// <summary>
    /// Creates an IEnumerable of a specified item type from a non-generic source.
    /// This method adapts the source to the generic IEnumerable using reflection.
    /// </summary>
    /// <param name="itemType">The type of the elements expected in the source.</param>
    /// <param name="source">The non-generic IEnumerable source to be adapted.</param>
    /// <returns>An IEnumerable interface over the adapted source.</returns>
		public static IEnumerable Create(Type itemType, IEnumerable source) {
			return (IEnumerable)Activator.CreateInstance(typeof(EnumerableAdapter<>).MakeGenericType(itemType), new object[] { source });
		}

    /// <summary>
    /// Creates an ImmutableArray of a specified item type from a non-generic source
    /// and returns it as the base type IEnumerable. Utilizes the CreateRange method for creation.
    /// </summary>
    /// <param name="itemType">The type of the elements expected in the source.</param>
    /// <param name="source">The non-generic IEnumerable source to be converted.</param>
    /// <returns>An IEnumerable interface over an immutable array of the source elements.</returns>
		public static IEnumerable CreateImmutableArray(Type itemType, IEnumerable source) {
      // Create an IEnumerable<T> adapter for the source
			var adapter = Create(itemType, source);
      // Locate the CreateRange method using reflection to make it generic
			var method = typeof(ImmutableArray).GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(m => m.Name == "CreateRange" && m.GetParameters().Length == 1);
      // Make the method generic for the itemType
			MethodInfo genericMethod = method!.MakeGenericMethod(new Type[] { itemType });
      // Invoke the method to create an ImmutableArray
			var result = genericMethod.Invoke(null, new object[] { adapter });
			return (IEnumerable)result;
		}
	}
}