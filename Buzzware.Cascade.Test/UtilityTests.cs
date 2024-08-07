using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Buzzware.Cascade.Testing;
using NUnit.Framework;

namespace Buzzware.Cascade.Test {
	
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

		[Test]
		public void IsEnumerableType() {
			var children = new List<Child>() {
				new Child() {id = "1", age = 1},
				new Child() {id = "2", age = 2},
				new Child() {id = "3", age = 3}
			};
			var parent = new Parent();
			var nullableChildrenType = typeof(Parent).GetProperty(nameof(Parent.Children))!.PropertyType;
			var nonNullableTargetType = CascadeTypeUtils.DeNullType(nullableChildrenType);
			var isEnumerable = CascadeTypeUtils.IsEnumerableType(nonNullableTargetType);
			Assert.That(isEnumerable,Is.True);
		}
	}
}
