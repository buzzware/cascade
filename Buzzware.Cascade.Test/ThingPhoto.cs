using System.Drawing;
using System.Text.Json.Serialization;

namespace Buzzware.Cascade.Test {
	public class ThingPhoto : SuperModel {

		// for setting proxyFor
		public ThingPhoto(ThingPhoto? proxyFor=null) : base(proxyFor) {
		}
		
		// for JSON deserialize
		public ThingPhoto() : base(null) {
		}
		
		[CascadeId]
		public int id {
			get => GetProperty(ref _id); 
			set => SetProperty(ref _id, value);
		}
		private int _id;
		
		public string? name {
			get => GetProperty(ref _name); 
			set => SetProperty(ref _name, value);
		}
		private string? _name;
		
		public string? Comments {
			get => GetProperty(ref _Comments); 
			set => SetProperty(ref _Comments, value);
		}
		private string? _Comments;
		
		public string? ImagePath {
			get => GetProperty(ref _ImagePath); 
			set => SetProperty(ref _ImagePath, value);
		}
		private string? _ImagePath;
		
		[FromBlob(nameof(ImagePath),typeof(DotNetBitmapConverter))]
		public Bitmap? Image {
			get => GetProperty(ref _Image); 
			set => SetProperty(ref _Image, value);
		}
		private Bitmap? _Image;
		
		public string? thumbnailPath {
			get => GetProperty(ref _thumbnailPath); 
			set => SetProperty(ref _thumbnailPath, value);
		}
		private string? _thumbnailPath;
	
		[JsonIgnore]
		[FromBlob(nameof(thumbnailPath),typeof(DotNetBitmapConverter))]
		public Bitmap? Thumbnail {
			get => GetProperty(ref _Thumbnail); 
			set => SetProperty(ref _Thumbnail, value);
		}
		private Bitmap? _Thumbnail;
		
		[FromProperty(nameof(Image),typeof(DotNetThumbnailConverter),100,100)]
		public Bitmap? ConvertedThumbnail {
			get => GetProperty(ref _ConvertedThumbnail); 
			set => SetProperty(ref _ConvertedThumbnail, value);
		}
		private Bitmap? _ConvertedThumbnail;
	}
}
