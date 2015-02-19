﻿namespace Lens.Compiler.Entities
{
	internal static class EntityNames
	{
		/// <summary>
		/// The template for an assembly generated by LENS.
		/// </summary>
		public const string AssemblyNameTemplate = "_CompiledAssembly{0}";

		/// <summary>
		/// The name of the main type in the assembly.
		/// </summary>
		public const string MainTypeName = "<Script>";

		/// <summary>
		/// The name of the main method in which the code is situated.
		/// </summary>
		public const string RunMethodName = "Run";

		/// <summary>
		/// The name of the assembly entry point (when it's saved as exe).
		/// </summary>
		public const string EntryPointMethodName = "Main";

		/// <summary>
		/// The name of a field that contains a pointer to root type.
		/// </summary>
		public const string ParentScopeFieldName = "<root>";

		/// <summary>
		/// The template for implicitly defined local variables.
		/// </summary>
		public const string ImplicitVariableNameTemplate = "<loc_{0}>";

		/// <summary>
		/// The template for closure type field names.
		/// </summary>
		public const string ClosureFieldNameTemplate = "<cf_{0}_{1}>";

		/// <summary>
		/// The template for closure type names.
		/// </summary>
		public const string ClosureTypeNameTemplate = "<ct_{0}>";

		/// <summary>
		/// The template for closure method names.
		/// </summary>
		public const string ClosureMethodNameTemplate = "<cm_{0}_{1}>";

		/// <summary>
		/// The template for pure methods.
		/// Actual method name is used for the newly generated wrapper, and the original method is renamed using this template.
		/// </summary>
		public const string PureMethodNameTemplate = "<pure_{0}>";

		/// <summary>
		/// The template for a field name which is used to store cached results of pure functions.
		/// </summary>
		public const string PureMethodCacheNameTemplate = "<pc_{0}>";

		/// <summary>
		/// The template for a field name which is used to store the flag indicating the pure function's result has been calculated.
		/// </summary>
		public const string PureMethodCacheFlagNameTemplate = "<pcf_{0}>";

		/// <summary>
		/// The template for an autogenerated lamda's argument name.
		/// </summary>
		internal const string AnonymousArgumentTemplate = "<arg_{0}>";
	}
}
