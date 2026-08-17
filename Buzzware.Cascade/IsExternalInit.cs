// Somehow this is necessary to support init; acessors in properties on older C# versions than 9
namespace System.Runtime.CompilerServices
{
	/// <summary>
	/// Compiler shim enabling C# 9 init-only property setters when targeting netstandard2.0, which lacks this type.
	/// </summary>
	public class IsExternalInit { }
}
