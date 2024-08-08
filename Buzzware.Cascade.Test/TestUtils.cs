
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Buzzware.Cascade.Test {
  
  /// <summary>
  /// Utility class providing static methods for test purposes
  /// </summary>
  public static class TestUtils {
    
    /// <summary>
    /// Creates a new byte array filled with a specified value.
    /// </summary>
    /// <param name="value">The byte value with which to fill the array.</param>
    /// <param name="length">The length of the byte array.</param>
    /// <returns>A byte array where each element is initialized to the specified value.</returns>
    public static byte[] NewBlob(byte value, int length) {
      byte[] data = new byte[length];
      
      // Fill the array with the specified byte value
      for (int i = 0; i < length; i++)
        data[i] = value;
      return data;
    }

    /// <summary>
    /// Creates a binary file at the specified path with a specified byte value repeated for the given length.
    /// </summary>
    /// <param name="path">The file path where the binary file will be created.</param>
    /// <param name="value">The byte value that will be written repeatedly into the file.</param>
    /// <param name="length">The number of times the byte value will be written into the file.</param>
    public static void CreateBinaryFile(string path, byte value, int length) {
      byte[] data = new byte[length];
      
      // Fill the byte array with the specified byte value
      for (int i = 0; i < length; i++)
        data[i] = value;
        
      // Write the byte array to the specified file path
      System.IO.File.WriteAllBytes(path, data);
    }
    
    /// <summary>
    /// Converts a Bitmap image to a byte array using the specified image format.
    /// </summary>
    /// <param name="bitmap">The bitmap image to convert into a byte array.</param>
    /// <param name="format">The image format to save the bitmap as.</param>
    /// <returns>A byte array representation of the Bitmap image.</returns>
    public static byte[] BlobFromBitmap(Bitmap bitmap, ImageFormat format) {
      
      using (MemoryStream stream = new MemoryStream()) {
        
        // Save the bitmap data to the memory stream in the specified format
        bitmap.Save(stream, format);
        
        // Convert the saved data in the stream to a byte array and return it
        return stream.ToArray();
      }
    }

    /// <summary>
    /// Converts a byte array back into a Bitmap image.
    /// </summary>
    /// <param name="blob">The byte array representing the image data.</param>
    /// <returns>A Bitmap constructed from the provided byte array.</returns>
    public static Bitmap BitmapFromBlob(byte[] blob) {
      
      using (var stream = new MemoryStream(blob.ToArray())) {
        
        // Create and return a Bitmap from the data in the memory stream
        return new Bitmap(stream);
      }
    }
  }
}
