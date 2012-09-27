﻿namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	/// <summary>
	/// The variable declaration node.
	/// </summary>
	public class VarNode : NameDeclarationBase
	{
		public VarNode()
		{
			VariableInfo.IsConstant = false;
		}
	}
}