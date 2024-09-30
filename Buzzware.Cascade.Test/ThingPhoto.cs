
using System.Drawing;
using System.Text.Json.Serialization;

namespace Buzzware.Cascade.Test {

  /// <summary>
  /// The ThingPhoto class is a data model that inherits from SuperModel, used to store and handle photo-related information such as
  /// an image, a thumbnail, and associated metadata like name and comments.
  /// </summary>
  public class ThingPhoto : SuperModel {

    /// <summary>
    /// </summary>
    /// <param name="proxyFor">Represents another ThingPhoto instance to proxy for. Defaults to null if not provided.</param>
    public ThingPhoto(ThingPhoto? proxyFor=null) : base(proxyFor) {
    }
    
    /// <summary>
    /// Default constructor for creating a ThingPhoto instance. Used primarily for JSON deserialization.
    /// </summary>
    public ThingPhoto() : base(null) {
    }
    
    /// <summary>
    /// Unique identifier for the ThingPhoto instance. Marked with [CascadeId] to signify its use as a unique key in the Cascade framework.
    /// </summary>
    [CascadeId]
    public int id {
      get => GetProperty(ref _id); 
      set => SetProperty(ref _id, value);
    }
    private int _id;
    
    /// <summary>
    /// Represents the name of the ThingPhoto. Can be used for identification or labeling purposes.
    /// </summary>
    public string? name {
      get => GetProperty(ref _name); 
      set => SetProperty(ref _name, value);
    }
    private string? _name;
    
    /// <summary>
    /// comments about the photo
    /// </summary>
    public string? Comments {
      get => GetProperty(ref _Comments); 
      set => SetProperty(ref _Comments, value);
    }
    private string? _Comments;
    
    /// <summary>
    /// File path to the image
    /// </summary>
    public string? imagePath {
      get => GetProperty(ref _ImagePath); 
      set => SetProperty(ref _ImagePath, value);
    }
    private string? _ImagePath;
    
    /// <summary>
    /// Bitmap of the image
    /// </summary>
    [FromBlob(nameof(imagePath),typeof(DotNetBitmapConverter))]
    public Bitmap? Image {
      get => GetProperty(ref _Image); 
      set => SetProperty(ref _Image, value);
    }
    private Bitmap? _Image;
    
    /// <summary>
    /// File path to the thumbnail image. Smaller version of the image.
    /// </summary>
    public string? thumbnailPath {
      get => GetProperty(ref _thumbnailPath); 
      set => SetProperty(ref _thumbnailPath, value);
    }
    private string? _thumbnailPath;
    
    /// <summary>
    /// The Bitmap representation of the thumbnail
    /// </summary>
    [FromBlob(nameof(thumbnailPath),typeof(DotNetBitmapConverter))]
    public Bitmap? Thumbnail {
      get => GetProperty(ref _Thumbnail); 
      set => SetProperty(ref _Thumbnail, value);
    }
    private Bitmap? _Thumbnail;
    
    [FromBlob(nameof(imagePath))]
    public byte[]? ImageBytes {
    	get => GetProperty(ref _ImageBytes); 
    	set => SetProperty(ref _ImageBytes, value);
    }
    private byte[] _ImageBytes;
    
    /// <summary>
    /// Bitmap representation of a converted thumbnail derived from the main image
    /// </summary>
    [FromProperty(nameof(Image),typeof(DotNetThumbnailConverter),100,100)]
    public Bitmap? ConvertedThumbnail {
      get => GetProperty(ref _ConvertedThumbnail); 
      set => SetProperty(ref _ConvertedThumbnail, value);
    }
    private Bitmap? _ConvertedThumbnail;
  }
}

