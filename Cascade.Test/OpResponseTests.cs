using System.Threading.Tasks;
using Cascade.Test;
using NUnit.Framework;

namespace Cascade.Test {

	[TestFixture]
	public class OpResponseTests {

		[SetUp]
		public void SetUp() {

		}

		[Test]
		public async Task ResultsTest() {
			var request = RequestOp.GetCollectionOp<Thing>("THINGS");
			var response = new OpResponse(
				request,
				1000,
				true,
				true,
				1000,
				new int[] {1,2,3}
			);
			Assert.That(response.IsIdResults,Is.True);
			Assert.That(response.IsModelResults,Is.False);
			Assert.That(response.FirstResult,Is.EqualTo(1));
			Assert.That(response.Results,Is.EqualTo(new int[] {1,2,3}));
			Assert.That(response.ResultIds,Is.EqualTo(new int[] {1,2,3}));
		}
	}
}
