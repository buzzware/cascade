using System;

namespace Cascade {
	public class ForeignKeyCriteria {
		public Type ModelType { get; }
		public string ForeignIdProperty { get; }
		public object IdValue { get; }

		public ForeignKeyCriteria(Type modelType, string foreignIdProperty, object idValue) {
			ModelType = modelType;
			ForeignIdProperty = foreignIdProperty;
			IdValue = idValue;
		}
	}
}
