using System;

namespace Cascade {
	public class HasOneAttribute : Attribute {
		public string ForeignIdProperty { get; }

		public HasOneAttribute(string foreignIdProperty) {
			ForeignIdProperty = foreignIdProperty;
		}
	}
}
