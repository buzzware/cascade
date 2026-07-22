using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Buzzware.Cascade.Testing;
using NUnit.Framework;
using Buzzware.StandardExceptions;

namespace Buzzware.Cascade.Test {

  /// <summary>
  /// Tests fallback behaviour of Get and Query for the Thing model through a memory layer
  /// (ModelCache/ModelClassCache) and a file layer (ModelCache/FastFileClassCache) when the cached
  /// data is older than the Thing freshness but within or beyond the fallback freshness.
  /// </summary>
  [TestFixture]
  public class QueryFallback {

    private const long T = 100;                              // origin time in ms when models arrive in the caches
    private const int ThingFreshnessSeconds = 1000;
    private const int ThingFallbackSeconds = 28 * 24 * 3600; // 28 days
    private const string RedThingsKey = "red_things";

    private string tempDir;
    private MockOrigin2 origin;
    private MockModelClassOrigin<Thing> thingOrigin;
    private ModelClassCache<Thing, int> thingMemoryCache;
    private ModelCache memoryCache;
    private FastFileClassCache<Thing, int> thingFileCache;
    private ModelCache fileCache;
    private CascadeDataLayer cascade;

    /// <summary>
    /// Sets up a mock origin for Thing plus a memory cache layer and a file cache layer in a
    /// clean temporary directory, and a cascade where Thing defaults to
    /// freshness 1000 seconds and fallback freshness 28 days.
    /// </summary>
    [SetUp]
    public void SetUp() {
      var testClassName = TestContext.CurrentContext.Test.ClassName!.Split('.').Last();
      var testName = TestContext.CurrentContext.Test.Name;
      var testSourcePath = CascadeUtils.AboveFolderNamed(TestContext.CurrentContext.TestDirectory, "bin")!;
      tempDir = testSourcePath + $"/temp/{testClassName}.{testName}";
      if (Directory.Exists(tempDir))
        Directory.Delete(tempDir, true);
      Directory.CreateDirectory(tempDir);

      thingOrigin = new MockModelClassOrigin<Thing>();
      origin = new MockOrigin2(
        new Dictionary<Type, IModelClassOrigin>() {
          { typeof(Thing), thingOrigin }
        },
        T
      );

      thingMemoryCache = new ModelClassCache<Thing, int>();
      memoryCache = new ModelCache(
        aClassCache: new Dictionary<Type, IModelClassCache>() {
          { typeof(Thing), thingMemoryCache }
        }
      );

      thingFileCache = new FastFileClassCache<Thing, int>(tempDir);
      fileCache = new ModelCache(
        aClassCache: new Dictionary<Type, IModelClassCache>() {
          { typeof(Thing), thingFileCache }
        }
      );

      var config = new CascadeConfig() {
        ModelConfig = new Dictionary<Type, ModelConfig> {
          { typeof(Thing), new ModelConfig(ThingFreshnessSeconds, ThingFallbackSeconds) }
        }
      };

      cascade = new CascadeDataLayer(
        origin,
        new ICascadeCache[] { memoryCache, fileCache },
        config,
        new MockCascadePlatform(),
        ErrorControl.Instance,
        new CascadeJsonSerialization()
      );
    }

    /// <summary>
    /// Stores things in the origin, then at time T queries the red ones through cascade so that
    /// the collection and its models are cached at T in both the memory and file layers.
    /// </summary>
    private async Task SeedAndCacheAtT() {
      Thing[] allThings = {
        new Thing() { id = 1, name = "Thing 1", colour = "red" },
        new Thing() { id = 2, name = "Thing 2", colour = "green" },
        new Thing() { id = 3, name = "Thing 3", colour = "red" },
      };
      foreach (var t in allThings)
        await thingOrigin.Store(t.id, t);

      var redThings = await cascade.Query<Thing>(RedThingsKey, new JsonObject { ["colour"] = "red" });
      Assert.That(redThings.Select(t => t.id), Is.EqualTo(new[] { 1, 3 }));
    }

    /// <summary>
    /// Asserts that Get returns the cached model, and that Query returns the cached collection and
    /// its models, even though the origin cannot be reached.
    /// </summary>
    private async Task AssertGetAndQueryReturnCachedRedThings() {
      var thing = await cascade.Get<Thing>(1);
      Assert.That(thing, Is.Not.Null);
      Assert.That(thing!.id, Is.EqualTo(1));
      Assert.That(thing.name, Is.EqualTo("Thing 1"));
      Assert.That(thing.colour, Is.EqualTo("red"));

      var redThings = (await cascade.Query<Thing>(RedThingsKey, new JsonObject { ["colour"] = "red" })).ToImmutableArray();
      Assert.That(redThings.Select(t => t.id), Is.EqualTo(new[] { 1, 3 }));
      Assert.That(redThings.Select(t => t.colour), Is.All.EqualTo("red"));

      var collection = await cascade.GetCollection<Thing>(RedThingsKey, freshnessSeconds: RequestOp.FRESHNESS_ANY);
      Assert.That(collection, Is.EqualTo(new[] { 1, 3 }));
    }

    /// <summary>
    /// ConnectionOnline==true with the cached values older than freshness but within fallback and the
    /// origin unreachable => Get should return the cached model and Query should return the cached
    /// collection and models, first from the memory layer and then from the file layer alone.
    /// </summary>
    [Test]
    public async Task WithinFallbackConnectionOnline() {
      await SeedAndCacheAtT();

      origin.NowMs = T + 2000 * 1000;    // past freshness (1000s), within fallback (28 days)
      origin.ActLikeOffline = true;      // origin requests fail, forcing fallback to cached values
      Assert.That(cascade.ConnectionOnline, Is.True);

      await AssertGetAndQueryReturnCachedRedThings();

      // clear the memory layer so the file cache alone provides the fallback values
      await memoryCache.ClearAll(exceptHeld: false);
      await AssertGetAndQueryReturnCachedRedThings();
    }

    /// <summary>
    /// ConnectionOnline==false with the cached values older than freshness but within fallback =>
    /// Get should return the cached model and Query should return the cached collection and models,
    /// first from the memory layer and then from the file layer alone.
    /// </summary>
    [Test]
    public async Task WithinFallbackConnectionOffline() {
      await SeedAndCacheAtT();

      origin.NowMs = T + 2000 * 1000;    // past freshness (1000s), within fallback (28 days)
      origin.ActLikeOffline = true;
      cascade.ConnectionOnline = false;

      await AssertGetAndQueryReturnCachedRedThings();

      // clear the memory layer so the file cache alone provides the fallback values
      await memoryCache.ClearAll(exceptHeld: false);
      await AssertGetAndQueryReturnCachedRedThings();
    }

    /// <summary>
    /// ConnectionOnline==true with the cached values older than fallback and the origin unreachable =>
    /// Get and Query should throw OriginAccessFailure.
    /// (When ConnectionOnline==true Cascade throws OriginAccessFailure rather than
    /// DataNotAvailableOffline - see ProcessGetOrQuery and CascadeExceptions.cs)
    /// </summary>
    [Test]
    public async Task BeyondFallbackConnectionOnline() {
      await SeedAndCacheAtT();

      origin.NowMs = T + 30L * 24 * 3600 * 1000;   // 30 days, past fallback (28 days)
      origin.ActLikeOffline = true;
      Assert.That(cascade.ConnectionOnline, Is.True);

      Assert.ThrowsAsync<OriginAccessFailure>(async () => await cascade.Get<Thing>(1));
      Assert.ThrowsAsync<OriginAccessFailure>(async () => await cascade.Query<Thing>(RedThingsKey, new JsonObject { ["colour"] = "red" }));
    }

    /// <summary>
    /// ConnectionOnline==false with the cached values older than fallback =>
    /// Get and Query should throw DataNotAvailableOffline.
    /// </summary>
    [Test]
    public async Task BeyondFallbackConnectionOffline() {
      await SeedAndCacheAtT();

      origin.NowMs = T + 30L * 24 * 3600 * 1000;   // 30 days, past fallback (28 days)
      origin.ActLikeOffline = true;
      cascade.ConnectionOnline = false;

      Assert.ThrowsAsync<DataNotAvailableOffline>(async () => await cascade.Get<Thing>(1));
      Assert.ThrowsAsync<DataNotAvailableOffline>(async () => await cascade.Query<Thing>(RedThingsKey, new JsonObject { ["colour"] = "red" }));
    }
  }
}
