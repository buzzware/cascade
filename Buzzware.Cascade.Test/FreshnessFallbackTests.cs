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
  public class FreshnessFallbackTests {

    private string tempDir;
    MockOrigin2 origin;
    private ModelCache cache;
    private MockModelClassOrigin<Thing> thingOrigin;
    private ModelClassCache<Thing, long> thingCache;


    private int testEndTime = 10000;
    private int testThingId = 1;
    private int testFreshness = 6000;
    private int testFallback = 9000;
    private OpResponse response;
    private Thing thing;


    /// <summary>
    /// Sets up the temporary directory and mock origin required for the tests.
    /// Initializes the mock origin with specific behaviors to simulate data responses.
    /// </summary>
    [SetUp]
    public void SetUp() {
      tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
      Log.Debug($"Buzzware.Cascade cache directory {tempDir}");
      Directory.CreateDirectory(tempDir);

      thingOrigin = new MockModelClassOrigin<Thing>();
      origin = new MockOrigin2(
        new Dictionary<Type, IModelClassOrigin>() {
          { typeof(Thing), thingOrigin }
        },
        10000
      );

      thingCache = new ModelClassCache<Thing, long>();
      cache = new ModelCache(
        aClassCache: new Dictionary<Type, IModelClassCache>() {
          { typeof(Thing), thingCache }
        }
      );

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
    /// * ConnectionOnline=true and cached version arrived within freshness => Should return cached version.
    /// * ConnectionOnline=true and cached version arrived older than freshness and succeeded reaching origin => Should return origin version
    /// </summary>
    [Test]
    public async Task ConnectionOnlineSuccess() {
      var config = new CascadeConfig() {
        DefaultFreshnessSeconds = 3,
        DefaultFallbackFreshnessSeconds = 5,
        ModelConfig = new Dictionary<Type,ModelConfig> {
          {typeof(Thing), new ModelConfig(testFreshness/1000,testFallback/1000)}
        }
      };
      
      var cascade = new CascadeDataLayer(
        origin,
        new ICascadeCache[] { cache },
        config,
        new MockCascadePlatform(),
        ErrorControl.Instance,
        new CascadeJsonSerialization()
      );
      
      
      Assert.That(config.GetFreshnessSeconds(typeof(Thing)), Is.EqualTo(testFreshness/1000));
      Assert.That(config.GetFallbackFreshnessSeconds(typeof(Thing)), Is.EqualTo(testFallback/1000));
      Assert.That(config.GetFreshnessSeconds(typeof(Child)), Is.EqualTo(3));
      Assert.That(config.GetFallbackFreshnessSeconds(typeof(Child)), Is.EqualTo(5));
      
      var request1ms = testEndTime - testFreshness + 500;
      origin.NowMs = request1ms;
      var thing1_1 = new Thing() {id = testThingId, name = $"thing1_1 @ {origin.NowMs}"};
      await thingOrigin.Store(testThingId,thing1_1);

      response = await cascade.GetResponse(typeof(Thing),testThingId);
      thing = (Thing)response.Result!;
      Assert.That(thing, Is.SameAs(thing1_1));
      Assert.That(response.ArrivedAtMs, Is.EqualTo(request1ms));
      Assert.That(response.LayerIndex, Is.EqualTo(-1));        // from origin
      
      
      var request2ms = request1ms + 100;
      origin.NowMs = request2ms; 
      var thing1_2 = new Thing() {id = testThingId, name = $"thing1_2 @ {origin.NowMs}"};
      await thingOrigin.Store(testThingId,thing1_2);

      response = await cascade.GetResponse(typeof(Thing), testThingId);
      thing = (Thing)response.Result!;
      Assert.That(thing, Is.SameAs(thing1_1));
      Assert.That(response.ArrivedAtMs, Is.EqualTo(request1ms));
      Assert.That(response.LayerIndex, Is.EqualTo(0));         // from cache
      
      // now move forward in time beyond freshness, and cascade should get from the origin
      
      var request3ms = request2ms + testFreshness + 100;
      origin.NowMs = request3ms;
      var thing1_3 = new Thing() {id = testThingId, name = $"thing1_3 @ {origin.NowMs}"};
      await thingOrigin.Store(testThingId,thing1_3);

      response = await cascade.GetResponse(typeof(Thing), testThingId);
      thing = (Thing)response.Result!;
      Assert.That(thing, Is.SameAs(thing1_3));
      Assert.That(response.ArrivedAtMs, Is.EqualTo(request3ms));
      Assert.That(response.LayerIndex, Is.EqualTo(-1));        // from origin
    }
    
    
    /// <summary>
    /// ConnectionOnline=true and cached version arrived older than fallback and failed to reach origin => Should throw DataNotAvailableOffline exception.
    /// ConnectionOnline=true and cached version arrived older than fallback and succeeded reaching origin => Should return origin version.
    /// ConnectionOnline=true and cached version arrived within fallback and failed to reach origin => Should return cached version.
    /// </summary>
    [Test]
    public async Task ConnectionOnlineFailure() {
      var config = new CascadeConfig() {
        DefaultFreshnessSeconds = 3,
        DefaultFallbackFreshnessSeconds = 5,
        ModelConfig = new Dictionary<Type,ModelConfig> {
          {typeof(Thing), new ModelConfig(testFreshness/1000,testFallback/1000)}
        }
      };
      
      var cascade = new CascadeDataLayer(
        origin,
        new ICascadeCache[] { cache },
        config,
        new MockCascadePlatform(),
        ErrorControl.Instance,
        new CascadeJsonSerialization()
      );
      
      var arrival1ms = testEndTime - testFallback - 500;
      origin.NowMs = arrival1ms;
      var thing1_1 = new Thing() {id = testThingId, name = $"thing1_1 @ {origin.NowMs}"};
      await cache.Store(typeof(Thing), testThingId, thing1_1, origin.NowMs);
      
      origin.NowMs = testEndTime;
      origin.ActLikeOffline = true;
      // ConnectionOnline=true and cached version arrived older than fallback and failed to reach origin => Should throw OriginAccessFailure exception.
      Assert.ThrowsAsync<OriginAccessFailure>(async () => response = await cascade.GetResponse(typeof(Thing),testThingId));
      
      var thing1_2 = new Thing() {id = testThingId, name = $"thing1_2 @ {origin.NowMs}"};
      await thingOrigin.Store(testThingId,thing1_2);
      origin.ActLikeOffline = false;
      
      response = await cascade.GetResponse(typeof(Thing), testThingId);
      thing = (Thing)response.Result!;
      // ConnectionOnline=true and cached version arrived older than fallback and succeeded reaching origin => Should return origin version.
      Assert.That(thing, Is.SameAs(thing1_2));
      Assert.That(response.ArrivedAtMs, Is.EqualTo(testEndTime));
      Assert.That(response.LayerIndex, Is.EqualTo(-1));         // from origin
      

      origin.ActLikeOffline = true;
      origin.NowMs = testEndTime - testFallback + 500;
      var thing1_3 = new Thing() {id = testThingId, name = $"thing1_3 @ {origin.NowMs}"};
      await cache.Store(typeof(Thing), testThingId, thing1_3, origin.NowMs);

      response = await cascade.GetResponse(typeof(Thing), testThingId);
      thing = (Thing)response.Result!;
      // ConnectionOnline=true and cached version arrived within fallback and failed to reach origin => Should return cached version.
      Assert.That(thing, Is.SameAs(thing1_3));
      Assert.That(response.ArrivedAtMs, Is.EqualTo(origin.NowMs));
      Assert.That(response.LayerIndex, Is.EqualTo(0));         // from cache
    }

    /// <summary>
    /// ConnectionOnline=false, arrived older then fallback => Should throw DataNotAvailableOffline exception.
    /// ConnectionOnline=false and cached version arrived within fallback => Should return cached version.
    /// ConnectionOnline=false and cached version arrived within freshness => Should return cached version.
    /// </summary>
    [Test]
    public async Task ConnectionOffline() {
      var config = new CascadeConfig() {
        DefaultFreshnessSeconds = 3,
        DefaultFallbackFreshnessSeconds = 5,
        ModelConfig = new Dictionary<Type, ModelConfig> {
          { typeof(Thing), new ModelConfig(testFreshness / 1000, testFallback / 1000) }
        }
      };

      var cascade = new CascadeDataLayer(
        origin,
        new ICascadeCache[] { cache },
        config,
        new MockCascadePlatform(),
        ErrorControl.Instance,
        new CascadeJsonSerialization()
      );
      cascade.ConnectionOnline = false;

      origin.NowMs = testEndTime; // in this test we set now to testEndTime and directly insert cache values with specific arrival times  
      var arrival1ms = testEndTime - testFallback - 500;
      var thing1_1 = new Thing() { id = testThingId, name = $"thing1_1 @ {arrival1ms}" };
      await cache.Store(typeof(Thing), testThingId, thing1_1, arrival1ms);
      
      origin.ActLikeOffline = true;
      // ConnectionOnline=false, arrived older then fallback => Should throw DataNotAvailableOffline exception.
      Assert.ThrowsAsync<DataNotAvailableOffline>(async () => response = await cascade.GetResponse(typeof(Thing),testThingId));
      
      var arrival2ms = testEndTime - testFallback + 500;
      var thing1_2 = new Thing() { id = testThingId, name = $"thing1_2 @ {arrival2ms}" };
      await cache.Store(typeof(Thing), testThingId, thing1_2, arrival2ms);

      response = await cascade.GetResponse(typeof(Thing), testThingId);
      thing = (Thing)response.Result!;
      // ConnectionOnline=false and cached version arrived within fallback => Should return cached version.
      Assert.That(thing, Is.SameAs(thing1_2));
      Assert.That(response.ArrivedAtMs, Is.EqualTo(arrival2ms));
      Assert.That(response.LayerIndex, Is.EqualTo(0));         // from cache
      
      var arrival3ms = testEndTime - testFreshness + 500;
      var thing1_3 = new Thing() { id = testThingId, name = $"thing1_3 @ {arrival3ms}" };
      await cache.Store(typeof(Thing), testThingId, thing1_3, arrival3ms);

      response = await cascade.GetResponse(typeof(Thing), testThingId);
      thing = (Thing)response.Result!;
      // ConnectionOnline=false and cached version arrived within freshness => Should return cached version.
      Assert.That(thing, Is.SameAs(thing1_3));
      Assert.That(response.ArrivedAtMs, Is.EqualTo(arrival3ms));
      Assert.That(response.LayerIndex, Is.EqualTo(0));         // from cache
    }
  }
}
