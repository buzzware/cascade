
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Buzzware.Cascade.Testing;
using NUnit.Framework;
using Serilog;

namespace Buzzware.Cascade.Test {

    /// <summary>
    /// Test suite for testing the FastFileClassCache functionality within the Cascade library.
    /// This class includes tests to verify the behavior of caching, storing, and retrieving objects
    /// using the FastFileClassCache and ensuring file operations like read, write, and deletion 
    /// work as expected.
    /// </summary>
    [TestFixture]
	public class FastFileClassCacheTests {
        private string testSourcePath;
        private string tempDir;
        private string testClassName;
        private string testName;

        /// <summary>
        /// Sets up the environment for each test, initializing test paths, and ensuring a clean
        /// temporary directory for file operations, ensuring no residual data from previous tests.
        /// </summary>
        [SetUp]
		public void SetUp() {
            testClassName = TestContext.CurrentContext.Test.ClassName.Split('.').Last();
            testName = TestContext.CurrentContext.Test.Name;
            testSourcePath = CascadeUtils.AboveFolderNamed(TestContext.CurrentContext.TestDirectory,"bin")!;
            tempDir = testSourcePath+$"/temp/{testClassName}.{testName}";
            Log.Debug($"Buzzware.Cascade cache directory {tempDir}");
            
            // Ensure that any existing temporary directory is deleted to avoid conflicts
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);

            // Create a new temporary directory for this test
            Directory.CreateDirectory(tempDir);
		}
		
		/// <summary>
        /// Tests the basic functionality of storing and fetching an object in the cache.
        /// Ensures that data retrieval returns the correct object and timestamp.
        /// </summary>
		[Test]
		public async Task Simple() {
            var cache = new FastFileClassCache<Thing,int>(tempDir);

            // Initialize test object
            var thing1 = new Thing() {
                id = 1,
                name = "Fred",
                colour = "blue"
            };
            var arrivedAt1 = 1001;

            // Store the object in the cache
            await cache.Store(thing1.id, thing1, arrivedAt1);

            // Fetch the object from the cache
            var arrivedAt1Response = await cache.Fetch(new RequestOp(
                arrivedAt1,
                thing1.GetType(),
                RequestVerb.Get,
                thing1.id
            ));
            
            // Assert the fetched object attributes are as expected
            var arrivedAt1Fetched = (arrivedAt1Response.Result as Thing)!;
            Assert.That(arrivedAt1Fetched.id, Is.EqualTo(thing1.id));
            Assert.That(arrivedAt1Fetched.name, Is.EqualTo(thing1.name));
            Assert.That(arrivedAt1Fetched.colour, Is.EqualTo(thing1.colour));
            Assert.That(arrivedAt1Response.ArrivedAtMs, Is.EqualTo(arrivedAt1));
        }

        /// <summary>
        /// Verifies that storing an object with the same key twice does not rewrite the file (because hashing allows us to know that the existing content is already the same).
        /// Ensures the cache system respects file existence after deletion and checks file operations.
        /// </summary>
        [Test]
        public async Task DoubleStoreShouldNotRewriteFile() {
            var cache = new FastFileClassCache<Thing,int>(tempDir);

            // Initialize test object
            var thing1 = new Thing() {
                id = 1,
                name = "Fred",
                colour = "blue"
            };
            var arrivedAt1 = 1001;

            // Store the object the first time
            await cache.Store(thing1.id, thing1, arrivedAt1);
            var filePath = cache.GetModelFilePath(thing1.id);
            
            // Assert the file exists after the first store
            Assert.That(File.Exists(filePath));
            
            // Delete the existing file
            File.Delete(filePath);

            // Store the object a second time
            await cache.Store(thing1.id, thing1, arrivedAt1);

            // Assert the file does not exist after the second store
            Assert.That(File.Exists(filePath), Is.False);

            // Fetch the object and verify attributes
            var fetchResponse = await cache.Fetch(new RequestOp(
                arrivedAt1,
                thing1.GetType(),
                RequestVerb.Get,
                thing1.id
            ));
            var fetchThing = fetchResponse.Result as Thing;
            Assert.That(fetchThing!.id, Is.EqualTo(thing1.id));
        }
        
        /// <summary>
        /// Tests that updating an object's timestamp in the store reflects the correct last write time on file.
        /// This ensures that file timestamps are managed properly for cache updates.
        /// </summary>
        [Test]
        public async Task DoubleStoreShouldUpdateFileTime() {
            var cache = new FastFileClassCache<Thing,int>(tempDir);

            // Initialize test object
            var thing1 = new Thing() {
                id = 1,
                name = "Fred",
                colour = "blue"
            };
            var arrivedAt1 = 1234;
            var filePath = cache.GetModelFilePath(thing1.id);

            // Store the object and assert the file's write time
            await cache.Store(thing1.id, thing1, arrivedAt1);
            Assert.That(CascadeUtils.toUnixMilliseconds(File.GetLastWriteTimeUtc(filePath)), Is.EqualTo(arrivedAt1));

            // Store the object again with a different timestamp
            await cache.Store(thing1.id, thing1, arrivedAt1+1000);

            // Assert the updated file's write time reflects the new timestamp
            Assert.That(CascadeUtils.toUnixMilliseconds(File.GetLastWriteTimeUtc(filePath)), Is.EqualTo(arrivedAt1+1000));

            // Fetch the object and verify the timestamp
            var fetchResponse = await cache.Fetch(new RequestOp(
                arrivedAt1+2000,
                thing1.GetType(),
                RequestVerb.Get,
                thing1.id
            ));
            var fetchThing = fetchResponse.Result as Thing;
            Assert.That(fetchThing!.id, Is.EqualTo(thing1.id));
            Assert.That(fetchResponse.ArrivedAtMs, Is.EqualTo(arrivedAt1+1000));
        }
        
        /// <summary>
        /// Tests the update mechanism to ensure an object's content is updated in the cache.
        /// Validates that changes are stored correctly and that file content reflects updates.
        /// </summary>
        [Test]
        public async Task UpdateShouldUpdateContent() {
            var cache = new FastFileClassCache<Thing,int>(tempDir);

            // Initialize test object
            var thing1 = new Thing() {
                id = 1,
                name = "Fred",
                colour = "blue"
            };
            var arrivedAt1 = 1234;
            var filePath = cache.GetModelFilePath(thing1.id);

            // Store original object with colour blue
            await cache.Store(thing1.id, thing1, arrivedAt1);
            Assert.That(CascadeUtils.toUnixMilliseconds(File.GetLastWriteTimeUtc(filePath)), Is.EqualTo(arrivedAt1));

            // Update the object's colour and store it again
            thing1.colour = "red";
            await cache.Store(thing1.id, thing1, arrivedAt1+1000);
            Assert.That(CascadeUtils.toUnixMilliseconds(File.GetLastWriteTimeUtc(filePath)), Is.EqualTo(arrivedAt1+1000));
            
            // Fetch the updated object and verify the attributes and timestamp
            var fetchResponse = await cache.Fetch(new RequestOp(
                arrivedAt1+2000,
                thing1.GetType(),
                RequestVerb.Get,
                thing1.id
            ));
            Assert.That(fetchResponse.ArrivedAtMs, Is.EqualTo(arrivedAt1+1000));
            var fetchThing = fetchResponse.Result as Thing;
            Assert.That(fetchThing!.id, Is.EqualTo(thing1.id));
            Assert.That(fetchThing!.colour, Is.EqualTo("red"));

            // Verify the file content reflects the updated colour
            var fileContent = CascadeUtils.LoadFileAsString(filePath);
            var fileThing = cache.DeserializeCacheString<Thing>(fileContent);
            Assert.That(fileThing.colour, Is.EqualTo("red"));
        }
    }
}
