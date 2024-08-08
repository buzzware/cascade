
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Buzzware.Cascade.Testing;
using NUnit.Framework;
using Serilog;
using Buzzware.StandardExceptions;

namespace Buzzware.Cascade.Test {

  /// <summary>
  /// Tests for reading data from the Cascade library, ensuring data can be retrieved from either the origin or cache.
  /// </summary>
  [TestFixture]
  public class ReadTests {
    
    private string tempDir;
    MockOrigin origin;

    /// <summary>
    /// Sets up the temporary directory and mock origin required for the tests.
    /// Initializes the mock origin with specific behaviors to simulate data responses.
    /// </summary>
    [SetUp]
    public void SetUp() {
      tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
      Log.Debug($"Buzzware.Cascade cache directory {tempDir}");
      Directory.CreateDirectory(tempDir);
      
      origin = new MockOrigin(nowMs:1000,handleRequest: (origin, requestOp) => {
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
    }

    /// <summary>
    /// Cleans up the temporary directory used during tests to avoid cluttering the filesystem.
    /// </summary>
    [TearDown]
    public void TearDown() {
      if (Directory.Exists(tempDir)) {
        Directory.Delete(tempDir, true);
      }
    }

    /// <summary>
    /// Verifies that the data can be read correctly from the origin when no cache is available.
    /// </summary>
    [Test]
    public async Task ReadWithoutCache() {
      var cascade = new CascadeDataLayer(origin,new ICascadeCache[] {}, new CascadeConfig(), new MockCascadePlatform(), ErrorControl.Instance, new CascadeJsonSerialization());
      var thing = await cascade.Get<Parent>(5);
      Assert.AreEqual(5,thing!.id);
    }

    /// <summary>
    /// Tests reading data using multiple caches to verify data retrieval and cache update logic.
    /// It ensures data consistency across multiple caches and tests the effect of different freshness parameters.
    /// </summary>
    [Test]
    public async Task ReadWithModelCachesMultitest() {
      var thingModelStore1 = new ModelClassCache<Parent, long>();
      var cache1 = new ModelCache(aClassCache: new Dictionary<Type, IModelClassCache>() {
        {typeof(Parent), thingModelStore1}
      });
      var thingModelStore2 = new ModelClassCache<Parent, long>();
      var cache2 = new ModelCache(aClassCache: new Dictionary<Type, IModelClassCache>() {
        {typeof(Parent), thingModelStore2}
      });
      
      // read from origin
      var cascade = new CascadeDataLayer(
        origin,
        new ICascadeCache[] {cache1,cache2}, 
        new CascadeConfig() {DefaultFreshnessSeconds = 1, StoragePath = tempDir}, 
        new MockCascadePlatform(), 
        ErrorControl.Instance, 
        new CascadeJsonSerialization()
      );
      var thing1 = await cascade.Get<Parent>(5);
      
      Assert.AreEqual(5,thing1!.id);
      Assert.AreEqual(cascade.NowMs,thing1.updatedAtMs);
      
      // should also be in both caches
      var store1ThingResponse = await thingModelStore1.Fetch(RequestOp.GetOp<Parent>(5,cascade.NowMs));
      Assert.AreEqual((store1ThingResponse.Result as Parent)!.id,5);
      Assert.AreEqual(cascade.NowMs,(store1ThingResponse.Result as Parent)!.updatedAtMs);
      var store2ThingResponse = await thingModelStore2.Fetch(RequestOp.GetOp<Parent>(5,cascade.NowMs));
      Assert.AreEqual((store2ThingResponse.Result as Parent)!.id,5);
      Assert.AreEqual(cascade.NowMs,(store2ThingResponse.Result as Parent)!.updatedAtMs);

      origin.IncNowMs();
      
      // freshness=5 allows for cached version 
      var thing2 = (await cascade.Get<Parent>(5,freshnessSeconds: 5))!;
      Assert.AreEqual(thing1.updatedAtMs,thing2.updatedAtMs);
      
      // freshness=0 doesn't allow for cached version 
      var thing3 = (await cascade.Get<Parent>(5,freshnessSeconds: 0))!;
      Assert.AreEqual(origin.NowMs,thing3.updatedAtMs);

      // caches should also be updated
      store1ThingResponse = await thingModelStore1.Fetch(RequestOp.GetOp<Parent>(5,cascade.NowMs));
      Assert.AreEqual(origin.NowMs,(store1ThingResponse.Result as Parent)!.updatedAtMs);
      store2ThingResponse = await thingModelStore2.Fetch(RequestOp.GetOp<Parent>(5,cascade.NowMs));
      Assert.AreEqual(origin.NowMs,(store2ThingResponse.Result as Parent)!.updatedAtMs);
      
      origin.IncNowMs(2000);
      
      // freshness=2 should allow for cached version 
      var thing4 = (await cascade.Get<Parent>(5,freshnessSeconds: 2))!;
      Assert.AreEqual(thing3.updatedAtMs,thing4.updatedAtMs);

      // freshness=1 should get fresh version 
      var thing5 = (await cascade.Get<Parent>(5,freshnessSeconds: 1))!;
      Assert.AreEqual(origin.NowMs,thing5.updatedAtMs);
      
      origin.IncNowMs(1000);
      
      // clear cache1, freshnessSeconds=1 should return value from cache2 and update cache1
      await cache1.ClearAll();
      var thing6 = (await cascade.Get<Parent>(thing4.id,freshnessSeconds: 1))!;      // should get cache2 version
      Assert.AreEqual(thing6.updatedAtMs,thing5.updatedAtMs);
      store1ThingResponse = await thingModelStore1.Fetch(RequestOp.GetOp<Parent>(5,cascade.NowMs));
      Assert.AreEqual(thing6.updatedAtMs,(store1ThingResponse.Result as Parent)!.updatedAtMs);
    }
  }
}
