using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cascade;
using Cascade.testing;
using NUnit.Framework;

namespace Test {
	[TestFixture]
	public class SqliteTests {
		[Test]
		public async Task CreateReadTest() {
			var path = System.IO.Path.GetTempFileName();
			var conn = new SQLite.SQLiteAsyncConnection(path);
			var db = new TestDatabase(conn);
			await db.Reset();

			var thing1 = new Thing() {
				Colour = "red"
			};
			thing1 = await db.Create<Thing>(thing1);
			Assert.Greater(thing1.Id,0);
			
			var loaded = await db.Get<Thing>(thing1.Id);
			Assert.AreEqual(thing1.Id,loaded.Id);
		}
	}
}
