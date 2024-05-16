using System;
using Easy.Common.Extensions;

namespace Buzzware.Cascade {
	
	public class FromBlobAttribute : Attribute {
		
		public string PathProperty { get; }
		public IBlobConverter Converter { get; }
		
		public FromBlobAttribute(string pathProperty, Type converter) {
			if (!converter.Implements<IBlobConverter>())
				throw new ArgumentException("Converter must implement IBlobConverter");
			PathProperty = pathProperty;
			Converter = (Activator.CreateInstance(converter) as IBlobConverter)!;
		}
	}
}
