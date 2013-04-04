﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.SyntaxTree.Literals;
using Lens.SyntaxTree.Translations;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing a method being invoked.
	/// </summary>
	public class InvocationNode : InvocationNodeBase
	{
		/// <summary>
		/// An expression to invoke the method on.
		/// </summary>
		public NodeBase Expression { get; set; }

		private bool m_IsResolved;

		private NodeBase m_InvocationSource;
		private MethodWrapper m_Method;

		private Type[] m_ArgTypes;
		private Type[] m_TypeHints;

		#region Resolve
		
		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			if (!m_IsResolved)
				resolve(ctx);

			return m_Method.ReturnType;
		}

		private void resolve(Context ctx)
		{
			var isParameterless = Arguments.Count == 1 && Arguments[0].GetExpressionType(ctx) == typeof (Unit);

			m_ArgTypes = isParameterless
				? Type.EmptyTypes
				: Arguments.Select(a => a.GetExpressionType(ctx)).ToArray();

			if (Expression is GetMemberNode)
				resolveGetMember(ctx, Expression as GetMemberNode);
			else if (Expression is GetIdentifierNode)
				resolveGetIdentifier(ctx, Expression as GetIdentifierNode);
			else
				resolveExpression(ctx, Expression);

			m_IsResolved = true;
		}

		private void resolveGetMember(Context ctx, GetMemberNode node)
		{
			m_InvocationSource = node.Expression;
			var type = m_InvocationSource != null
				? m_InvocationSource.GetExpressionType(ctx)
				: ctx.ResolveType(node.StaticType);

			if (node.TypeHints.Any())
				m_TypeHints = node.TypeHints.Select(x => x.Signature == "_" ? null : ctx.ResolveType(x)).ToArray();

			try
			{
				// resolve a normal method
				try
				{
					m_Method = ctx.ResolveMethod(type, node.MemberName, m_ArgTypes, m_TypeHints);

					if (m_Method.IsStatic)
						m_InvocationSource = null;

					return;
				}
				catch (KeyNotFoundException)
				{
					if (m_InvocationSource == null)
						throw;
				}

				// resolve a local function that is implicitly used as an extension method
				// move invocation source to arguments
				if (Arguments[0] is UnitNode)
					Arguments[0] = m_InvocationSource;
				else
					Arguments.Insert(0, m_InvocationSource);

				var oldArgTypes = m_ArgTypes;
				m_ArgTypes = Arguments.Select(a => a.GetExpressionType(ctx)).ToArray();
				m_InvocationSource = null;

				try
				{
					m_Method = ctx.ResolveMethod(ctx.MainType.TypeInfo, node.MemberName, m_ArgTypes);
				}
				catch (KeyNotFoundException)
				{
					// resolve a declared extension method
					// most time-consuming operation, therefore is last checked
					m_Method = ctx.ResolveExtensionMethod(type, node.MemberName, oldArgTypes, m_TypeHints);
				}
			}
			catch (AmbiguousMatchException)
			{
				Error(CompilerMessages.TypeMethodInvocationAmbiguous, type, node.MemberName);
			}
			catch (KeyNotFoundException)
			{
				Error(CompilerMessages.TypeMethodNotFound, type, node.MemberName);
			}
		}

		private void resolveGetIdentifier(Context ctx, GetIdentifierNode node)
		{
			var nameInfo = ctx.CurrentScope.FindName(node.Identifier);
			if (nameInfo != null)
			{
				resolveExpression(ctx, node);
				return;
			}

			try
			{
				m_Method = ctx.ResolveMethod(ctx.MainType.TypeInfo, node.Identifier, m_ArgTypes);
				if (m_Method == null)
					throw new KeyNotFoundException();
			}
			catch (KeyNotFoundException)
			{
				Error(CompilerMessages.FunctionNotFound, node.Identifier);
			}
			catch (AmbiguousMatchException)
			{
				Error(CompilerMessages.FunctionInvocationAmbiguous, node.Identifier);
			}
		}

		private void resolveExpression(Context ctx, NodeBase node)
		{
			var exprType = node.GetExpressionType(ctx);
			if (!exprType.IsCallableType())
				Error(CompilerMessages.TypeNotCallable, exprType);

			m_Method = ctx.ResolveMethod(exprType, "Invoke");
			var argTypes = m_Method.ArgumentTypes;
			if (argTypes.Length != m_ArgTypes.Length)
				Error(CompilerMessages.DelegateArgumentsCountMismatch, exprType, argTypes.Length, m_ArgTypes.Length);

			for (var idx = 0; idx < argTypes.Length; idx++)
			{
				var fromType = m_ArgTypes[idx];
				var toType = argTypes[idx];
				if (!toType.IsExtendablyAssignableFrom(fromType))
					Error(Arguments[idx], CompilerMessages.ArgumentTypeMismatch, fromType, toType);
			}

			m_InvocationSource = node;
		}

		#endregion

		public override IEnumerable<NodeBase> GetChildNodes()
		{
			if (Expression != null)
				yield return Expression;

			foreach (var curr in Arguments)
				yield return curr;
		}

		#region Compile
		
		public override void Compile(Context ctx, bool mustReturn)
		{
			if (!m_IsResolved)
				resolve(ctx);

			var gen = ctx.CurrentILGenerator;

			Type constraint = null;

			if (m_InvocationSource != null)
			{
				var type = m_InvocationSource.GetExpressionType(ctx);

				if (type.IsValueType)
				{
					constraint = type;
					if (m_InvocationSource is IPointerProvider)
					{
						(m_InvocationSource as IPointerProvider).PointerRequired = true;
						m_InvocationSource.Compile(ctx, true);
					}
					else
					{
						var tmpVar = ctx.CurrentScope.DeclareImplicitName(ctx, type, true);
						gen.EmitLoadLocal(tmpVar, true);

						m_InvocationSource.Compile(ctx, true);
						gen.EmitSaveObject(type);
						
						gen.EmitLoadLocal(tmpVar, true);
					}
				}
				else
				{
					m_InvocationSource.Compile(ctx, true);
				}
			}

			if (m_ArgTypes.Length > 0)
			{
				var destTypes = m_Method.ArgumentTypes;

				for (var idx = 0; idx < Arguments.Count; idx++)
				{
					var arg = Arguments[idx];
					var argRef = arg is IPointerProvider && (arg as IPointerProvider).PointerRequired;
					var targetRef = destTypes[idx].IsByRef;

					if (argRef != targetRef)
					{
						if(argRef)
							Error(arg, "Cannot use reference as a value for non-reference argument!");
						else
							Error(arg, "Argument {0} requires a reference of type '{1}!", idx+1, destTypes[idx].GetElementType());
					}

					var expr = argRef ? Arguments[idx] : Expr.Cast(Arguments[idx], destTypes[idx]);
					expr.Compile(ctx, true);
				}
			}

			if (constraint != null)
				gen.EmitCall(m_Method.MethodInfo, true, constraint);	
			else
				gen.EmitCall(m_Method.MethodInfo);
		}

		#endregion

		#region Equality members

		protected bool Equals(InvocationNode other)
		{
			return base.Equals(other)
				&& Equals(Expression, other.Expression);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((InvocationNode)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				int hashCode = base.GetHashCode();
				hashCode = (hashCode * 397) ^ (Expression != null ? Expression.GetHashCode() : 0);
				return hashCode;
			}
		}

		#endregion

		public override string ToString()
		{
			return string.Format("invoke({0}, args: {1})", Expression, string.Join(",", Arguments));
		}
	}
}
