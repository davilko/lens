﻿using System;
using Lens.Compiler;
using Lens.Resolver;

namespace Lens.SyntaxTree.Operators.Unary
{
    /// <summary>
    /// A node representing the boolean inversion operator.
    /// </summary>
    internal class InversionOperatorNode : UnaryOperatorNodeBase
    {
        #region Operator basics

        protected override string OperatorRepresentation => "not";

        #endregion

        #region Resolve

        protected override Type ResolveOperatorType(Context ctx)
        {
            return Operand.Resolve(ctx).IsImplicitlyBoolean() ? typeof(bool) : null;
        }

        #endregion

        #region Transform

        protected override NodeBase Expand(Context ctx, bool mustReturn)
        {
            var op = Operand as InversionOperatorNode;
            if (op != null)
                return op.Operand;

            return base.Expand(ctx, mustReturn);
        }

        #endregion

        #region Emit

        protected override void EmitOperator(Context ctx)
        {
            var gen = ctx.CurrentMethod.Generator;

            Expr.Cast<bool>(Operand).Emit(ctx, true);

            gen.EmitConstant(0);
            gen.EmitCompareEqual();
        }

        #endregion

        #region Constant unroll

        protected override dynamic UnrollConstant(dynamic value)
        {
            return !value;
        }

        #endregion
    }
}