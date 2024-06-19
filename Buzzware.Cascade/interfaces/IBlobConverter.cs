using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Buzzware.Cascade {
	
	public interface IBlobConverter {
		object? Convert(byte[]? blob, Type destinationPropertyType);
	}
}
