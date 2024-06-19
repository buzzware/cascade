using System;

namespace Buzzware.Cascade.Test {
	public class DotNetBitmapConverter : IBlobConverter {
		public object? Convert(byte[]? blob, Type destinationPropertyType) {
			return blob!=null ? TestUtils.BitmapFromBlob(blob) : null;
		}
	}
}
