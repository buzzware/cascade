using System.Collections.Generic;
using System.Threading.Tasks;
using Cascade;
using NUnit.Framework;

namespace Test {

	public class Thing : CascadeModel {
		public long Id { get; set; }
		public string Colour { get; set; }
		public string Size { get; set; }
		public override string GetResourceId() {
			return Id.ToString();
		}
	}

	[TestFixture]
	public class CascadeSimpleReadTests {
		[Test]
		public async Task SimpleRead() {
			var cdl = new CascadeDataLayer();
			cdl.Layers.Add(new MockStore(origin:true,local:true){
				handleOp = async (aOp) => {
					switch (aOp.Verb) {
						case RequestOp.Verbs.Read:							
							var thing = new Thing(){
								Id = CascadeUtils.LongId(aOp.Id),
								Colour = "red",
								Size = "large"
							};
							var resultKey = aOp.ResultKey ?? cdl.GetKeyFrom(thing); 
							return new OpResponse(){
								Connected = true,
								Present = true,
								Index = aOp.Index,
								ResultKey = resultKey,
								Results = new Dictionary<string, object>(){
									{resultKey,thing}
								}
							};
							break;
					}
					return null;
				}
			});
			var opResponse = await cdl.Read<Thing>(new RequestOp() {Id = "1"});
			Assert.That(opResponse.Colour,Is.EqualTo("red"));
			Assert.That(opResponse.Size,Is.EqualTo("large"));
			Assert.That(opResponse.Id,Is.EqualTo(1));
		}
	}

}