﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	public abstract class TypeDefinitionNodeBase : NodeBase, IStartLocationTrackingEntity
	{
		/// <summary>
		/// The name of the type.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// The type builder associated with this type.
		/// </summary>
		public TypeBuilder TypeBuilder { get; private set; }

		#region Methods

		/// <summary>
		/// Prepares the assembly entities for the type.
		/// </summary>
		/// <param name="ctx">Context pointer.</param>
		public void PrepareSelf(Context ctx)
		{
			if(TypeBuilder != null)
				throw new InvalidOperationException(string.Format("Type {0} has already been prepared!", Name));

			TypeBuilder = ctx.MainModule.DefineType(Name, TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed);
		}

		#endregion
	}

	/// <summary>
	/// A base node for algebraic types and records.
	/// </summary>
	public abstract class TypeDefinitionNodeBase<T> : TypeDefinitionNodeBase where T : LocationEntity
	{
		protected TypeDefinitionNodeBase()
		{
			Entries = new List<T>();
		}

		/// <summary>
		/// The entries of the type node.
		/// </summary>
		public List<T> Entries { get; private set; }

		public override LexemLocation EndLocation
		{
			get { return Entries.Last().EndLocation; }
			set { base.EndLocation = value; }
		}

		#region Equality members

		protected bool Equals(TypeDefinitionNodeBase<T> other)
		{
			return string.Equals(Name, other.Name) && Entries.SequenceEqual(other.Entries);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((TypeDefinitionNodeBase<T>)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return ((Name != null ? Name.GetHashCode() : 0) * 397) ^ (Entries != null ? Entries.GetHashCode() : 0);
			}
		}

		#endregion
	}
}