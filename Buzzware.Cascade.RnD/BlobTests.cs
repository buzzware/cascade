using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Buzzware.Cascade.Test;
using Buzzware.Cascade.Testing;
using Buzzware.StandardExceptions;
using NUnit.Framework;
using Serilog;
using Serilog.Events;

namespace Buzzware.Cascade.RnD {
	
	public class Photo : SuperModel {

		[CascadeId]
		public int id {
			get => GetProperty(ref _id); 
			set => SetProperty(ref _id, value);
		}
		private int _id;
		
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
		
		//[BlobBelongsTo(nameof(ImagePath))]
		// public ImmutableArray<byte> Image {
		// 	get => GetProperty(ref _Image); 
		// 	set => SetProperty(ref _Image, value);
		// }
		// private ImmutableArray<byte> _Image;
		
		[FromBlob(nameof(ImagePath),typeof(DotNetBitmapConverter))]		// Avalonia-specific populate code would Bitmap.LoadAsync(basepath+ImagePath)
		public Bitmap? Image {
			get => GetProperty(ref _Image); 
			set => SetProperty(ref _Image, value);
		}
		private Bitmap? _Image;
	}

	public class DotNetBitmapConverter : IBlobConverter {
		public object? Convert(ImmutableArray<byte>? blob, Type destinationPropertyType) {
			return blob!=null ? TestUtils.BitmapFromBlob(blob.Value) : null;
		}
	}

	// public class Blob : SuperModel {
	// 	
	// 	public string? ImagePath {
	// 		get => GetProperty(ref _ImagePath); 
	// 		set => SetProperty(ref _ImagePath, value);
	// 	}
	// 	private string? _ImagePath;
	// 	
	// 	public ImmutableArray<byte> Data {
	// 		get => GetProperty(ref _Data); 
	// 		set => SetProperty(ref _Data, value);
	// 	}
	// 	private ImmutableArray<byte> _Data;
	// 	
	// }


	[TestFixture]
	public class BlobTests {
		
		private string testSourcePath;
		private string tempDir;
		private string testClassName;
		private string testName;
		
		
		MockOrigin2 origin;
		MockModelClassOrigin<Thing> thingOrigin;
		MockModelClassOrigin<Photo> photoOrigin;
		CascadeDataLayer cascade;
		private FastFileClassCache<Thing,Int32> thingFileCache;
		private FastFileClassCache<Photo,int> photoFileCache;

		[SetUp]
		public void SetUp() {
			// Log.Logger = new LoggerConfiguration()
			// 	.MinimumLevel.Is(LogEventLevel.Debug)
			// 	//.WriteTo.Console()
			// 	.WriteTo.Debug()
			// 	.CreateLogger();
			//
			
			testClassName = TestContext.CurrentContext.Test.ClassName.Split('.').Last();
			testName = TestContext.CurrentContext.Test.Name;
			testSourcePath = CascadeUtils.AboveFolderNamed(TestContext.CurrentContext.TestDirectory,"bin")!;
			tempDir = testSourcePath+$"/temp/{testClassName}.{testName}";
			
			Log.Debug($"Test tempDir {tempDir}");
			if (Directory.Exists(tempDir))
				Directory.Delete(tempDir,true);
			Directory.CreateDirectory(tempDir);

			var cascadeDir = tempDir + "/Cascade";
			
			thingOrigin = new MockModelClassOrigin<Thing>();
			photoOrigin = new MockModelClassOrigin<Photo>();
			origin = new MockOrigin2(
				new Dictionary<Type, IModelClassOrigin>() {
					{ typeof(Thing), thingOrigin },
					{ typeof(Photo), photoOrigin }
				},
				1000
			);
			thingFileCache = new FastFileClassCache<Thing, int>(cascadeDir);
			photoFileCache = new FastFileClassCache<Photo, int>(cascadeDir);
			var modelCache = new ModelCache	(
				aClassCache: new Dictionary<Type, IModelClassCache>() {
					{ typeof(Thing), thingFileCache },
					{ typeof(Photo), photoFileCache },
				},
				blobCache: new FileBlobCache(cascadeDir)
			);
			
			cascade = new CascadeDataLayer(
				origin, 
				new ICascadeCache[] { modelCache }, 
				new CascadeConfig() {StoragePath = cascadeDir},
				new MockCascadePlatform(),
				ErrorControl.Instance,
				new CascadeJsonSerialization()
			);
		}
		
		public const string TEST_PROFILE1 = "";
		public const string BLOB1_PATH = "person/123/profile/1.png";

		[Test]
		public async Task GetPutTest() {
			var bitmap1 = new Bitmap(10,10);
			var image = TestUtils.BlobFromBitmap(bitmap1,ImageFormat.Png);
			await cascade.BlobPut(BLOB1_PATH, image);
			var blob = (await cascade.BlobGet(BLOB1_PATH))!;
			var bitmap2 = TestUtils.BitmapFromBlob(blob.Value);
			Assert.That(bitmap2.Width,Is.EqualTo(bitmap1.Width));
		}
		
		[Test]
		public async Task PopulateBlobTest() {
		
			var image = TestUtils.BlobFromBitmap(new Bitmap(10,10),ImageFormat.Png);
			await cascade.BlobPut(BLOB1_PATH, image);
			var testPhoto1 = new Photo() {
				id = 1,
				Comments = "Fred",
				ImagePath = BLOB1_PATH
			};
			var testPhoto2 = await cascade.Create(testPhoto1);
			
			await cascade.Populate(testPhoto2, nameof(Photo.Image));
			Assert.That(testPhoto2.Image.Width,Is.EqualTo(10));
		
			var testPhoto3 = await cascade.Get<Photo>(testPhoto1.id, freshnessSeconds: -1, populate: new []{ nameof(Photo.Image) });
			Assert.That(testPhoto3.Image.Width,Is.EqualTo(10));
		}
	}
}
