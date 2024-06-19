using System;
using System.Threading.Tasks;
using Easy.Common.Extensions;

namespace Buzzware.Cascade {

	public interface IPropertyConverter {
		Task<object?> Convert(object? input, Type destinationType, params object[] args);
	}

	public class FromPropertyAttribute : Attribute {

		public string SourcePropertyName { get; }
		public IPropertyConverter? Converter { get; }
		public object[] Arguments { get; }
		
		public FromPropertyAttribute(string sourcePropertyName, Type converterClass, params object[] args) {
			if (!converterClass.Implements<IPropertyConverter>())
				throw new ArgumentException("Converter must implement IBlobConverter");
			SourcePropertyName = sourcePropertyName;
			Converter = Activator.CreateInstance(converterClass) as IPropertyConverter;
			Arguments = args;
		}
	}
}
