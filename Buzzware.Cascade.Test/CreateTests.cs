
using System;
using System.Threading.Tasks;
using Buzzware.Cascade.Testing;
using NUnit.Framework;
using Buzzware.StandardExceptions;

namespace Buzzware.Cascade.Test {
	
	/// <summary>
	/// Tests related to creating and replacing entities
	/// </summary>
	[TestFixture]
	public class CreateTests {
		
		MockOrigin origin;

		/// <summary>
		/// Sets up a MockOrigin to handle requests
		/// </summary>
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
					timeMs: nowMs,
					exists: true,
					arrivedAtMs: nowMs, result: parent));
			});
		}
		
		/// <summary>
		/// Tests the creation of a Parent object using the Cascade library without using any caching.
		/// Ensures that the object is created with the expected attributes.
		/// </summary>
		[Test]
		public async Task CreateWithoutCache() {

			// Initialize CascadeDataLayer without any cache.
			var cascade = new CascadeDataLayer(origin,new ICascadeCache[] {}, new CascadeConfig(), new MockCascadePlatform(), ErrorControl.Instance, new CascadeJsonSerialization());
			
			// Create a Parent object with a specific color.
			var parent = new Parent() {
				colour = "red"
			};
			
			// Send create request and await the response.
			parent = await cascade.Create<Parent>(parent);
			
			// Verify the Parent object is created with the expected id and colour.
			Assert.AreEqual(5,parent!.id);
			Assert.AreEqual("red",parent!.colour);
		}
		
		/// <summary>
		/// Tests the creation and subsequent replacement of a Parent object using Cascade without using any caching.
		/// Verifies the replace operation updates the entity's attributes as expected.
		/// </summary>
		[Test]
		public async Task CreateReplaceWithoutCache() {

			// Initialize CascadeDataLayer without any cache.
			var cascade = new CascadeDataLayer(origin,new ICascadeCache[] {}, new CascadeConfig(), new MockCascadePlatform(), ErrorControl.Instance, new CascadeJsonSerialization());
			
			// Create a Parent object and verify its creation.
			var parent = new Parent() {
				colour = "red"
			};
			parent = await cascade.Create(parent);

			// Prepare a new Parent object with the same id but different colour for replacement.
			var parent2 = new Parent() {
				id = parent.id,
				colour = "green"
			};
			
			// Send replace request and await the response.
			parent2 = await cascade.Replace(parent2);
			
			// Verify the Parent object is updated with the new color while retaining the same id.
			Assert.AreEqual(5,parent2.id);
			Assert.AreEqual("green",parent2!.colour);
		}
	}
}
