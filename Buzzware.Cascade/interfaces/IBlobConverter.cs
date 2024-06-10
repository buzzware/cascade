using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Buzzware.Cascade {
	
	public interface IBlobConverter {
		object? Convert(IReadOnlyList<byte>? blob, Type destinationPropertyType);
	}
}
