using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Cascade.Test {
	
	[TestFixture]
	public class UtilityTests {
		
		[SetUp]
		public void SetUp() {
		}
		
		[Test]
		public async Task TypeConversion() {
			Assert.AreEqual((byte)1, CascadeTypeUtils.ConvertTo(typeof(byte),"1",null));
			Assert.AreEqual((int)1, CascadeTypeUtils.ConvertTo(typeof(int),"1",null));
			Assert.AreEqual((long)1, CascadeTypeUtils.ConvertTo(typeof(long),"1",null));
			
			
			Assert.AreEqual("1", CascadeTypeUtils.ConvertTo(typeof(string),(byte)1,null));
			Assert.AreEqual("1", CascadeTypeUtils.ConvertTo(typeof(string),(int)1,null));
			Assert.AreEqual("1", CascadeTypeUtils.ConvertTo(typeof(string),(long)1,null));
			
			Assert.That(CascadeTypeUtils.ConvertTo(typeof(byte),(int)1), Is.EqualTo(1).And.InstanceOf<Byte>());
			Assert.That(CascadeTypeUtils.ConvertTo(typeof(byte),(long)1), Is.EqualTo(1).And.InstanceOf<Byte>());
			
			Assert.That(CascadeTypeUtils.ConvertTo(typeof(long),(byte)1), Is.EqualTo(1).And.InstanceOf<long>());
			Assert.That(CascadeTypeUtils.ConvertTo(typeof(long),(int)1), Is.EqualTo(1).And.InstanceOf<long>());
			Assert.That(CascadeTypeUtils.ConvertTo(typeof(int),(byte)1), Is.EqualTo(1).And.InstanceOf<int>());
			Assert.That(CascadeTypeUtils.ConvertTo(typeof(int),(long)1), Is.EqualTo(1).And.InstanceOf<int>());
		}

		[Test]
		public void IntegerCompatible() {
			Assert.That(CascadeTypeUtils.ValueCompatibleWithType(1, typeof(Int64)), Is.True);
			Assert.That(CascadeTypeUtils.ValueCompatibleWithType(1U, typeof(Int64)), Is.True);
			Assert.That(CascadeTypeUtils.ValueCompatibleWithType(1L, typeof(Int64)), Is.True);
			Assert.That(CascadeTypeUtils.ValueCompatibleWithType(1, typeof(int)), Is.True);
			Assert.That(CascadeTypeUtils.ValueCompatibleWithType(1, typeof(byte)), Is.False);
			Assert.That(CascadeTypeUtils.ValueCompatibleWithType(1L, typeof(Int64)), Is.True);
			Assert.That(CascadeTypeUtils.ValueCompatibleWithType(1, typeof(string)), Is.False);
			Assert.That(CascadeTypeUtils.ValueCompatibleWithType(1L, typeof(string)), Is.False);
			Assert.That(CascadeTypeUtils.ValueCompatibleWithType("1", typeof(string)), Is.True);
			Assert.That(CascadeTypeUtils.ValueCompatibleWithType("1", typeof(int)), Is.False);
		}
	}
}
