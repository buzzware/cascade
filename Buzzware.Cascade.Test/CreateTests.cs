using System;
using System.Threading.Tasks;
using Buzzware.Cascade.Testing;
using NUnit.Framework;
using Buzzware.StandardExceptions;

namespace Buzzware.Cascade.Test {
	
	[TestFixture]
	public class CreateTests {
		
		MockOrigin origin;

		[SetUp]
		public void SetUp() {
			origin = new MockOrigin(nowMs:1000,handleRequest: (origin, requestOp) => {
				var nowMs = origin.NowMs;
				var reqParent = (requestOp.Value as Parent)!;
				Parent parent;
				switch (requestOp.Verb) {
					case RequestVerb.Create:
						parent = new Parent() {
							id = 5,
							colour = reqParent.colour,
							updatedAtMs = nowMs
						};
						break;
					case RequestVerb.Replace:
						parent = new Parent() {
							id = reqParent.id,
							colour = reqParent.colour,
							updatedAtMs = nowMs
						};
						break;
					default:
						throw new NotImplementedException("Verb not implemented");
				}
				return Task.FromResult(new OpResponse(
					requestOp: requestOp,
					nowMs,
					connected: true,
					exists: true,
					result: parent,
					arrivedAtMs: nowMs
				));
			});
		}
		
		[Test]
		public async Task CreateWithoutCache() {
			var cascade = new CascadeDataLayer(origin,new ICascadeCache[] {}, new CascadeConfig(), new MockCascadePlatform(), ErrorControl.Instance, new CascadeJsonSerialization());
			var parent = new Parent() {
				colour = "red"
			};
			parent = await cascade.Create<Parent>(parent);
			Assert.AreEqual(5,parent!.id);
			Assert.AreEqual("red",parent!.colour);
		}
		
		[Test]
		public async Task CreateReplaceWithoutCache() {
			var cascade = new CascadeDataLayer(origin,new ICascadeCache[] {}, new CascadeConfig(), new MockCascadePlatform(), ErrorControl.Instance, new CascadeJsonSerialization());
			var parent = new Parent() {
				colour = "red"
			};
			parent = await cascade.Create(parent);

			var parent2 = new Parent() {
				id = parent.id,
				colour = "green"
			};
			parent2 = await cascade.Replace(parent2);
			Assert.AreEqual(5,parent2.id);
			Assert.AreEqual("green",parent2!.colour);
		}
	}
}
