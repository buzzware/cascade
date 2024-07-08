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
	[TestFixture]
	public class JsonSerializationTests {
		private CascadeJsonSerialization sz;


		[SetUp]
		public void SetUp() {
			sz = new CascadeJsonSerialization(ignoreUnderscoreProperties: true, ignoreAssociations: true);
		}

		[Test]
		public async Task Simple() {
			var child = new Child() { age = 25 };
			var output = sz.SerializeToNode(child);
			Assert.That(output.HasKey(nameof(SuperModel.__mutable)),Is.False);
			Assert.That(output.HasKey(nameof(Child.Parent)),Is.False);
			Assert.That(output.Keys, Is.EquivalentTo(new string[] { "id","parentId","weight","power","age","updatedAtMs" }));
				
			
			var parent = new Parent() { colour = "blue", Children = new[] { child } };
			output = sz.SerializeToNode(parent);
			Assert.That(output.HasKey(nameof(SuperModel.__mutable)),Is.False);
			Assert.That(output.HasKey(nameof(Parent.Children)),Is.False);
			Assert.That(output.Keys(), Is.EquivalentTo(new string[] { "id","colour","Size","updatedAtMs" }));
			
			child.Parent = parent;
			output = sz.SerializeToNode(child);
			Assert.That(output.HasKey(nameof(Child.Parent)),Is.False);
		}
		
		[Test]
		public async Task BinaryProperties() {
			var thingPhoto = new ThingPhoto() {
				id = 3,
				name = "happy snap",
				Image = new Bitmap(100,100),
				imagePath = "main",
				Thumbnail = new Bitmap(10,10),
				thumbnailPath = "thumbnail"
			};
			var output = sz.SerializeToNode(thingPhoto);
			Assert.That(output[nameof(ThingPhoto.name)]!.GetValue<string>(),Is.EqualTo(thingPhoto.name));
			Assert.That(output[nameof(ThingPhoto.imagePath)]!.GetValue<string>(),Is.EqualTo(thingPhoto.imagePath));
			Assert.That(output[nameof(ThingPhoto.thumbnailPath)]!.GetValue<string>(),Is.EqualTo(thingPhoto.thumbnailPath));

			Assert.That(output.HasKey(nameof(ThingPhoto.Image)), Is.False);
			Assert.That(output.HasKey(nameof(ThingPhoto.Thumbnail)), Is.False);
		}
		
		[Test]
		public void TestDictionaryModelSerialization() {
			List<Thing> things = new List<Thing>(new[] {
				new Thing() {
					id = 1,
					name = "xyz"
				},
				new Thing() {
					id = 2,
					name = "tuv"
				}
			});
			var output = sz.Serialize(
				ImmutableDictionary<string,object?>.Empty.Add(
					"Things",
					things.ToImmutableArray()
				)
			);

			Log.Debug(output);
			var outputDictionary = sz.DeserializeImmutableDictionary(output!);
			var things2 = ((outputDictionary["Things"] as IList<object>)!).Cast<IReadOnlyDictionary<string,object?>>().ToArray();
			Assert.That(things2!.Count,Is.EqualTo(2));
			Assert.That(things2[0]["id"],Is.EqualTo(things[0].id));
			Assert.That(things2[0]["name"],Is.EqualTo(things[0].name));
		}
	}
}
