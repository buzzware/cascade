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
	[TestFixture]
	public class PendingChangesTests {
		
		
		MockOrigin origin;
		private CascadeDataLayer cascade;
		private string tempDir;
		private CascadeJsonSerialization serialization;

		[SetUp]
		public void SetUp() {
			tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
			Log.Debug($"Buzzware.Cascade cache directory {tempDir}");
			Directory.CreateDirectory(tempDir);

			serialization = new CascadeJsonSerialization();

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
			cascade = new CascadeDataLayer(
				origin,
				new ICascadeCache[] {}, 
				new CascadeConfig() {StoragePath = tempDir}, 
				new MockCascadePlatform(), 
				ErrorControl.Instance, 
				serialization
			);
		}

		[TearDown]
		public void TearDown() {
			if (Directory.Exists(tempDir)) {
				Directory.Delete(tempDir, true);
			}
		}

		
		[Test]
		public async Task CreateSerialisation() {
			var thing = new Thing() {
				id = 3,
				colour = "brown"
			};
			var op = RequestOp.CreateOp(thing, cascade.NowMs);
			var szn = cascade.SerializeRequestOp(op, out var externalContent);
			var sz = szn.ToJsonString();
			Log.Debug(sz);
			const string expected = "{\"Verb\":\"Create\",\"Type\":\"Buzzware.Cascade.Testing.Thing\",\"Id\":3,\"TimeMs\":1000,\"Value\":{\"id\":3,\"name\":null,\"colour\":\"brown\"}}";
			Assert.That(sz,Is.EqualTo(expected));

			var op2 = cascade.DeserializeRequestOp(sz, out var externals);
			Assert.That(op2.Verb,Is.EqualTo(RequestVerb.Create));
			Assert.That(op2.Id,Is.EqualTo(thing.id));
			Assert.That(op2.Type,Is.EqualTo(typeof(Thing)));
			Assert.That(op2.TimeMs, Is.EqualTo(op.TimeMs));
			Assert.That(((Thing)op2.Value).id, Is.EqualTo(thing.id));
			Assert.That(((Thing)op2.Value).colour, Is.EqualTo(thing.colour));
		}
		
		[Test]
		public async Task UpdateSerialisation() {
			var thing = new Thing() {
				id = 5,
				colour = "brown",
				name = "Boris"
			};
			var changes = new Dictionary<string, object>();
			changes["colour"] = "blue";
			changes["name"] = "Winston";
			var op = RequestOp.UpdateOp(thing, changes, cascade.NowMs);
			var szn = cascade.SerializeRequestOp(op, out var externalContent);
			var sz = szn.ToJsonString();
			Log.Debug(sz);
			const string expected = "{\"Verb\":\"Update\",\"Type\":\"Buzzware.Cascade.Testing.Thing\",\"Id\":5,\"TimeMs\":1000,\"Value\":{\"colour\":\"blue\",\"name\":\"Winston\"},\"Extra\":{\"id\":5,\"name\":\"Boris\",\"colour\":\"brown\"}}";
			Assert.That(sz,Is.EqualTo(expected));

			var op2 = cascade.DeserializeRequestOp(sz, out var externals);
			Assert.That(op2.Verb,Is.EqualTo(RequestVerb.Update));
			Assert.That(op2.Id,Is.EqualTo(thing.id));
			Assert.That(op2.Type,Is.EqualTo(typeof(Thing)));
			Assert.That(op2.TimeMs, Is.EqualTo(op.TimeMs));
			var opChanges = op2.Value as Dictionary<string, object>;
			Assert.That(opChanges["colour"], Is.EqualTo(changes["colour"]));
			Assert.That(opChanges["name"], Is.EqualTo(changes["name"]));
		}

		[Test]
		public async Task DestroySerialisation() {
			var thing = new Thing() {
				id = 5,
				colour = "brown",
				name = "Boris"
			};
			// var changes = new Dictionary<string, object>();
			// changes["colour"] = "blue";
			// changes["name"] = "Winston";
			var op = RequestOp.DestroyOp(thing, cascade.NowMs);
			var szn = cascade.SerializeRequestOp(op, out var externalContent);
			var sz = szn.ToJsonString();
			Log.Debug(sz);
			const string expected = "{\"Verb\":\"Destroy\",\"Type\":\"Buzzware.Cascade.Testing.Thing\",\"Id\":5,\"TimeMs\":1000,\"Value\":{\"id\":5,\"name\":\"Boris\",\"colour\":\"brown\"}}";
			Assert.That(sz,Is.EqualTo(expected));

			var op2 = cascade.DeserializeRequestOp(sz, out var externals);
			Assert.That(op2.Verb,Is.EqualTo(RequestVerb.Destroy));
			Assert.That(op2.Id,Is.EqualTo(thing.id));
			Assert.That(op2.Type,Is.EqualTo(typeof(Thing)));
			Assert.That(op2.TimeMs, Is.EqualTo(op.TimeMs));
		}

		public static void AssertRequestOpsMatch(RequestOp op1, RequestOp op2, bool checkValue) {
			Assert.That(op2.Verb, Is.EqualTo(op1.Verb));
			Assert.That(op2.Id, Is.EqualTo(op1.Id));
			Assert.That(op2.Type, Is.EqualTo(op1.Type));
			Assert.That(op2.TimeMs, Is.EqualTo(op1.TimeMs));
			if (checkValue)
				Assert.That(op2.Value!=null, Is.EqualTo(op1.Value!=null));
			Assert.That(op2.Key, Is.EqualTo(op1.Key));

			Assert.That(op2.Populate, Is.EqualTo(op1.Populate));
			Assert.That(op2.FreshnessSeconds, Is.EqualTo(op1.FreshnessSeconds));
			Assert.That(op2.PopulateFreshnessSeconds, Is.EqualTo(op1.PopulateFreshnessSeconds));
			Assert.That(op2.FallbackFreshnessSeconds, Is.EqualTo(op1.FallbackFreshnessSeconds));
			Assert.That(op2.Hold, Is.EqualTo(op1.Hold));
			Assert.That(op2.Criteria, Is.EqualTo(op1.Criteria));
			Assert.That(op2.Params, Is.EqualTo(op1.Params));
		}
		
		
		[Test]
		public async Task BlobPutOpSerialisation() {
			var image = TestUtils.BlobFromBitmap(new Bitmap(10,10),ImageFormat.Png);
			var op = RequestOp.BlobPutOp("first/second/happy_snap", cascade.NowMs,image);
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
			
			var szn = cascade.SerializeRequestOp(op,out var externalContent);
			var sz = szn.ToJsonString();
			Log.Debug(sz);
			const string expected = "{\"Verb\":\"BlobPut\",\"Type\":\"System.Byte[]\",\"Id\":\"first/second/happy_snap\",\"TimeMs\":1000,\"Value\":null}";
			Assert.That(sz,Is.EqualTo(expected));
			Assert.That(externalContent.Count,Is.EqualTo(1));
			Assert.That(externalContent[nameof(RequestOp.Value)],Is.EqualTo(image));

			const string mainFilename = "00000000001000.json";
			var externals = new JsonObject();
			externals["Value"] = cascade.ExternalBinaryPathFromPendingChangePath(mainFilename, "Value");
			szn["externals"] = externals;
			var op2 = cascade.DeserializeRequestOp(szn.ToJsonString(), out var dzexternals);

			AssertRequestOpsMatch(op, op2, checkValue: false);
			Assert.That(op2.Value, Is.Null);
			Assert.That(dzexternals, Has.Count.EqualTo(1));
			Assert.That(dzexternals["Value"], Is.EqualTo("00000000001000__Value.bin"));
		}

		
		[Test]
		public async Task BlobDestroySerialisation()
		{
		    string blobId = "first/second/happy_snap_deleted";
		    var op = RequestOp.BlobDestroyOp(blobId, cascade.NowMs);
		    Assert.That(op.Verb, Is.EqualTo(RequestVerb.BlobDestroy));
		    Assert.That(op.Id, Is.EqualTo(blobId));
		    Assert.That(op.Type, Is.EqualTo(typeof(byte[]))); // For BlobDestroy operation, Type is commonly null
		    Assert.That(op.TimeMs, Is.EqualTo(cascade.NowMs));
		    Assert.That(op.Value, Is.EqualTo(null));
		    
		    var szn = cascade.SerializeRequestOp(op, out var externalContent);
		    var sz = szn.ToJsonString();
		    Log.Debug(sz);
		    const string expected = "{\"Verb\":\"BlobDestroy\",\"Type\":\"System.Byte[]\",\"Id\":\"first/second/happy_snap_deleted\",\"TimeMs\":1000,\"Value\":null}";
		    Assert.That(sz, Is.EqualTo(expected));

		    var op2 = cascade.DeserializeRequestOp(sz, out var externals);
		    Assert.That(op2.Verb, Is.EqualTo(op.Verb));
		    Assert.That(op2.Id, Is.EqualTo(op.Id));
		    Assert.That(op2.TimeMs, Is.EqualTo(op.TimeMs));
		}
		
		[Test]
		public async Task BlobPendingChangesSerialisation() {
			cascade.ConnectionOnline = false;
			
			if (!Directory.Exists(cascade.Config.PendingChangesPath))
				Directory.CreateDirectory(cascade.Config.PendingChangesPath);
			Log.Debug(cascade.Config.PendingChangesPath);
			
			var image = new Bitmap(100,100);
			var imageBlob = TestUtils.BlobFromBitmap(image,ImageFormat.Png);
			var imagePath = "first/second/happy_snap";
			var imageOp = RequestOp.BlobPutOp(imagePath,cascade.NowMs, imageBlob);
			await cascade.AddPendingChange(imageOp);

			var thumbnail = new Bitmap(10,10);
			var thumbnailBlob = TestUtils.BlobFromBitmap(thumbnail,ImageFormat.Png);
			var thumbnailPath = "first/second/happy_snap.thumb10";
			var thumbnailOp = RequestOp.BlobPutOp(thumbnailPath,cascade.NowMs, thumbnailBlob);
			await cascade.AddPendingChange(thumbnailOp);
			
			var thing = new ThingPhoto() {
				id = 3,
				name = "happy snap",
				Image = image,
				imagePath = imagePath,
				Thumbnail = thumbnail,
				thumbnailPath = thumbnailPath
			};
			var thingOp = RequestOp.CreateOp(thing,cascade.NowMs);
			await cascade.AddPendingChange(thingOp);
			
			var changes = await cascade.GetChangesPending();
			
			Assert.That(changes.Count,Is.EqualTo(3));
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
			Assert.That(change.Value,Is.InstanceOf<ThingPhoto>());

			foreach (var ch in changes) {
				await cascade.RemoveChangePending(ch.Item1,ch.Item3?.Values);
			}
			var items = Directory.GetFiles(cascade.Config.PendingChangesPath);
			Assert.That(items.Length,Is.Zero);
		}
		
		[Test]
		public async Task EnqueueOperation() {
			var parent = new Parent() {
				id = 3,
				colour = "red"
			};
			RequestOp requestOpParent = RequestOp.CreateOp(parent,cascade.NowMs);
			var child1 = new Child() {
				id = "c1",
				Parent = parent,
				age = 7,
				weight = 55
			};
			RequestOp requestOpChild1 = RequestOp.CreateOp(child1,cascade.NowMs);
			var child2 = new Child() {
				id = "c2",
				Parent = parent,
				age = 11,
				weight = 77
			};
			RequestOp requestOpChild2 = RequestOp.CreateOp(child2,cascade.NowMs);

			var filepathParent = await cascade.AddPendingChange(requestOpParent);
			Assert.That(Path.GetFileName(filepathParent),Is.EqualTo("000000000001000.json"));
			Assert.That(Directory.GetParent(filepathParent)!.Name, Is.EqualTo("PendingChanges"));
			var parentOp = cascade.DeserializeRequestOp(File.ReadAllText(filepathParent), out var externals1);
			Assert.That(parentOp.Id,Is.EqualTo(3));
			
			var filepathChild1 = await cascade.AddPendingChange(requestOpChild1);
			Assert.That(Path.GetFileName(filepathChild1),Is.EqualTo("000000000001001.json"));
			//Assert.That(Directory.GetParent(filepathChild1)!.Name, Is.EqualTo("Child"));
			var childOp1 = cascade.DeserializeRequestOp(File.ReadAllText(filepathChild1), out var externals2);
			Assert.That(childOp1.Id,Is.EqualTo("c1"));
			
			var filepathChild2 = await cascade.AddPendingChange(requestOpChild2);
			Assert.That(Path.GetFileName(filepathChild2),Is.EqualTo("000000000001002.json"));
			origin.NowMs += 111;
			var child3 = new Child() {
				id = "c3",
				Parent = parent,
				age = 17,
				weight = 99
			};
			RequestOp requestOpChild3 = RequestOp.CreateOp(child3,cascade.NowMs);
			var filepathChild3 = await cascade.AddPendingChange(requestOpChild3);
			Assert.That(Path.GetFileName(filepathChild3),Is.EqualTo("000000000001111.json"));
			
			RequestOp updateOpChild3 = RequestOp.UpdateOp(child3,ImmutableDictionary<string, object>.Empty.Add("age",18),cascade.NowMs);
			var filepathChild3Update = await cascade.AddPendingChange(updateOpChild3);
			Assert.That(Path.GetFileName(filepathChild3Update),Is.EqualTo("000000000001112.json"));
			var updateOpLoaded = cascade.DeserializeRequestOp(File.ReadAllText(filepathChild3Update), out var externals);
			Assert.That(updateOpLoaded.Id,Is.EqualTo("c3"));

			var changesPending = cascade.GetChangesPendingList();
			Assert.That(changesPending,Is.EquivalentTo(new string[] {
				Path.GetFileName(filepathParent),
				Path.GetFileName(filepathChild1),
				Path.GetFileName(filepathChild2),
				Path.GetFileName(filepathChild3),
				Path.GetFileName(filepathChild3Update)
			}));
		}
	}
}
