using System;
using System.Collections.Immutable;

namespace Buzzware.Cascade {
	
	public interface IBlobConverter {
		object? Convert(ImmutableArray<byte>? blob, Type destinationPropertyType);
	}
}
