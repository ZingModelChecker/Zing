using System.Compiler;

namespace Microsoft.Zing
{
    /// <summary>
    /// Walks the statement list of a Block, entering any declarations into the associated scope. Does not recurse.
    /// This visitor is instantiated and called by Looker.
    /// </summary>
    internal sealed class Declarer : System.Compiler.Declarer
    {
        public Declarer(Zing.ErrorHandler errorHandler)
            : base(errorHandler)
        {
        }

        public override Node Visit(Node node)
        {
            if (node == null) return null;
            switch (((ZingNodeType)node.NodeType))
            {
                // None of these nodes may contain variable declarations, so we don't need to
                // explore them. If an atomic block (erroneously) contains declarations, we'll
                // discover that in the "checker" phase.

                case ZingNodeType.Array:
                case ZingNodeType.Accept:
                case ZingNodeType.Assert:
                case ZingNodeType.Assume:
                case ZingNodeType.Async:
                case ZingNodeType.Atomic:
                case ZingNodeType.Chan:
                case ZingNodeType.Choose:
                case ZingNodeType.Event:
                case ZingNodeType.EventPattern:
                case ZingNodeType.JoinStatement:
                case ZingNodeType.InvokePlugin:
                case ZingNodeType.InvokeSched:
                case ZingNodeType.Range:
                case ZingNodeType.ReceivePattern:
                case ZingNodeType.Select:
                case ZingNodeType.Self:
                case ZingNodeType.Send:
                case ZingNodeType.Set:
                case ZingNodeType.TimeoutPattern:
                case ZingNodeType.Trace:
                case ZingNodeType.Try:
                case ZingNodeType.WaitPattern:
                case ZingNodeType.With:
                case ZingNodeType.Yield:
                case ZingNodeType.In:
                    return node;

                case ZingNodeType.AttributedStatement:
                    return this.VisitAttributedStatement((AttributedStatement)node);

                default:
                    return base.Visit(node);
            }
        }

        private AttributedStatement VisitAttributedStatement(AttributedStatement attributedStmt)
        {
            attributedStmt.Statement = (Statement)this.Visit(attributedStmt.Statement);
            return attributedStmt;
        }
    }
}