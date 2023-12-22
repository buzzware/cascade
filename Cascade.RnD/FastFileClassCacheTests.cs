using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Cascade.Test;
using Cascade.Testing;
using NUnit.Framework;
using Serilog;

namespace Cascade.RnD {

    [TestFixture]
	public class FastFileClassCacheTests {
        private string tempDir;
		
		[SetUp]
		public void SetUp() {
            //tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            tempDir = "/Users/gary/repos/civmec/CivtracDispatch/cascade/Cascade.RnD/temp/FastFileClassCacheTests";
            Log.Debug($"Cascade cache directory {tempDir}");
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir,true);
            Directory.CreateDirectory(tempDir);
		}
		
		[Test]
		public async Task Simple() {
            var cache = new FastFileClassCache<Thing,int>(tempDir);
            var thing1 = new Thing() {
                id = 1,
                name = "Fred",
                colour = "blue"
            };
            var arrivedAt1 = 1001;
            await cache.Store(thing1.id, thing1, arrivedAt1);
            var arrivedAt1Response = await cache.Fetch(new RequestOp(
                arrivedAt1,
                thing1.GetType(),
                RequestVerb.Get,
                thing1.id
            ));
            // var content = CascadeUtils.LoadFileAsString(Path.Combine(tempDir,""))
            // Assert.That();
            Debug.WriteLine("Hello");
            var arrivedAt1Fetched = (arrivedAt1Response.Result as Thing)!;
            Assert.That(arrivedAt1Fetched.id,Is.EqualTo(thing1.id));
            Assert.That(arrivedAt1Fetched.name,Is.EqualTo(thing1.name));
            Assert.That(arrivedAt1Fetched.colour,Is.EqualTo(thing1.colour));
            Assert.That(arrivedAt1Response.ArrivedAtMs,Is.EqualTo(arrivedAt1));
        }

        [Test]
        public async Task DoubleStoreShouldNotRewriteFile() {
            var cache = new FastFileClassCache<Thing,int>(tempDir);
            var thing1 = new Thing() {
                id = 1,
                name = "Fred",
                colour = "blue"
            };
            var arrivedAt1 = 1001;
            await cache.Store(thing1.id, thing1, arrivedAt1);
            var filePath = cache.GetModelFilePath(thing1.id);
            Assert.That(File.Exists(filePath));
            File.Delete(filePath);
            await cache.Store(thing1.id, thing1, arrivedAt1);
            Assert.That(File.Exists(filePath),Is.False);
            var fetchResponse = await cache.Fetch(new RequestOp(
                arrivedAt1,
                thing1.GetType(),
                RequestVerb.Get,
                thing1.id
            ));
            var fetchThing = fetchResponse.Result as Thing;
            Assert.That(fetchThing!.id,Is.EqualTo(thing1.id));
        }
        
        [Test]
        public async Task DoubleStoreShouldUpdateFileTime() {
            var cache = new FastFileClassCache<Thing,int>(tempDir);
            var thing1 = new Thing() {
                id = 1,
                name = "Fred",
                colour = "blue"
            };
            var arrivedAt1 = 1234;
            var filePath = cache.GetModelFilePath(thing1.id);
            await cache.Store(thing1.id, thing1, arrivedAt1);
            Assert.That(CascadeUtils.toUnixMilliseconds(File.GetLastWriteTimeUtc(filePath)),Is.EqualTo(arrivedAt1));
            await cache.Store(thing1.id, thing1, arrivedAt1+1000);
            Assert.That(CascadeUtils.toUnixMilliseconds(File.GetLastWriteTimeUtc(filePath)),Is.EqualTo(arrivedAt1+1000));
            var fetchResponse = await cache.Fetch(new RequestOp(
                arrivedAt1+2000,
                thing1.GetType(),
                RequestVerb.Get,
                thing1.id
            ));
            var fetchThing = fetchResponse.Result as Thing;
            Assert.That(fetchThing!.id,Is.EqualTo(thing1.id));
            Assert.That(fetchResponse.ArrivedAtMs, Is.EqualTo(arrivedAt1+1000));
        }
        
        [Test]
        public async Task UpdateShouldUpdateContent() {
            var cache = new FastFileClassCache<Thing,int>(tempDir);
            var thing1 = new Thing() {
                id = 1,
                name = "Fred",
                colour = "blue"
            };
            var arrivedAt1 = 1234;
            var filePath = cache.GetModelFilePath(thing1.id);
            // store original ie blue
            await cache.Store(thing1.id, thing1, arrivedAt1);
            Assert.That(CascadeUtils.toUnixMilliseconds(File.GetLastWriteTimeUtc(filePath)),Is.EqualTo(arrivedAt1));
            thing1.colour = "red";
            // store updated ie red
            await cache.Store(thing1.id, thing1, arrivedAt1+1000);
            Assert.That(CascadeUtils.toUnixMilliseconds(File.GetLastWriteTimeUtc(filePath)),Is.EqualTo(arrivedAt1+1000));
            
            // fetch and check is red
            var fetchResponse = await cache.Fetch(new RequestOp(
                arrivedAt1+2000,
                thing1.GetType(),
                RequestVerb.Get,
                thing1.id
            ));
            Assert.That(fetchResponse.ArrivedAtMs, Is.EqualTo(arrivedAt1+1000));
            var fetchThing = fetchResponse.Result as Thing;
            Assert.That(fetchThing!.id,Is.EqualTo(thing1.id));
            Assert.That(fetchThing!.colour,Is.EqualTo("red"));

            // is red in file
            var fileContent = CascadeUtils.LoadFileAsString(filePath);
            var fileThing = cache.DeserializeCacheString<Thing>(fileContent);
            Assert.That(fileThing.colour,Is.EqualTo("red"));
        }
    }
}
