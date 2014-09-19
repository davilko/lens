﻿using System;
using Lens.Compiler;
using Lens.Resolver;

namespace Lens.SyntaxTree.Operators
{
	/// <summary>
	/// A node representing AND, OR or XOR binary operations.
	/// </summary>
	internal class BooleanOperatorNode : BinaryOperatorNodeBase
	{
		#region Constructor

		public BooleanOperatorNode(LogicalOperatorKind kind = default(LogicalOperatorKind))
		{
			Kind = kind;
		}

		#endregion

		#region Fields

		public LogicalOperatorKind Kind;

		#endregion

		#region Operator basics

		protected override bool IsNumericOperator { get { return false; } }

		protected override string OperatorRepresentation
		{
			get { return Kind == LogicalOperatorKind.And ? "&&" : "||"; }
		}

		#endregion

		#region Resolve

		protected override Type resolveOperatorType(Context ctx, Type leftType, Type rightType)
		{
			return leftType.IsImplicitlyBoolean() && rightType.IsImplicitlyBoolean()
				       ? typeof (bool)
				       : null;
		}

		#endregion

		#region Expand

		protected override NodeBase expand(Context ctx, bool mustReturn)
		{
			if (!IsConstant)
			{
				return Kind == LogicalOperatorKind.And
					? Expr.If(LeftOperand, Expr.Block(Expr.Cast<bool>(RightOperand)), Expr.Block(Expr.False()))
					: Expr.If(LeftOperand, Expr.Block(Expr.True()), Expr.Block(Expr.Cast<bool>(RightOperand)));
			}

			return base.expand(ctx, mustReturn);
		}

		protected override void emitOperator(Context ctx)
		{
			throw new InvalidOperationException("The BooleanOperatorNode has not been expanded!");
		}

		#endregion

		#region Constant unroll

		protected override dynamic unrollConstant(dynamic left, dynamic right)
		{
			return Kind == LogicalOperatorKind.And ? left && right : left || right;
		}

		#endregion
	}

	/// <summary>
	/// The kind of boolean operators.
	/// </summary>
	public enum LogicalOperatorKind
	{
		And,
		Or
	}
}
