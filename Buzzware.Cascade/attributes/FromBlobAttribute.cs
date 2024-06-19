using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Easy.Common.Extensions;

namespace Buzzware.Cascade {
	
	public class FromBlobAttribute : Attribute {
		
		public string PathProperty { get; }
		public IBlobConverter? Converter { get; }
		
		public FromBlobAttribute(string pathProperty, Type? converter) {
			if (!converter.Implements<IBlobConverter>())
				throw new ArgumentException("Converter must implement IBlobConverter");
			PathProperty = pathProperty;
			Converter = converter!=null ? (Activator.CreateInstance(converter) as IBlobConverter)! : null;
		}

		public object? ConvertToPropertyType(byte[] blob, Type destinationPropertyType) {
			return Converter != null ? Converter.Convert(blob, destinationPropertyType) : blob;
		}
	}
}
