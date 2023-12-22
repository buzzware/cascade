using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Security;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Cascade.Test;
using NUnit.Framework;
using Serilog;
using StandardExceptions;

namespace Cascade.Test {
	[TestFixture]
	public class PendingChangesTests {
		
		
		MockOrigin origin;
		private CascadeDataLayer cascade;
		private string tempDir;
		private CascadeJsonSerialization serialization;

		[SetUp]
		public void SetUp() {
			tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
			Log.Debug($"Cascade cache directory {tempDir}");
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
			var sz = cascade.SerializeRequestOp(op);
			Log.Debug(sz);
			const string expected = "{\"Verb\":\"Create\",\"Type\":\"Cascade.Test.Thing\",\"Id\":3,\"TimeMs\":1000,\"Value\":{\"id\":3,\"name\":null,\"colour\":\"brown\"}}";
			Assert.That(sz,Is.EqualTo(expected));

			var op2 = cascade.DeserializeRequestOp(sz);
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
			var sz = cascade.SerializeRequestOp(op);
			Log.Debug(sz);
			const string expected = "{\"Verb\":\"Update\",\"Type\":\"Cascade.Test.Thing\",\"Id\":5,\"TimeMs\":1000,\"Value\":{\"colour\":\"blue\",\"name\":\"Winston\"},\"Extra\":{\"id\":5,\"name\":\"Boris\",\"colour\":\"brown\"}}";
			Assert.That(sz,Is.EqualTo(expected));

			var op2 = cascade.DeserializeRequestOp(sz);
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
			var sz = cascade.SerializeRequestOp(op);
			Log.Debug(sz);
			const string expected = "{\"Verb\":\"Destroy\",\"Type\":\"Cascade.Test.Thing\",\"Id\":5,\"TimeMs\":1000,\"Value\":{\"id\":5,\"name\":\"Boris\",\"colour\":\"brown\"}}";
			Assert.That(sz,Is.EqualTo(expected));

			var op2 = cascade.DeserializeRequestOp(sz);
			Assert.That(op2.Verb,Is.EqualTo(RequestVerb.Destroy));
			Assert.That(op2.Id,Is.EqualTo(thing.id));
			Assert.That(op2.Type,Is.EqualTo(typeof(Thing)));
			Assert.That(op2.TimeMs, Is.EqualTo(op.TimeMs));
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
			var parentOp = cascade.DeserializeRequestOp(File.ReadAllText(filepathParent));
			Assert.That(parentOp.Id,Is.EqualTo(3));
			
			var filepathChild1 = await cascade.AddPendingChange(requestOpChild1);
			Assert.That(Path.GetFileName(filepathChild1),Is.EqualTo("000000000001001.json"));
			//Assert.That(Directory.GetParent(filepathChild1)!.Name, Is.EqualTo("Child"));
			var childOp1 = cascade.DeserializeRequestOp(File.ReadAllText(filepathChild1));
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
			var updateOpLoaded = cascade.DeserializeRequestOp(File.ReadAllText(filepathChild3Update));
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
