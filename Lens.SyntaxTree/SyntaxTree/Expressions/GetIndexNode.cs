﻿using System;
using System.Collections.Generic;
using Lens.SyntaxTree.Compiler;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing a read-access to an array or list's value.
	/// </summary>
	public class GetIndexNode : IndexNodeBase, IEndLocationTrackingEntity
	{
		public override Type GetExpressionType(Context ctx)
		{
			if (m_ExpressionType != null)
				return m_ExpressionType;

			var exprType = Expression.GetExpressionType(ctx);
			if (exprType.IsArray)
			{
				m_ExpressionType = exprType.GetElementType();
			}
			else if (exprType.IsGenericType)
			{
				var gt = exprType.GetGenericTypeDefinition();
				var args = exprType.GetGenericArguments();
				if (gt == typeof (List<>))
					m_ExpressionType = args[0];
				else if (gt == typeof (Dictionary<,>))
					m_ExpressionType = args[1];
			}
			else
			{
				Error("Type '{0}' cannot be indexed.", exprType);
			}

			return m_ExpressionType;
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			throw new NotImplementedException();
		}

		public override string ToString()
		{
			return string.Format("getidx({0} of {1})", Index, Expression);
		}
	}
}