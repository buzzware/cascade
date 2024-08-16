
using System.Threading.Tasks;
using Buzzware.Cascade.Testing;
using NUnit.Framework;

namespace Buzzware.Cascade.Test {

  /// <summary>
  /// Tests for validating the <see cref="OpResponse"/> class functionalities, 
  /// specifically item retrieval and result processing capabilities.
  /// </summary>
  [TestFixture]
  public class OpResponseTests {

    /// <summary>
    /// </summary>
    [SetUp]
    public void SetUp() {
    }

    /// <summary>
    /// Tests the <see cref="OpResponse"/> class's ability to handle and return results accurately.
    /// This includes checking if response contains ID results, model results, obtaining the first result,
    /// and verifying the results and result IDs.
    /// </summary>
    [Test]
    public async Task ResultsTest() {

      // Create a collection operation request for the 'Thing' entity.
      var request = RequestOp.GetCollectionOp<Thing>("THINGS", 1000);

      // Create a response object simulating a successful operation with id results.
      var response = new OpResponse(
        request,
        1000,           // Simulated duration of the operation.
        true,           // Indicates if the operation was successful.
        true,           // Indicates if results are id-based.
        1000,           // Processing time.
        new int[] {1,2,3} // The ID results retrieved from the operation.
      );

      Assert.That(response.IsIdResults, Is.True);
      Assert.That(response.IsModelResults, Is.False);
      Assert.That(response.FirstResult, Is.EqualTo(1));
      Assert.That(response.Results, Is.EqualTo(new int[] {1,2,3}));
      Assert.That(response.ResultIds, Is.EqualTo(new int[] {1,2,3}));
    }
  }
}
