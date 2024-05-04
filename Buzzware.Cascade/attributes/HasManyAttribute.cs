using System;

namespace Buzzware.Cascade {
	public class HasManyAttribute : Attribute {
		public string ForeignIdProperty { get; }

		public HasManyAttribute(string foreignIdProperty) {
			ForeignIdProperty = foreignIdProperty;
		}
	}
}
