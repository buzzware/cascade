using System.Collections.Generic;
using System.Collections.Immutable;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Buzzware.Cascade.Test {
	public static class TestUtils {
		
		public static byte[] NewBlob(byte value, int length) {
			byte[] data = new byte[length];
			for (int i = 0; i < length; i++)
				data[i] = value;
			return data;
		}

		public static void CreateBinaryFile(string path, byte value, int length) {
			byte[] data = new byte[length];
			for (int i = 0; i < length; i++)
				data[i] = value;
			System.IO.File.WriteAllBytes(path, data);
		}
		
		public static byte[] BlobFromBitmap(Bitmap bitmap, ImageFormat format)
		{
			using(MemoryStream stream = new MemoryStream())
			{
				bitmap.Save(stream, format);
				return stream.ToArray();
			}
		}

		public static Bitmap BitmapFromBlob(byte[] blob) {
			using (var stream = new MemoryStream(blob.ToArray()))
			{
				return new Bitmap(stream);
			}
		}	
	}
}
