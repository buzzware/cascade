using System.Collections.Generic;
using System.Threading.Tasks;
using Cascade;
using NUnit.Framework;

namespace Test {

	public class Thing : CascadeModel {
		public long Id { get; set; }
		public string Colour { get; set; }
		public string Size { get; set; }
		public override string CascadeId() {
			return Id.ToString();
		}
	}

	[TestFixture]
	public class CascadeSimpleReadTests {
		//[Test]
		public async Task SimpleOneStoreRead() {
			var cdl = new CascadeDataLayer();
			cdl.Layers.Add(new MockStore(cdl,origin:true,local:true){
				handleOp = async (store,aOp) => {
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

		[Test]
		public async Task TwoStoreReadDataNotLocal() {
			var cdl = new CascadeDataLayer();
			var origin = new MockStore(cdl,origin: true, local: false){
				handleOp = async (store,aOp) => {
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
									{resultKey, thing}
								}
							};
							break;
					}
					return null;
				}
			};
			cdl.Layers.Add(origin);
			
			var localStore = new MockStore(cdl,origin: false, local: true){
				handleOp = async (store,aOp) => {
					switch (aOp.Verb) {
						case RequestOp.Verbs.Read:
							var storeResponse = new OpResponse(){
								Connected = true,
								Index = aOp.Index,
								Present = store.cache.ContainsKey(aOp.Key)
							};							
							if (storeResponse.Present)
								storeResponse.SetResult(aOp.Key,store.cache[aOp.Key]);
							return storeResponse;
							break;
					}
					return null;
				}
			};
			cdl.Layers.Add(localStore);
						
			var opResponse = await cdl.ReadResponse<Thing>(new RequestOp() {Id = "1"});
			var result = opResponse.ResultObject as Thing;
			
			Assert.That(opResponse.FromOrigin,Is.True);
			Assert.That(result.Colour,Is.EqualTo("red"));
			Assert.That(result.Size,Is.EqualTo("large"));
			Assert.That(result.Id,Is.EqualTo(1));
			
			opResponse = await cdl.ReadResponse<Thing>(new RequestOp() {Id = "1"});
			result = opResponse.ResultObject as Thing;

			Assert.That(opResponse.FromOrigin,Is.False);
			Assert.That(result.Colour,Is.EqualTo("red"));
			Assert.That(result.Size,Is.EqualTo("large"));
			Assert.That(result.Id,Is.EqualTo(1));
			
			opResponse = await cdl.ReadResponse<Thing>(new RequestOp() {Id = "1", Fresh = true});
			var result2 = opResponse.ResultObject as Thing;

			Assert.That(opResponse.FromOrigin,Is.True);
			Assert.That(result2.Colour,Is.EqualTo("red"));
			Assert.That(result2.Size,Is.EqualTo("large"));
			Assert.That(result2.Id,Is.EqualTo(1));			
			Assert.That(result2,Is.Not.SameAs(result));
		}
	}
}