using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Buzzware.Cascade.Testing;
using Buzzware.StandardExceptions;
using NUnit.Framework;
using Serilog;

namespace Buzzware.Cascade.Test {

  /// <summary>
  /// Test suite for validating the Blob functionalities of the Cascade library.
  /// </summary>
  [TestFixture]
  public class BlobTests {
    
    private string testSourcePath;
    private string tempDir;
    private string testClassName;
    private string testName;
    
    MockOrigin2 origin;
    MockModelClassOrigin<Thing> thingOrigin;
    MockModelClassOrigin<ThingPhoto> photoOrigin;
    CascadeDataLayer cascade;
    private FastFileClassCache<Thing,Int32> thingFileCache;
    private FastFileClassCache<ThingPhoto,int> photoFileCache;
    private ModelCache modelCache;
    private FileBlobCache blobCache;

    /// <summary>
    /// Sets up the testing environment before each test.
    /// Initializes mock objects and file caches, and prepares the directory structure required for testing.
    /// </summary>
    [SetUp]
    public void SetUp() {
      
      // Initializes the test class and method names for directory creation
      testClassName = TestContext.CurrentContext.Test.ClassName.Split('.').Last();
      testName = TestContext.CurrentContext.Test.Name;
      
      // Determines the test source path and the temporary directory path
      testSourcePath = CascadeUtils.AboveFolderNamed(TestContext.CurrentContext.TestDirectory,"bin")!;
      tempDir = testSourcePath+$"/temp/{testClassName}.{testName}";
      
      Log.Debug($"Test tempDir {tempDir}");
      
      // Cleans up any existing directory and creates a new one for the test
      if (Directory.Exists(tempDir))
        Directory.Delete(tempDir,true);
      Directory.CreateDirectory(tempDir);

      var cascadeDir = tempDir + "/Cascade";
      
      // Initializes mock origins and file caches for testing
      thingOrigin = new MockModelClassOrigin<Thing>();
      photoOrigin = new MockModelClassOrigin<ThingPhoto>();
      origin = new MockOrigin2(
        new Dictionary<Type, IModelClassOrigin>() {
          { typeof(Thing), thingOrigin },
          { typeof(ThingPhoto), photoOrigin }
        },
        1000
      );
      thingFileCache = new FastFileClassCache<Thing, int>(cascadeDir);
      photoFileCache = new FastFileClassCache<ThingPhoto, int>(cascadeDir);
      blobCache = new FileBlobCache(cascadeDir);
      modelCache = new ModelCache(
        aClassCache: new Dictionary<Type, IModelClassCache>() {
          { typeof(Thing), thingFileCache },
          { typeof(ThingPhoto), photoFileCache },
        },
        blobCache: blobCache
      );
      
      // Configures the CascadeDataLayer with the initialized components
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

    /// <summary>
    /// Tests the ability to store and retrieve a blob using the Cascade Blob storage methods.
    /// Validates that a blob can be stored and then correctly retrieved, confirming the integrity of data.
    /// </summary>
    [Test]
    public async Task GetPutTest() {
      
      // Creates a bitmap image and converts it to a blob
      var bitmap1 = new Bitmap(10,10);
      var image = TestUtils.BlobFromBitmap(bitmap1,ImageFormat.Png);
      
      // Stores the blob using CascadeDataLayer
      await cascade.BlobPut(BLOB1_PATH, image);
      
      // Retrieves the blob and converts it back to a bitmap
      var blob = (await cascade.BlobGet(BLOB1_PATH))!;
      var bitmap2 = TestUtils.BitmapFromBlob(blob);
      
      // Asserts that the retrieved bitmap dimensions are the same as the original
      Assert.That(bitmap2.Width,Is.EqualTo(bitmap1.Width));
    }
    
    /// <summary>
    /// Validates the functionality of populating a model property with a blob.
    /// Ensures that a blob stored as a path in a model can be correctly populated into the model's property.
    /// </summary>
    [Test]
    public async Task PopulateBlobTest() {
      
      // Stores a blob image
      var image = TestUtils.BlobFromBitmap(new Bitmap(10,10),ImageFormat.Png);
      await cascade.BlobPut(BLOB1_PATH, image);
      
      // Creates a ThingPhoto object and sets its image path to the stored blob
      var testPhoto1 = new ThingPhoto() {
        id = 1,
        Comments = "Fred",
        imagePath = BLOB1_PATH
      };
      
      // Adds the ThingPhoto object to the CascadeDataLayer
      var testPhoto2 = await cascade.Create(testPhoto1);
      
      // Populates the Image property of ThingPhoto with the blob data
      await cascade.Populate(testPhoto2, nameof(ThingPhoto.Image));
      Assert.That(testPhoto2.Image.Width,Is.EqualTo(10));
      
      // Retrieves the ThingPhoto object and ensures the Image property is populated
      var testPhoto3 = await cascade.Get<ThingPhoto>(testPhoto1.id, freshnessSeconds: -1, populate: new []{ nameof(ThingPhoto.Image) });
      Assert.That(testPhoto3.Image.Width,Is.EqualTo(10));
    }
    
    /// <summary>
    /// Tests the conversion of an image to a thumbnail using [FromProperty].
    /// Ensures that a thumbnail can be generated and assigned based on an existing image property.
    /// </summary>
    [Test]
    public async Task SimpleConvertedThumbnail() {
      
      // Initializes a bitmap to be used as the image
      var bitmap = new Bitmap(10, 10);
      
      // Creates a ThingPhoto object with the initialized bitmap as its Image
      var diaryPhoto = new ThingPhoto() {
        Image = bitmap
      };
      
      // Populates the ConvertedThumbnail property of ThingPhoto from the original image
      await cascade.Populate(diaryPhoto, nameof(ThingPhoto.ConvertedThumbnail));
      Assert.That(diaryPhoto.ConvertedThumbnail,Is.Not.Null);
    }
    
    /// <summary>
    /// Tests the FromBlob functionality when updating the path, ensuring it correctly populates the association.
    /// </summary>
    [Test]
    public async Task FromBlob_UpdatePath_PopulatesAssociation()
    {
      // Arrange
      var bitmap1 = new Bitmap(10, 10);
      var image1 = TestUtils.BlobFromBitmap(bitmap1, ImageFormat.Png);
      await cascade.BlobPut("path/to/image1.png", image1);
      var bitmap2 = new Bitmap(20, 20);
      var image2 = TestUtils.BlobFromBitmap(bitmap2, ImageFormat.Png);
      await cascade.BlobPut("path/to/image2.png", image2);

      var thingPhoto = new ThingPhoto { id = 1, imagePath = "path/to/image1.png" };
      thingPhoto = await cascade.Create(thingPhoto);

      // Act
      await cascade.Populate(thingPhoto, nameof(ThingPhoto.Image));

      // Assert
      Assert.That(thingPhoto.imagePath, Is.EqualTo("path/to/image1.png"));
      Assert.That(thingPhoto.Image!.Width, Is.EqualTo(bitmap1.Width));

      // Act
      thingPhoto = await cascade.Update(thingPhoto, new Dictionary<string, object?>() { [nameof(ThingPhoto.imagePath)] = "path/to/image2.png" });

      // Assert
      Assert.That(thingPhoto.imagePath, Is.EqualTo("path/to/image2.png"));
      Assert.That(thingPhoto.Image!.Width, Is.EqualTo(bitmap2.Width));
    }

    [Test]
    public async Task PopulateThumbnailBytesTest() {
      // Arrange
      var bitmap = new Bitmap(10, 10);
      var image = TestUtils.BlobFromBitmap(bitmap, ImageFormat.Png);
      await cascade.BlobPut(BLOB1_PATH, image);

      var thingPhoto = new ThingPhoto {
        id = 1,
        imagePath = BLOB1_PATH
      };
      await cascade.Create(thingPhoto);

      // Act
      await cascade.Populate(thingPhoto, nameof(ThingPhoto.ImageBytes));

      // Assert
      Assert.That(thingPhoto.ImageBytes, Is.Not.Null);
      Assert.That(thingPhoto.ImageBytes.Length, Is.GreaterThan(0));
    }

    
    
    [Test]
    public async Task EtagTest() {
      OpResponse? response = null;
      var blob11 = TestUtils.NewBlob(11,100);
      await origin.ProcessRequest(RequestOp.BlobPutOp(BLOB1_PATH, cascade.NowMs, blob11).CloneWith(eTag: "11"), true);
      var thingPhoto = new ThingPhoto {
        id = 1,
        imagePath = BLOB1_PATH
      };
      await cascade.Create(thingPhoto);

      // Act
      await cascade.Populate(thingPhoto, nameof(ThingPhoto.ImageBytes));

      // Assert
      Assert.That(thingPhoto.ImageBytes, Is.EquivalentTo(blob11));

      origin.NowMs += 1000;

      // any freshness, so should come from cache as normal
      response = await cascade.BlobGetResponse(BLOB1_PATH, freshnessSeconds: RequestOp.FRESHNESS_ANY);
      Assert.That(response.Result, Is.EquivalentTo(blob11));
      Assert.That(response.SourceName, Is.EqualTo("FileBlobCache"));
      
      // freshest, but etag should match and we still get cache version
      response = await cascade.BlobGetResponse(BLOB1_PATH, freshnessSeconds: RequestOp.FRESHNESS_FRESHEST);
      Assert.That(response.Result, Is.EquivalentTo(blob11));
      Assert.That(response.SourceName, Is.EqualTo("FileBlobCache"));
      // check cache ArrivedAtMs has been updated
      response = await blobCache.Fetch(RequestOp.BlobGetOp(BLOB1_PATH, freshnessSeconds: RequestOp.FRESHNESS_ANY));
      Assert.That(response.ArrivedAtMs, Is.EqualTo(origin.NowMs));
      
      // change origin version
      var blob12 = TestUtils.NewBlob(12,100);
      response = await origin.ProcessRequest(RequestOp.BlobPutOp(BLOB1_PATH, cascade.NowMs, blob12).CloneWith(eTag: "12"), true);

      origin.NowMs += 1000;
      
      // origin changed but with freshness any we still get cache version
      response = await cascade.BlobGetResponse(BLOB1_PATH, freshnessSeconds: RequestOp.FRESHNESS_ANY);
      Assert.That(response.Result, Is.EquivalentTo(blob11));
      Assert.That(response.SourceName, Is.EqualTo("FileBlobCache"));
      
      origin.NowMs += 1000;
      
      // freshest, and etag has changed so get origin version
      response = await cascade.BlobGetResponse(BLOB1_PATH, freshnessSeconds: RequestOp.FRESHNESS_FRESHEST);
      Assert.That(response.Result, Is.EquivalentTo(blob12));
      Assert.That(response.SourceName, Is.EqualTo("MockOrigin2"));
    }
  }
}

