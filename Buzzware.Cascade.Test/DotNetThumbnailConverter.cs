using System;
using System.Drawing;
using System.Threading.Tasks;

namespace Buzzware.Cascade.Test {
	
	/// <summary>
	/// Converts a bitmap in the given source property to a scaled thumbnail of the given dimensions and sets the local property 
	/// </summary>
	public class DotNetThumbnailConverter : IPropertyConverter {
		public static readonly int DEFAULT_SIZE = 128;

		public async Task<object?> Convert(object? input, Type destinationType, params object[] args) {
			if (input == null)
				return null;
			if (input is Bitmap bitmap && (destinationType == typeof(Bitmap))) {
				int width = DEFAULT_SIZE, height = DEFAULT_SIZE;
				if (args.Length >= 1)
					width = (args.GetValue(0) as int?) ?? DEFAULT_SIZE;
				if (args.Length >= 2)
					height = (args.GetValue(0) as int?) ?? width;
				//return bitmap.GetThumbnailImage(width,height,)
				
				Image thumbnail = bitmap.GetThumbnailImage(width, height, 
					new Image.GetThumbnailImageAbort(ThumbnailCallback), 
					IntPtr.Zero);
				return thumbnail;
			} else 
				throw new NotImplementedException();
		}
		
		public bool ThumbnailCallback()
		{
			return false;
		}				
		
	}
}
