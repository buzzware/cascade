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

		public static async Task<byte[]> ReadBinaryFile(string path) {
			var builder = ImmutableArray.CreateBuilder<byte>();

			using (FileStream sourceStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true))
			{
				byte[] buffer = new byte[4096];
				int numRead;
				while ((numRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
				{
					for(int i = 0; i < numRead; i++)
					{
						builder.Add(buffer[i]);
					}
				}
			}
			return builder.ToArray();
		}
		
		public static async Task WriteBinaryFile(string path, byte[] content)
		{
			using (FileStream destinationStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true))
			{
				byte[] buffer = content.ToArray();
				await destinationStream.WriteAsync(buffer, 0, buffer.Length);
			}
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
