
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Buzzware.Cascade.Testing;
using NUnit.Framework;
using Serilog;
using Buzzware.StandardExceptions;

namespace Buzzware.Cascade.Test {
  /// <summary>
  /// Test suite for verifying the handling of pending changes in the Cascade library.
  /// Includes tests for serialization and deserialization of create, update, delete, and blob operations.
  /// </summary>
  [TestFixture]
  public class PendingChangesTests {

    // Mock origin server for simulating responses.
    MockOrigin origin;
    private CascadeDataLayer cascade;
    private string tempDir;
    private CascadeJsonSerialization serialization;

    /// <summary>
    /// Setup method to initialize resources required for testing, like creating a temporary directory and CascadeDataLayer instance.
    /// </summary>
    [SetUp]
    public void SetUp() {
      // Create a temporary directory and initialize logging.
      tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
      Log.Debug($"Buzzware.Cascade cache directory {tempDir}");
      Directory.CreateDirectory(tempDir);

      // Initialize the JSON serialization component.
      serialization = new CascadeJsonSerialization();

      // Setup a mock origin to handle request operations.
      origin = new MockOrigin(nowMs: 1000, handleRequest: (origin, requestOp) => {
        var nowMs = origin.NowMs;
        var thing = new Parent() {
          id = requestOp.IdAsInt ?? 0
        };
        thing.updatedAtMs = requestOp.TimeMs;
        return Task.FromResult(new OpResponse(
          requestOp: requestOp,
          nowMs,
          connected: true,
          exists: true,
          result: thing,
          arrivedAtMs: nowMs
        ));
      });

      // Instantiate a new CascadeDataLayer with a mock origin and configuration.
      cascade = new CascadeDataLayer(
        origin,
        new ICascadeCache[] { }, 
        new CascadeConfig() { StoragePath = tempDir }, 
        new MockCascadePlatform(), 
        ErrorControl.Instance, 
        serialization
      );
    }

    /// <summary>
    /// Cleans up resources by deleting the temporary directory after tests are completed.
    /// </summary>
    [TearDown]
    public void TearDown() {
      if (Directory.Exists(tempDir)) {
        Directory.Delete(tempDir, true);
      }
    }

    /// <summary>
    /// Tests the serialization and deserialization process of a Create operation in Cascade.
    /// Ensures that the serialized JSON string matches the expected structure and can be accurately deserialized back.
    /// </summary>
    [Test]
    public async Task CreateSerialisation() {
      var thing = new Thing() {
        id = 3,
        colour = "brown"
      };

      // Create a Create operation request.
      var op = RequestOp.CreateOp(thing, cascade.NowMs);

      // Serialize the request operation.
      var szn = cascade.SerializeRequestOp(op, out var externalContent);

      // Convert serialized object to JSON string and log it.
      var sz = szn.ToJsonString();
      Log.Debug(sz);

      // Expected JSON string for validation.
      const string expected = "{\"Verb\":\"Create\",\"Type\":\"Buzzware.Cascade.Testing.Thing\",\"Id\":3,\"TimeMs\":1000,\"Value\":{\"id\":3,\"name\":null,\"colour\":\"brown\"}}";
      Assert.That(sz, Is.EqualTo(expected));

      // Deserialize the JSON string back to operation.
      var op2 = cascade.DeserializeRequestOp(sz, out var externals);
      
      // Verify the deserialized object matches the original operation details.
      Assert.That(op2.Verb, Is.EqualTo(RequestVerb.Create));
      Assert.That(op2.Id, Is.EqualTo(thing.id));
      Assert.That(op2.Type, Is.EqualTo(typeof(Thing)));
      Assert.That(op2.TimeMs, Is.EqualTo(op.TimeMs));
      Assert.That(((Thing)op2.Value).id, Is.EqualTo(thing.id));
      Assert.That(((Thing)op2.Value).colour, Is.EqualTo(thing.colour));
    }
    
    /// <summary>
    /// Validates the serialization and deserialization of an Update operation.
    /// Checks field change tracking and ensures that serialization process maintains data integrity.
    /// </summary>
    [Test]
    public async Task UpdateSerialisation() {
      var thing = new Thing() {
        id = 5,
        colour = "brown",
        name = "Boris"
      };

      // Define changes for the update operation.
      var changes = new Dictionary<string, object?> {
        { "colour", "blue" },
        { "name", "Winston" }
      };

      // Create an Update operation request.
      var op = RequestOp.UpdateOp(thing, changes, cascade.NowMs);
      
      // Serialize the update operation.
      var szn = cascade.SerializeRequestOp(op, out var externalContent);

      // Log the serialized JSON string.
      var sz = szn.ToJsonString();
      Log.Debug(sz);

      // Expected JSON string for validation.
      const string expected = "{\"Verb\":\"Update\",\"Type\":\"Buzzware.Cascade.Testing.Thing\",\"Id\":5,\"TimeMs\":1000,\"Value\":{\"colour\":\"blue\",\"name\":\"Winston\"},\"Extra\":{\"id\":5,\"name\":\"Boris\",\"colour\":\"brown\"}}";
      Assert.That(sz, Is.EqualTo(expected));

      // Deserialize the JSON string back to operation.
      var op2 = cascade.DeserializeRequestOp(sz, out var externals);

      // Validate that the deserialized operation matches the original changes.
      Assert.That(op2.Verb, Is.EqualTo(RequestVerb.Update));
      Assert.That(op2.Id, Is.EqualTo(thing.id));
      Assert.That(op2.Type, Is.EqualTo(typeof(Thing)));
      Assert.That(op2.TimeMs, Is.EqualTo(op.TimeMs));
      var opChanges = op2.Value as Dictionary<string, object?>;
      Assert.That(opChanges["colour"], Is.EqualTo(changes["colour"]));
      Assert.That(opChanges["name"], Is.EqualTo(changes["name"]));
    }

    /// <summary>
    /// Tests the serialization and deserialization of a Destroy operation in Cascade.
    /// Ensures that resources are accurately represented and destroyed in serialized form.
    /// </summary>
    [Test]
    public async Task DestroySerialisation() {
      var thing = new Thing() {
        id = 5,
        colour = "brown",
        name = "Boris"
      };

      // Create a Destroy operation request.
      var op = RequestOp.DestroyOp(thing, cascade.NowMs);

      // Serialize the destroy operation.
      var szn = cascade.SerializeRequestOp(op, out var externalContent);

      // Log the serialized JSON string.
      var sz = szn.ToJsonString();
      Log.Debug(sz);

      // Expected JSON string for validation.
      const string expected = "{\"Verb\":\"Destroy\",\"Type\":\"Buzzware.Cascade.Testing.Thing\",\"Id\":5,\"TimeMs\":1000,\"Value\":{\"id\":5,\"name\":\"Boris\",\"colour\":\"brown\"}}";
      Assert.That(sz, Is.EqualTo(expected));

      // Deserialize the JSON string back to operation.
      var op2 = cascade.DeserializeRequestOp(sz, out var externals);

      // Validate that the deserialized operation matches the original destroy intent.
      Assert.That(op2.Verb, Is.EqualTo(RequestVerb.Destroy));
      Assert.That(op2.Id, Is.EqualTo(thing.id));
      Assert.That(op2.Type, Is.EqualTo(typeof(Thing)));
      Assert.That(op2.TimeMs, Is.EqualTo(op.TimeMs));
    }

    /// <summary>
    /// Helper method to assert that two RequestOp objects match in various aspects.
    /// Verifies that both operations have the same properties and values.
    /// </summary>
    public static void AssertRequestOpsMatch(RequestOp op1, RequestOp op2, bool checkValue) {
      Assert.That(op2.Verb, Is.EqualTo(op1.Verb));
      Assert.That(op2.Id, Is.EqualTo(op1.Id));
      Assert.That(op2.Type, Is.EqualTo(op1.Type));
      Assert.That(op2.TimeMs, Is.EqualTo(op1.TimeMs));
      if (checkValue)
        Assert.That(op2.Value != null, Is.EqualTo(op1.Value != null));
      Assert.That(op2.Key, Is.EqualTo(op1.Key));

      Assert.That(op2.Populate, Is.EqualTo(op1.Populate));
      Assert.That(op2.FreshnessSeconds, Is.EqualTo(op1.FreshnessSeconds));
      Assert.That(op2.PopulateFreshnessSeconds, Is.EqualTo(op1.PopulateFreshnessSeconds));
      Assert.That(op2.FallbackFreshnessSeconds, Is.EqualTo(op1.FallbackFreshnessSeconds));
      Assert.That(op2.Hold, Is.EqualTo(op1.Hold));
      Assert.That(op2.Criteria, Is.EqualTo(op1.Criteria));
      Assert.That(op2.Params, Is.EqualTo(op1.Params));
    }

    /// <summary>
    /// Tests the serialization and deserialization of a BlobPut operation in Cascade.
    /// Ensures binary data is handled correctly and stored externally when necessary.
    /// </summary>
    [Test]
    public async Task BlobPutOpSerialisation() {
      // Create a blob from a bitmap and create a BlobPut operation.
      var image = TestUtils.BlobFromBitmap(new Bitmap(10, 10), ImageFormat.Png);
      var op = RequestOp.BlobPutOp("first/second/happy_snap", cascade.NowMs, image);
      
      // Assert initial properties of the BlobPut operation.
      Assert.That(op.Verb, Is.EqualTo(RequestVerb.BlobPut));
      Assert.That(op.Id, Is.EqualTo("first/second/happy_snap"));
      Assert.That(op.Type, Is.EqualTo(typeof(byte[])));
      Assert.That(op.TimeMs, Is.EqualTo(cascade.NowMs));
      Assert.That(op.Value, Is.EqualTo(image));
      Assert.That(op.Populate, Is.EqualTo(null));
      Assert.That(op.FreshnessSeconds, Is.EqualTo(RequestOp.FRESHNESS_DEFAULT));
      Assert.That(op.PopulateFreshnessSeconds, Is.EqualTo(RequestOp.FRESHNESS_DEFAULT));
      Assert.That(op.FallbackFreshnessSeconds, Is.EqualTo(RequestOp.FRESHNESS_ANY));
      Assert.That(op.Hold, Is.False);
      Assert.That(op.Criteria, Is.EqualTo(null));
      Assert.That(op.Params, Is.EqualTo(null));
      
      // Serialize the BlobPut operation and extract a JSON string.
      var szn = cascade.SerializeRequestOp(op, out var externalContent);
      var sz = szn.ToJsonString();
      Log.Debug(sz);

      // Validate the serialized JSON string.
      const string expected = "{\"Verb\":\"BlobPut\",\"Type\":\"System.Byte[]\",\"Id\":\"first/second/happy_snap\",\"TimeMs\":1000,\"Value\":null}";
      Assert.That(sz, Is.EqualTo(expected));
      Assert.That(externalContent.Count, Is.EqualTo(1));
      Assert.That(externalContent[nameof(RequestOp.Value)], Is.EqualTo(image));

      // Prepare external paths for deserialization and deserialize the operation.
      const string mainFilename = "00000000001000.json";
      var externals = new JsonObject();
      externals["Value"] = cascade.ExternalBinaryPathFromPendingChangePath(mainFilename, "Value");
      szn["externals"] = externals;
      var op2 = cascade.DeserializeRequestOp(szn.ToJsonString(), out var dzexternals);

      // Assert that the deserialized operation matches the initial operation properties except for the Value.
      AssertRequestOpsMatch(op, op2, checkValue: false);
      Assert.That(op2.Value, Is.Null);
      Assert.That(dzexternals, Has.Count.EqualTo(1));
      Assert.That(dzexternals["Value"], Is.EqualTo("00000000001000__Value.bin"));
    }
    
    /// <summary>
    /// Tests the serialization and deserialization of a BlobDestroy operation.
    /// Verifies that destroying blob operations are correctly managed in serialized form.
    /// </summary>
    [Test]
    public async Task BlobDestroySerialisation() {
      // Define a blob ID and create a BlobDestroy operation.
      string blobId = "first/second/happy_snap_deleted";
      var op = RequestOp.BlobDestroyOp(blobId, cascade.NowMs);

      // Assert properties of the BlobDestroy operation.
      Assert.That(op.Verb, Is.EqualTo(RequestVerb.BlobDestroy));
      Assert.That(op.Id, Is.EqualTo(blobId));
      Assert.That(op.Type, Is.EqualTo(typeof(byte[]))); // Type is typically byte[] for blob operations.
      Assert.That(op.TimeMs, Is.EqualTo(cascade.NowMs));
      Assert.That(op.Value, Is.EqualTo(null));

      // Serialize the BlobDestroy operation and log the JSON.
      var szn = cascade.SerializeRequestOp(op, out var externalContent);
      var sz = szn.ToJsonString();
      Log.Debug(sz);

      // Validate the serialized JSON string.
      const string expected = "{\"Verb\":\"BlobDestroy\",\"Type\":\"System.Byte[]\",\"Id\":\"first/second/happy_snap_deleted\",\"TimeMs\":1000,\"Value\":null}";
      Assert.That(sz, Is.EqualTo(expected));

      // Deserialize the operation and verify it matches the original.
      var op2 = cascade.DeserializeRequestOp(sz, out var externals);
      Assert.That(op2.Verb, Is.EqualTo(op.Verb));
      Assert.That(op2.Id, Is.EqualTo(op.Id));
      Assert.That(op2.TimeMs, Is.EqualTo(op.TimeMs));
    }
    
    /// <summary>
    /// Tests the behavior of the Cascade library when handling pending changes involving blob data.
    /// Verifies pending changes serialization, storage, retrieval, and cleanup.
    /// </summary>
    [Test]
    public async Task BlobPendingChangesSerialisation() {
      // Simulate offline connection and ensure pending change directory exists.
      cascade.ConnectionOnline = false;
      if (!Directory.Exists(cascade.Config.PendingChangesPath))
        Directory.CreateDirectory(cascade.Config.PendingChangesPath);
      Log.Debug(cascade.Config.PendingChangesPath);
      
      // Create image blob data and corresponding BlobPut operation.
      var image = new Bitmap(100, 100);
      var imageBlob = TestUtils.BlobFromBitmap(image, ImageFormat.Png);
      var imagePath = "first/second/happy_snap";
      var imageOp = RequestOp.BlobPutOp(imagePath, cascade.NowMs, imageBlob);
      await cascade.AddPendingChange(imageOp);

      // Create thumbnail blob data and corresponding BlobPut operation.
      var thumbnail = new Bitmap(10, 10);
      var thumbnailBlob = TestUtils.BlobFromBitmap(thumbnail, ImageFormat.Png);
      var thumbnailPath = "first/second/happy_snap.thumb10";
      var thumbnailOp = RequestOp.BlobPutOp(thumbnailPath, cascade.NowMs, thumbnailBlob);
      await cascade.AddPendingChange(thumbnailOp);

      // Create a ThingPhoto object and serialize its creation operation.
      var thing = new ThingPhoto() {
        id = 3,
        name = "happy snap",
        Image = image,
        imagePath = imagePath,
        Thumbnail = thumbnail,
        thumbnailPath = thumbnailPath
      };
      var thingOp = RequestOp.CreateOp(thing, cascade.NowMs);
      await cascade.AddPendingChange(thingOp);

      // Retrieve pending changes from the cascade.
      var changes = await cascade.GetChangesPending();
      
      // Assert that all three changes have been added and their serialized forms match expectations.
      Assert.That(changes.Count, Is.EqualTo(3));
      RequestOp change;
      string filename;
      
      filename = changes[0].Item1;
      change = changes[0].Item2;
      Assert.That(filename, Does.Match("^[0-9]+\\.json$"));
      AssertRequestOpsMatch(imageOp, change, checkValue: false);
      Assert.That((byte[])change.Value!, Is.EqualTo((byte[])imageOp.Value!).AsCollection);
      
      filename = changes[1].Item1;
      change = changes[1].Item2;
      Assert.That(filename, Does.Match("^[0-9]+\\.json$"));
      AssertRequestOpsMatch(thumbnailOp, change, checkValue: false);
      Assert.That((byte[])change.Value!, Is.EqualTo((byte[])thumbnailOp.Value!).AsCollection);
      
      filename = changes[2].Item1;
      change = changes[2].Item2;
      Assert.That(filename, Does.Match("^[0-9]+\\.json$"));
      AssertRequestOpsMatch(thingOp, change, checkValue: false);
      Assert.That(change.Value, Is.InstanceOf<ThingPhoto>());

      // Remove all pending changes and ensure the directory is clean.
      foreach (var ch in changes) {
        await cascade.RemoveChangePending(ch.Item1, ch.Item3?.Values);
      }
      var items = Directory.GetFiles(cascade.Config.PendingChangesPath);
      Assert.That(items.Length, Is.Zero);
    }
    
    /// <summary>
    /// Tests enqueuing multiple create and update operations into the pending changes.
    /// Ensures that changes are correctly serialized, stored, retrieved, and identifiable by filenames.
    /// </summary>
    [Test]
    public async Task EnqueueOperation() {
      // Create a parent and corresponding Create operation.
      var parent = new Parent() {
        id = 3,
        colour = "red"
      };
      RequestOp requestOpParent = RequestOp.CreateOp(parent, cascade.NowMs);

      // Create a child and corresponding Create operation.
      var child1 = new Child() {
        id = "c1",
        Parent = parent,
        age = 7,
        weight = 55
      };
      RequestOp requestOpChild1 = RequestOp.CreateOp(child1, cascade.NowMs);

      // Create another child and corresponding Create operation.
      var child2 = new Child() {
        id = "c2",
        Parent = parent,
        age = 11,
        weight = 77
      };
      RequestOp requestOpChild2 = RequestOp.CreateOp(child2, cascade.NowMs);

      // Add the parent operation to pending changes and validate the resulting file.
      var filepathParent = await cascade.AddPendingChange(requestOpParent);
      Assert.That(Path.GetFileName(filepathParent), Is.EqualTo("000000000001000.json"));
      Assert.That(Directory.GetParent(filepathParent)!.Name, Is.EqualTo("PendingChanges"));
      var parentOp = cascade.DeserializeRequestOp(File.ReadAllText(filepathParent), out var externals1);
      Assert.That(parentOp.Id, Is.EqualTo(3));
      
      // Add the first child operation to pending changes and validate it.
      var filepathChild1 = await cascade.AddPendingChange(requestOpChild1);
      Assert.That(Path.GetFileName(filepathChild1), Is.EqualTo("000000000001001.json"));
      var childOp1 = cascade.DeserializeRequestOp(File.ReadAllText(filepathChild1), out var externals2);
      Assert.That(childOp1.Id, Is.EqualTo("c1"));
      
      // Add the second child operation to pending changes and increment timestamp.
      var filepathChild2 = await cascade.AddPendingChange(requestOpChild2);
      Assert.That(Path.GetFileName(filepathChild2), Is.EqualTo("000000000001002.json"));
      origin.NowMs += 111;

      // Create and serialize an additional child creation followed by an update operation.
      var child3 = new Child() {
        id = "c3",
        Parent = parent,
        age = 17,
        weight = 99
      };
      RequestOp requestOpChild3 = RequestOp.CreateOp(child3, cascade.NowMs);
      var filepathChild3 = await cascade.AddPendingChange(requestOpChild3);
      Assert.That(Path.GetFileName(filepathChild3), Is.EqualTo("000000000001111.json"));

      RequestOp updateOpChild3 = RequestOp.UpdateOp(child3, ImmutableDictionary<string, object?>.Empty.Add("age", 18), cascade.NowMs);
      var filepathChild3Update = await cascade.AddPendingChange(updateOpChild3);
      Assert.That(Path.GetFileName(filepathChild3Update), Is.EqualTo("000000000001112.json"));
      var updateOpLoaded = cascade.DeserializeRequestOp(File.ReadAllText(filepathChild3Update), out var externals);
      Assert.That(updateOpLoaded.Id, Is.EqualTo("c3"));

      // Verify the changes pending list reflects all operations.
      var changesPending = cascade.GetChangesPendingList();
      Assert.That(changesPending, Is.EquivalentTo(new string[] {
        Path.GetFileName(filepathParent),
        Path.GetFileName(filepathChild1),
        Path.GetFileName(filepathChild2),
        Path.GetFileName(filepathChild3),
        Path.GetFileName(filepathChild3Update)
      }));
    }
  }
}

