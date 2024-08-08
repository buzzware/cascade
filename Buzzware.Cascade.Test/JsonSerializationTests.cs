
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading.Tasks;
using Buzzware.Cascade.Testing;
using NUnit.Framework;
using Serilog;

namespace Buzzware.Cascade.Test {

  /// <summary>
  /// Tests related to JSON serialization functionality of the Cascade library.
  /// </summary>
  [TestFixture]
  public class JsonSerializationTests {
    private CascadeJsonSerialization sz;

    /// <summary>
    /// Sets up the test environment by initializing the CascadeJsonSerialization instance
    /// with specific configurations to ignore underscore properties and associations.
    /// </summary>
    [SetUp]
    public void SetUp() {
      sz = new CascadeJsonSerialization(ignoreUnderscoreProperties: true, ignoreAssociations: true);
    }

    /// <summary>
    /// Tests the serialization of a simple object and ensures that specific properties,
    /// including any ignored and association properties, are not included in the serialized output.
    /// </summary>
    [Test]
    public async Task Simple() {
      // Create and serialize a child object
      var child = new Child() { age = 25 };
      var output = sz.SerializeToNode(child);

      // Validate that specific properties are not included in serialization
      Assert.That(output.HasKey(nameof(SuperModel.__mutable)), Is.False);
      Assert.That(output.HasKey(nameof(Child.Parent)), Is.False);
      Assert.That(output.Keys, Is.EquivalentTo(new string[] { "id", "parentId", "weight", "power", "age", "updatedAtMs" }));
      
      // Create and serialize a parent object with a child
      var parent = new Parent() { colour = "blue", Children = new[] { child } };
      output = sz.SerializeToNode(parent);

      // Validate the serialized parent object does not include specific properties
      Assert.That(output.HasKey(nameof(SuperModel.__mutable)), Is.False);
      Assert.That(output.HasKey(nameof(Parent.Children)), Is.False);
      Assert.That(output.Keys(), Is.EquivalentTo(new string[] { "id", "colour", "Size", "updatedAtMs" }));

      // Re-associate parent to child and serialize the child to ensure properties are not included
      child.Parent = parent;
      output = sz.SerializeToNode(child);
      Assert.That(output.HasKey(nameof(Child.Parent)), Is.False);
    }

    /// <summary>
    /// Tests the serialization of objects containing binary data properties to ensure such properties
    /// are ignored and their paths are serialized instead, preserving the efficient handling of large binary data.
    /// </summary>
    [Test]
    public async Task BinaryProperties() {
      var thingPhoto = new ThingPhoto() {
        id = 3,
        name = "happy snap",
        Image = new Bitmap(100, 100),
        imagePath = "main",
        Thumbnail = new Bitmap(10, 10),
        thumbnailPath = "thumbnail"
      };

      // Serialize and validate that paths are serialized and binary data is ignored
      var output = sz.SerializeToNode(thingPhoto);
      Assert.That(output[nameof(ThingPhoto.name)]!.GetValue<string>(), Is.EqualTo(thingPhoto.name));
      Assert.That(output[nameof(ThingPhoto.imagePath)]!.GetValue<string>(), Is.EqualTo(thingPhoto.imagePath));
      Assert.That(output[nameof(ThingPhoto.thumbnailPath)]!.GetValue<string>(), Is.EqualTo(thingPhoto.thumbnailPath));

      Assert.That(output.HasKey(nameof(ThingPhoto.Image)), Is.False);
      Assert.That(output.HasKey(nameof(ThingPhoto.Thumbnail)), Is.False);
    }

    /// <summary>
    /// Tests the serialization and deserialization mechanism for a dictionary model.
    /// Ensures that a serialized dictionary containing model objects can be reconstructed accurately.
    /// </summary>
    [Test]
    public void TestDictionaryModelSerialization() {
      // Initialize a list of Thing objects
      List<Thing> things = new List<Thing>(new[] {
        new Thing() { id = 1, name = "xyz" },
        new Thing() { id = 2, name = "tuv" }
      });

      // Serialize the list into an immutable dictionary structure
      var output = sz.Serialize(
        ImmutableDictionary<string, object?>.Empty.Add(
          "Things",
          things.ToImmutableArray()
        )
      );

      Log.Debug(output);

      // Deserialize as a dictionary
      var outputDictionary = sz.DeserializeImmutableDictionary(output!);
      var things2 = ((outputDictionary["Things"] as IList<object>)!).Cast<IReadOnlyDictionary<string, object?>>().ToArray();

      // Check the deserialized objects
      Assert.That(things2!.Count, Is.EqualTo(2));
      Assert.That(things2[0]["id"], Is.EqualTo(things[0].id));
      Assert.That(things2[0]["name"], Is.EqualTo(things[0].name));
    }
  }
}
