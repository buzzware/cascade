
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
          timeMs: nowMs,
          exists: true,
          arrivedAtMs: nowMs, result: thing));
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
      Assert.That(thing!.id, Is.EqualTo(5));
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
      
      Assert.That(thing1!.id, Is.EqualTo(5));
      Assert.That(thing1.updatedAtMs, Is.EqualTo(cascade.NowMs));

      // should also be in both caches
      var store1ThingResponse = await thingModelStore1.Fetch(RequestOp.GetOp<Parent>(5,cascade.NowMs));
      Assert.That((store1ThingResponse.Result as Parent)!.id, Is.EqualTo(5));
      Assert.That((store1ThingResponse.Result as Parent)!.updatedAtMs, Is.EqualTo(cascade.NowMs));
      var store2ThingResponse = await thingModelStore2.Fetch(RequestOp.GetOp<Parent>(5,cascade.NowMs));
      Assert.That((store2ThingResponse.Result as Parent)!.id, Is.EqualTo(5));
      Assert.That((store2ThingResponse.Result as Parent)!.updatedAtMs, Is.EqualTo(cascade.NowMs));

      origin.IncNowMs();
      
      // freshness=5 allows for cached version 
      var thing2 = (await cascade.Get<Parent>(5,freshnessSeconds: 5))!;
      Assert.That(thing2.updatedAtMs, Is.EqualTo(thing1.updatedAtMs));
      
      // freshness=0 doesn't allow for cached version 
      var thing3 = (await cascade.Get<Parent>(5,freshnessSeconds: 0))!;
      Assert.That(thing3.updatedAtMs, Is.EqualTo(origin.NowMs));

      // caches should also be updated
      store1ThingResponse = await thingModelStore1.Fetch(RequestOp.GetOp<Parent>(5,cascade.NowMs));
      Assert.That((store1ThingResponse.Result as Parent)!.updatedAtMs, Is.EqualTo(origin.NowMs));
      store2ThingResponse = await thingModelStore2.Fetch(RequestOp.GetOp<Parent>(5,cascade.NowMs));
      Assert.That((store2ThingResponse.Result as Parent)!.updatedAtMs, Is.EqualTo(origin.NowMs));
      
      origin.IncNowMs(2000);
      
      // freshness=2 should allow for cached version 
      var thing4 = (await cascade.Get<Parent>(5,freshnessSeconds: 2))!;
      Assert.That(thing4.updatedAtMs, Is.EqualTo(thing3.updatedAtMs));

      // freshness=1 should get fresh version
      var thing5 = (await cascade.Get<Parent>(5,freshnessSeconds: 1))!;
      Assert.That(thing5.updatedAtMs, Is.EqualTo(origin.NowMs));
      
      origin.IncNowMs(1000);
      
      // clear cache1, freshnessSeconds=1 should return value from cache2 and update cache1
      await cache1.ClearAll();
      var thing6 = (await cascade.Get<Parent>(thing4.id,freshnessSeconds: 1))!;      // should get cache2 version
      Assert.That(thing6.updatedAtMs, Is.EqualTo(thing5.updatedAtMs));
      store1ThingResponse = await thingModelStore1.Fetch(RequestOp.GetOp<Parent>(5,cascade.NowMs));
      Assert.That((store1ThingResponse.Result as Parent)!.updatedAtMs, Is.EqualTo(thing6.updatedAtMs));
    }
  }
}
