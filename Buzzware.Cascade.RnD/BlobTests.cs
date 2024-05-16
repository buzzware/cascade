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
		
		// [FromBlob(nameof(ImagePath),DotNetImageConverter)]		// Avalonia-specific populate code would Bitmap.LoadAsync(basepath+ImagePath)
		public Bitmap? Image {
			get => GetProperty(ref _Image); 
			set => SetProperty(ref _Image, value);
		}
		private Bitmap? _Image;
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
	

	public class FileBlobCache : IBlobCache {
		private readonly string _tempDir;

		public CascadeDataLayer Cascade { get; set; }

		private string FullBlobPath { get; }

		private readonly string _blobDirectory = "Blob";		
		
		public FileBlobCache(string tempDir) {
			_tempDir = tempDir;
			FullBlobPath = ToFilePath(_blobDirectory);
			Directory.CreateDirectory(FullBlobPath);
		}

		protected string ToFilePath(string path) {
			return Path.Combine(_tempDir, path);
		}

		protected string GetBlobPath(string path) { 
			return Path.Combine(_blobDirectory, path);
		}
		
		public string GetModelFilePath(string path) {
			return ToFilePath(GetBlobPath(path)); 
		}
		
		public async Task ClearAll(bool exceptHeld, DateTime? olderThan) {
			if (exceptHeld || olderThan!=null) {
				// models
				foreach (var file in Directory.GetFiles(FullBlobPath,"*",SearchOption.AllDirectories)) {
					if (olderThan != null) {
						var fileTime = File.GetLastWriteTimeUtc(file);
						if (fileTime.IsGreaterOrEqual(olderThan.Value))
							continue;
					}
					var path = CascadeUtils.GetRelativePath(FullBlobPath, file);
					if (exceptHeld) {
						if (Cascade!.IsHeldBlob(path))
							continue;
					}
					//Log.Debug($"FastFileClassCache Clear {typeof(Model).FullName} id {id}");
					CascadeUtils.EnsureFileOperationSync(() => {
						File.Delete(file);
					});
					// var cachePath = CascadeUtils.GetRelativePath(_fileDir, file);
					// cache.TryRemove(cachePath, out var removed);
				}
			} else {
				// cache.Clear();
				// Delete all files in the models directory
				foreach (var file in Directory.GetFiles(FullBlobPath)) {
					//Log.Debug($"FastFileClassCache Clear {typeof(Model).FullName} id {Path.GetFileNameWithoutExtension(file)}");
					CascadeUtils.EnsureFileOperationSync(() => {
						File.Delete(file);
					});
				}
			}
		}

		public async Task<OpResponse> Fetch(RequestOp requestOp) {
	    if (requestOp.Verb != RequestVerb.BlobGet)
	        throw new Exception("requestOp.Verb != Blob");
	    bool exists;
	    long arrivedAtMs;
	    
      var path = requestOp.Id as string;
      if (path == null)
          throw new Exception("Id must be a string");

      string blobFilePath = GetModelFilePath(path);
      exists = File.Exists(blobFilePath);
      arrivedAtMs = exists ? CascadeUtils.toUnixMilliseconds(File.GetLastWriteTimeUtc(blobFilePath)) : -1;
      if (
          exists && 
          requestOp.FreshnessSeconds>=0 && 
          (requestOp.FreshnessSeconds==CascadeDataLayer.FRESHNESS_ANY || ((Cascade.NowMs-arrivedAtMs) <= requestOp.FreshnessSeconds*1000))
      ) {
          var loaded = await LoadBlob(blobFilePath);
          return new OpResponse(
              requestOp,
              Cascade?.NowMs ?? 0,
              connected: true,
              exists: true,
              result: loaded,
              arrivedAtMs: arrivedAtMs
          );
      } else {
          return OpResponse.None(requestOp, Cascade.NowMs, this.GetType().Name);
      }
		}
		
		public async Task Store(OpResponse opResponse) {
			var path = opResponse.RequestOp.Id as string;
			long arrivedAt = opResponse.ArrivedAtMs ?? Cascade.NowMs;
			
			if (path == null)
				throw new Exception("Bad path");
			try {
				string modelFilePath = GetModelFilePath(path)!;
				if (opResponse.ResultIsEmpty()) {
					File.Delete(modelFilePath);
				} else {
					if (!(opResponse.Result is ImmutableArray<byte>))
						throw new ArgumentException("Result must be null or ImmutableArray<byte>");
					await StoreBlob(modelFilePath, (ImmutableArray<byte>) opResponse.Result, arrivedAt);
				}
			} catch (Exception e) {
				Log.Debug(e.Message);   // sharing violation exception sometimes happens here
			}
		}

		private async Task<ImmutableArray<byte>?> LoadBlob(string path) {
			if (!File.Exists(path))
				return null;

			byte[] result;
			using (FileStream stream = File.OpenRead(path)) {
				result = new byte[stream.Length];
				await stream.ReadAsync(result, 0, (int)stream.Length);
			}
			return result.ToImmutableArray();			
		}
		
		private async Task StoreBlob(string path, ImmutableArray<byte> blob, long arrivedAt) {
			await Task.Run(async () => {
				if (!Directory.Exists(Path.GetDirectoryName(path)))
					Directory.CreateDirectory(Path.GetDirectoryName(path)!);
				
				using FileStream fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 256*1024, true);
				await fileStream.WriteAsync(blob.ToArray(), 0, blob.Length).ConfigureAwait(false);
				
				File.SetLastWriteTimeUtc(path, CascadeUtils.fromUnixMilliseconds(arrivedAt));
			});
		}
	}

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
		
		// [Test]
		// public async Task PopulateBlobTest() {
		//
		// 	var image = TestUtils.BlobFromBitmap(new Bitmap(10,10),ImageFormat.Png);
		// 	await cascade.BlobPut(BLOB1_PATH, image);
		// 	var testPhoto1 = new Photo() {
		// 		id = 1,
		// 		Comments = "Fred",
		// 		ImagePath = BLOB1_PATH
		// 	};
		// 	var testPhoto2 = await cascade.Create(testPhoto1);
		// 	
		// 	await cascade.Populate(testPhoto2, nameof(Photo.Image));
		// 	Assert.That(testPhoto2.Image.Width,Is.EqualTo(10));
		//
		// 	var testPhoto3 = await cascade.Get<Photo>(testPhoto1.id, freshnessSeconds: -1, populate: new []{ nameof(Photo.Image) });
		// 	Assert.That(testPhoto3.Image.Width,Is.EqualTo(10));
		// }
	}
}
