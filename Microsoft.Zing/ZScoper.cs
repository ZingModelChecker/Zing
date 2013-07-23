using System;
using System.Collections;
using System.Compiler;

namespace Microsoft.Zing
{
	/// <summary>
	/// Walks a CompilationUnit and creates a scope for each namespace and each type.
	/// The scopes are attached to the corresponding instances via the ScopeFor hash table.
	/// </summary>
	internal class Scoper : System.Compiler.Scoper
	{
		internal Scoper(TrivialHashtable scopeFor)
			: base(scopeFor)
		{
		}

		public override Node VisitUnknownNodeType(Node node)
		{
			if (node == null) return null;

			switch ((ZingNodeType) node.NodeType)
			{
				case ZingNodeType.Array:
				case ZingNodeType.Chan:
				case ZingNodeType.Set:
				case ZingNodeType.Range:
					return base.VisitTypeNode((TypeNode) node);
				default:
					return base.VisitUnknownNodeType(node);
			}
		}
	}
}
