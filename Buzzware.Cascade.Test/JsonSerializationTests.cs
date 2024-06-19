using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using Buzzware.Cascade.Testing;
using NUnit.Framework;

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
	}
}
