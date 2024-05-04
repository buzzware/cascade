using System;

namespace Buzzware.Cascade {
	public class BelongsToAttribute : Attribute {
		public string IdProperty { get; }

		public BelongsToAttribute(string idProperty) {
			IdProperty = idProperty;
		}
	}
}
