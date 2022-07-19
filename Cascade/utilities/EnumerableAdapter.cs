using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace Cascade {

	// This class implements IEnumerable<T> (and therefore IEnumerable) but simply returns values from the source - it has no state of its own 
	public class EnumerableAdapter<T> : IEnumerable<T> {
		public IEnumerable Source { get; }

		public EnumerableAdapter(IEnumerable source) {
			Source = source;
		}

		public IEnumerator<T> GetEnumerator() {
			foreach (var item in Source)
				yield return (T)item;
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}

		ImmutableArray<T> ToImmutableArray() {
			return ImmutableArray.CreateRange(this);
		}
	}

	// This static class really just provides static methods for using EnumerableAdapter<T>
	public static class EnumerableAdapter {

		// Creates an IEnumerable<T> but returns it as the base type IEnumerable to avoid generics  
		public static IEnumerable Create(Type itemType, IEnumerable source) {
			return (IEnumerable)Activator.CreateInstance(typeof(EnumerableAdapter<>).MakeGenericType(itemType), new object[] { source });
		}

		// Creates an ImmutableArray<T> but returns it as the base type IEnumerable to avoid generics
		// IEnumerable<T> inherits from IEnumerable 
		public static IEnumerable CreateImmutableArray(Type itemType, IEnumerable source) {
			var adapter = Create(itemType, source);
			var method = typeof(ImmutableArray).GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(m => m.Name == "CreateRange" && m.GetParameters().Length == 1);
			MethodInfo genericMethod = method!.MakeGenericMethod(new Type[] { itemType });
			var result = genericMethod.Invoke(null, new object[] { adapter });
			return (IEnumerable)result;
		}
	}
}
