// using System.Collections.Generic;
// using System.Threading.Tasks;
// using Buzzware.Cascade;
// using NUnit.Framework;
//
// namespace Test {
// 	[TestFixture]
// 	public class CascadeSimpleReadTests {
// 		CascadeDataLayer cdl;
// 		MockStore origin;
// 		MockStore localStore;
//
// 		private void SetOriginStoreToReturnThing() {
// 			origin = new MockStore(cdl, origin: true, local: false){
// 				handleOp = async (store, aOp) => {
// 					switch (aOp.Verb) {
// 						case RequestVerb.Read:
// 							var thing = new Thing(){
// 								Id = CascadeUtils.LongId(aOp.Id),
// 								Colour = "red",
// 								Size = "large"
// 							};
// 							var resultKey = aOp.ResultKey ?? cdl.GetKeyFrom(thing);
// 							return new OpResponse(){
// 								Connected = true,
// 								Present = true,
// 								Index = aOp.Index,
// 								ResultKey = resultKey,
// 								Results = new Dictionary<string, object?>(){
// 									{resultKey, thing}
// 								}
// 							};
// 							break;
// 					}
//
// 					return null;
// 				}
// 			};
// 			cdl.Layers.Add(origin);
// 		}
// 		
// 		private void SetSimpleLocalStore() {
// 			localStore = new MockStore(cdl, origin: false, local: true){
// 				handleOp = async (store, aOp) => {
// 					switch (aOp.Verb) {
// 						case RequestVerb.Read:
// 							var storeResponse = new OpResponse(){
// 								Connected = true,
// 								Index = aOp.Index,
// 								Present = store.Models.ContainsKey(aOp.Key)
// 							};
// 							if (storeResponse.Present)
// 								storeResponse.SetResult(aOp.Key, store.Models[aOp.Key]);
// 							return storeResponse;
// 							break;
// 						case RequestVerb.ReadAll:
// 							/*
// 							 * resultKey = resultKey || resource
// 							 * if KeyExists(resultKey)
// 							 * 	SetResult(resultKey,KeyGet(resultKey))
// 							 * else
// 							 * 	continue reading upward
// 							 */
// 							break;
// 					}
//
// 					return null;
// 				}
// 			};
// 			cdl.Layers.Add(localStore);
// 		}
// 				
// 		[Test]
// 		public async Task SimpleOneStoreRead() {
// 			cdl = new CascadeDataLayer();
// 			SetOriginStoreToReturnThing();
//
// 			var opResponse = await cdl.Read<Thing>(new RequestOp() {Id = "1"});
// 			
// 			Assert.That(opResponse.Colour,Is.EqualTo("red"));
// 			Assert.That(opResponse.Size,Is.EqualTo("large"));
// 			Assert.That(opResponse.Id,Is.EqualTo(1));
// 		}
//
// 		[Test]
// 		public async Task TwoStoreReadDataNotLocal() {
// 			cdl = new CascadeDataLayer();
// 			SetOriginStoreToReturnThing();
// 			SetSimpleLocalStore();
//
// 			var opResponse = await cdl.ReadResponse<Thing>(new RequestOp() {Id = "1"});
// 			var result = opResponse.ResultObject as Thing;
// 			
// 			Assert.That(opResponse.FromOrigin,Is.True);
// 			Assert.That(result.Colour,Is.EqualTo("red"));
// 			Assert.That(result.Size,Is.EqualTo("large"));
// 			Assert.That(result.Id,Is.EqualTo(1));
// 			
// 			opResponse = await cdl.ReadResponse<Thing>(new RequestOp() {Id = "1"});
// 			result = opResponse.ResultObject as Thing;
//
// 			Assert.That(opResponse.FromOrigin,Is.False);
// 			Assert.That(result.Colour,Is.EqualTo("red"));
// 			Assert.That(result.Size,Is.EqualTo("large"));
// 			Assert.That(result.Id,Is.EqualTo(1));
// 			
// 			opResponse = await cdl.ReadResponse<Thing>(new RequestOp() {Id = "1", Fresh = true});
// 			var result2 = opResponse.ResultObject as Thing;
//
// 			Assert.That(opResponse.FromOrigin,Is.True);
// 			Assert.That(result2.Colour,Is.EqualTo("red"));
// 			Assert.That(result2.Size,Is.EqualTo("large"));
// 			Assert.That(result2.Id,Is.EqualTo(1));			
// 			Assert.That(result2,Is.Not.SameAs(result));
// 		}
// 	}
// }
