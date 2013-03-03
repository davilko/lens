﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Lens.SyntaxTree.SyntaxTree;
using Lens.SyntaxTree.SyntaxTree.ControlFlow;

namespace Lens.SyntaxTree.Compiler
{
	public partial class Context
	{
		#region Methods

		/// <summary>
		/// Creates a new type entity with given name.
		/// </summary>
		internal TypeEntity CreateType(string name, string parent = null, bool isSealed = false, bool defaultCtor = true)
		{
			var te = createTypeCore(name, isSealed, defaultCtor);
			te.ParentSignature = parent;
			return te;
		}

		/// <summary>
		/// Creates a new type entity with given name and a resolved type for parent.
		/// </summary>
		internal TypeEntity CreateType(string name, Type parent, bool isSealed = false, bool defaultCtor = true)
		{
			var te = createTypeCore(name, isSealed, defaultCtor);
			te.Parent = parent;
			return te;
		}

		/// <summary>
		/// Resolves a type by its string signature.
		/// Warning: this method might return a TypeBuilder as well as a Type, if the signature points to an inner type.
		/// </summary>
		public Type ResolveType(string signature)
		{
			try
			{
				TypeEntity type;
				return _DefinedTypes.TryGetValue(signature, out type)
					       ? type.TypeBuilder
					       : _TypeResolver.ResolveType(signature);
			}
			catch (ArgumentException ex)
			{
				throw new LensCompilerException(ex.Message);
			}
		}

		/// <summary>
		/// Tries to search for a declared type.
		/// </summary>
		internal TypeEntity FindType(string name)
		{
			TypeEntity entity;
			_DefinedTypes.TryGetValue(name, out entity);
			return entity;
		}

		/// <summary>
		/// Resolves a method by its name and agrument list.
		/// </summary>
		public MethodInfo ResolveMethod(string typeName, string methodName, Type[] args = null)
		{
			return ResolveMethod(ResolveType(typeName), methodName, args);
		}

		/// <summary>
		/// Resolves a method by its name and agrument list.
		/// </summary>
		public MethodInfo ResolveMethod(Type type, string methodName, Type[] args = null)
		{
			if(args == null)
				args = new Type[0];

			var method = type is TypeBuilder
				? _DefinedTypes[type.Name].ResolveMethod(methodName, args)
				: type.GetMethod(methodName, args);

			if(method == null)
				throw new KeyNotFoundException(string.Format("Type '{0}' does not contain any method named '{1}'.", type, methodName));

			return method;
		}

		/// <summary>
		/// Resolves a group of methods by the name.
		/// </summary>
		public MethodInfo[] ResolveMethodGroup(Type type, string methodName)
		{
			var group = type is TypeBuilder
				? _DefinedTypes[type.Name].ResolveMethodGroup(methodName)
				: type.GetMethods().Where(m => m.Name == methodName).ToArray();

			if(group == null || group.Length == 0)
				throw new KeyNotFoundException(string.Format("Type '{0}' does not contain any method named '{1}'.", type, methodName));

			return group;
		}

		/// <summary>
		/// Resolves a field by its name.
		/// </summary>
		public FieldInfo ResolveField(string typeName, string fieldName)
		{
			return ResolveField(ResolveType(typeName), fieldName);
		}

		/// <summary>
		/// Resolves a field by its name.
		/// </summary>
		public FieldInfo ResolveField(Type type, string fieldName)
		{
			var field = type is TypeBuilder
				? _DefinedTypes[type.Name].ResolveField(fieldName)
				: type.GetField(fieldName);

			if(field == null)
				throw new KeyNotFoundException(string.Format("Type '{0}' does not contain any field named '{1}'.", type, fieldName));

			return field;
		}

		/// <summary>
		/// Resolves a property by its name.
		/// </summary>
		public PropertyInfo ResolveProperty(Type type, string propertyName)
		{
			var pty = type is TypeBuilder
				? null
				: type.GetProperty(propertyName);

			if(pty == null)
				throw new KeyNotFoundException(string.Format("Type '{0}' does not contain any property named '{1}'.", type, propertyName));

			return pty;
		}

		/// <summary>
		/// Resolves a constructor by it's argument list.
		/// </summary>
		public ConstructorInfo ResolveConstructor(string typeName, Type[] args = null)
		{
			return ResolveConstructor(ResolveType(typeName), args);
		}

		/// <summary>
		/// Resolves a constructor by agrument list.
		/// </summary>
		public ConstructorInfo ResolveConstructor(Type type, Type[] args = null)
		{
			if (args == null)
				args = new Type[0];

			var ctor = type is TypeBuilder
				? _DefinedTypes[type.Name].ResolveConstructor(args)
				: type.GetConstructor(args);

			if(ctor == null)
				throw new KeyNotFoundException(string.Format("Type '{0}' does not contain a constructor with the given arguments.", type));

			return ctor;
		}

		/// <summary>
		/// Resolves a type by its signature.
		/// </summary>
		public Type ResolveType(TypeSignature signature)
		{
			try
			{
				return ResolveType(signature.Signature);
			}
			catch (LensCompilerException ex)
			{
				ex.BindToLocation(signature);
				throw;
			}
		}

		/// <summary>
		/// Declares a new type.
		/// </summary>
		public void DeclareType(TypeDefinitionNode node)
		{
			var root = CreateType(node.Name);
			root.Kind = TypeEntityKind.Type;

			foreach (var curr in node.Entries)
			{
				var currType = CreateType(curr.Name, node.Name, true);
				currType.Kind = TypeEntityKind.TypeLabel;
				if (curr.IsTagged)
					currType.CreateField("Tag", curr.TagType);
			}
		}

		/// <summary>
		/// Declares a new record.
		/// </summary>
		public void DeclareRecord(RecordDefinitionNode node)
		{
			var root = CreateType(node.Name, isSealed: true);
			root.Kind = TypeEntityKind.Record;

			foreach (var curr in node.Entries)
				root.CreateField(curr.Name, curr.Type);
		}

		/// <summary>
		/// Declares a new function.
		/// </summary>
		public void DeclareFunction(FunctionNode node)
		{
			var method = MainType.CreateMethod(node.Name, node.Arguments, true);
			method.Body = node.Body;
		}

		/// <summary>
		/// Opens a new namespace for current script.
		/// </summary>
		public void DeclareOpenNamespace(UsingNode node)
		{
			_TypeResolver.AddNamespace(node.Namespace);
		}

		/// <summary>
		/// Adds a new node to the main script's body.
		/// </summary>
		public void DeclareScriptNode(NodeBase node)
		{
			MainMethod.Body.Add(node);
		}

		#endregion

		#region Helpers

		/// <summary>
		/// Generates a unique assembly name.
		/// </summary>
		private static string getAssemblyName()
		{
			lock (typeof(Context))
				_AssemblyId++;
			return "_CompiledAssembly" + _AssemblyId;
		}

		/// <summary>
		/// Traverses the syntactic tree, searching for closures and curried methods.
		/// </summary>
		private void processClosures()
		{
			var types = _DefinedTypes.ToArray();

			// ProcessClosures() usually processes new types, hence the caching to array
			foreach (var currType in types)
				currType.Value.ProcessClosures();
		}

		/// <summary>
		/// Prepares the assembly entities for the type list.
		/// </summary>
		private void prepareEntities()
		{
			// prepare types first
			foreach (var curr in _DefinedTypes)
				curr.Value.PrepareSelf();

			foreach (var curr in _DefinedTypes)
				curr.Value.PrepareMembers();
		}

		/// <summary>
		/// Compiles the source code for all the declared classes.
		/// </summary>
		private void compileCore()
		{
			foreach (var curr in _DefinedTypes)
				curr.Value.Compile();
		}

		/// <summary>
		/// Create a type entry without setting its parent info.
		/// </summary>
		private TypeEntity createTypeCore(string name, bool isSealed, bool defaultCtor)
		{
			if (_DefinedTypes.ContainsKey(name))
				Error("Type '{0}' has already been defined!", name);

			var te = new TypeEntity(this)
			{
				Name = name,
				IsSealed = isSealed,
			};
			_DefinedTypes.Add(name, te);

			if (defaultCtor)
				te.CreateConstructor();

			return te;
		}

		/// <summary>
		/// Finalizes the assembly.
		/// </summary>
		private void finalizeAssembly()
		{
//			var ep = ResolveMethod(RootTypeName, RootMethodName);
//			MainAssembly.SetEntryPoint(ep, PEFileKinds.ConsoleApplication);
			foreach (var curr in _DefinedTypes)
				curr.Value.TypeBuilder.CreateType();
		}

		#endregion
	}
}