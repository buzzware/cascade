using System;

namespace Cascade {
	public class HasManyAttribute : Attribute {
		public string ForeignIdProperty { get; }

		public HasManyAttribute(string foreignIdProperty) {
			ForeignIdProperty = foreignIdProperty;
		}
	}
}
