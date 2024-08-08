using System;

namespace Buzzware.Cascade.Test {
	
	/// <summary>
	/// Converts a binary blob to a DotNet Bitmap
	/// </summary>
	public class DotNetBitmapConverter : IBlobConverter {
		public object? Convert(byte[]? blob, Type destinationPropertyType) {
			return blob!=null ? TestUtils.BitmapFromBlob(blob) : null;
		}
	}
}
