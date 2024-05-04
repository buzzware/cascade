using System;

namespace Buzzware.Cascade {
	public class HasOneAttribute : Attribute {
		public string ForeignIdProperty { get; }

		public HasOneAttribute(string foreignIdProperty) {
			ForeignIdProperty = foreignIdProperty;
		}
	}
}
