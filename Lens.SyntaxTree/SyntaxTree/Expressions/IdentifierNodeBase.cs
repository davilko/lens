﻿using Lens.SyntaxTree.Compiler;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// The base node for identifier access.
	/// </summary>
	public abstract class IdentifierNodeBase : NodeBase
	{
		/// <summary>
		/// Identifier to access.
		/// </summary>
		public string Identifier { get; set; }

		public LocalName LocalName { get; set; }

		public override void ProcessClosures(Context ctx)
		{
			base.ProcessClosures(ctx);

			try
			{
				ctx.CurrentScope.ReferenceName(Identifier ?? LocalName.Name);
			}
			catch { }
		}

		#region Equality members

		protected bool Equals(IdentifierNodeBase other)
		{
			return string.Equals(Identifier, other.Identifier);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((IdentifierNodeBase)obj);
		}

		public override int GetHashCode()
		{
			return (Identifier != null ? Identifier.GetHashCode() : 0);
		}

		#endregion
	}
}
