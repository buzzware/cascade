// using System;
// using System.Collections.Generic;
// using System.Threading.Tasks;
// using Cascade;
// using NUnit.Framework;
//
// namespace Cascade.Test {
// 	[TestFixture]
// 	public class SqliteTests {
// 		[Test]
// 		public async Task CreateReadUpdateDeleteTest() {
// 			var path = System.IO.Path.GetTempFileName();
// 			var conn = new SQLite.SQLiteAsyncConnection(path);
// 			var db = new TestDatabase(conn);
// 			await db.Reset();
//
// 			var thing1 = new Parent() {
// 				colour = "red"
// 			};
// 			thing1 = await db.Create<Parent>(thing1);
// 			Assert.Greater(thing1.id,0);
// 			
// 			var loaded = await db.Get<Parent>(thing1.id);
// 			Assert.AreEqual(thing1.id,loaded.id);
//
// 			thing1.colour = "green";
// 			await db.Update(thing1);
//
// 			var thing2 = await db.Get<Parent>(thing1.id);
// 			Assert.AreEqual(thing1.colour,thing2.colour);
//
// 			await db.Delete(thing2);
// 			var thing3 = await db.Get<Parent>(thing2.id);
// 			Assert.IsNull(thing3);
// 		}
// 	}
// }
