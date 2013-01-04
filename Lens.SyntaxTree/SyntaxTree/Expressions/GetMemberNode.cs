﻿using System;
using Lens.SyntaxTree.Compiler;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing read access to a member of a type, either field or property.
	/// </summary>
	public class GetMemberNode : MemberNodeBase, IEndLocationTrackingEntity
	{
		public override Type GetExpressionType(Context ctx)
		{
			throw new NotImplementedException();
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			throw new NotImplementedException();
		}

		public override string ToString()
		{
			return StaticType == null
				? string.Format("getmbr({0} of value {1})", MemberName, Expression)
				: string.Format("getmbr({0} of type {1})", MemberName, StaticType);
		}
	}
}