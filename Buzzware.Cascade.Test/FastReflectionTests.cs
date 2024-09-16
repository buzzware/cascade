using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Buzzware.Cascade.Testing;
using NUnit.Framework;

namespace Buzzware.Cascade.Test {

	/// <summary>
	/// Test suite for utility functions in the Cascade library
	/// </summary>
	[TestFixture]
	public class FastReflectionTests {

		/// <summary>
		/// Tests ConvertTo for converting various data types to other types accurately.
		/// </summary>
		[Test]
		public async Task SimpleTest() {
			var props = FastReflection.GetClassInfo<Child>();
			var dataAndIdNames = new string[] {
				nameof(Child.id),
				nameof(Child.level),
				nameof(Child.power),
				nameof(Child.tally),
				nameof(Child.parentId),
				nameof(Child.updatedAtMs),
			};
			var associationNames = new string[] {
				nameof(Child.Detail),
				nameof(Child.Parent),
			};
			var utilityNames = new string[] {
				nameof(Child.__ProxyFor),
				nameof(Child.__mutable),
				nameof(Child.__HasChanges),
			};
			
			var allPropertynames = dataAndIdNames.ToList();
			allPropertynames.AddRange(associationNames);
			allPropertynames.AddRange(utilityNames);
			
			Assert.That(props.AllPropertyInfos.Keys, Is.EquivalentTo(allPropertynames));
			Assert.That(props.DataAndIdNames, Is.EquivalentTo(dataAndIdNames));
			Assert.That(props.Associationinfos.Keys, Is.EquivalentTo(associationNames));
			Assert.That(props.DataAndAssociationInfos.Keys, Is.EquivalentTo(props.DataAndIdNames.Concat(props.Associationinfos.Keys)));
		}

	}
}
