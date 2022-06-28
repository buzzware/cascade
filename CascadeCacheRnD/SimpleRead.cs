using System;
using System.Threading.Tasks;
using Cascade;
using NUnit.Framework;
using Test;

namespace CascadeCacheRnD {
	
	public class MockOrigin : ICascadeOrigin {
		public async Task<OpResponse> ProcessRequest(RequestOp request) {
			var now = CascadeUtils.NowMs;
			var thing = new Thing();
			thing.Id = request.IdAsInt ?? 0; 
			return new OpResponse(
				requestOp: request,
				now,
				connected: true,
				present: true,
				result: thing,
				arrivedAtMs: now
			);
		}
	}

	[TestFixture]
	public class SimpleRead {
		[Test]
		public async Task ReadWithoutCache() {
			var origin = new MockOrigin();
			var cascade = new CascadeDataLayer(origin,new ICascadeCache[] {}, new CascadeConfig());
			var thing = await cascade.Read<Thing>(5);
			Assert.AreEqual(5,thing!.Id);
		}
	}

}
