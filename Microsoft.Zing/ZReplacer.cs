#if !ReducedFootprint

using System;
using System.Compiler;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Zing
{
    /// <summary>
    /// Summary description for ZReplacer.
    /// </summary>
    [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly")]
    public class ZReplacer : Replacer
    {
        private ZReplacer(Identifier oldName, Node newNode)
            : base(oldName, newNode)
        {
        }

        /// <summary>
        /// Within "node", replace occurrences of identifiers matching oldName (by
        /// value) with the given newNode. newNode must be an expression.
        /// </summary>
        /// <param name="node"></param>
        /// <param name="oldName"></param>
        /// <param name="newNode"></param>
        public static void ZReplace(Node node, Identifier oldName, Node newNode)
        {
            if (!(newNode is Expression))
                throw new ArgumentException("ZReplace: newNode must be an Expression");

            ZReplacer replacer = new ZReplacer(oldName, newNode);
            replacer.Visit(node);
        }

        /// <summary>
        /// Within "node", replace identifiers matching the value of oldName with
        /// the given newNode (of type Expression).
        /// </summary>
        /// <param name="node"></param>
        /// <param name="oldName"></param>
        /// <param name="newNode"></param>
        public static void ZReplace(Node node, string oldName, Node newNode)
        {
            ZReplace(node, Identifier.For(oldName), newNode);
        }

        /// <summary>
        /// Within "node", find a labeled statement whose label matches the given
        /// string, and replace it with the supplied statement block.
        /// </summary>
        /// <param name="node"></param>
        /// <param name="labelName"></param>
        /// <param name="block"></param>
        public static void ZReplace(Node node, string labelName, Block block)
        {
            Replacer replacer = new ZReplacer(Identifier.For(labelName), block);
            replacer.Visit(node);
        }

        public override Node VisitUnknownNodeType(Node node)
        {
            if (node == null) return null;
            switch (((ZingNodeType)node.NodeType))
            {
                case ZingNodeType.Array:
                    return this.VisitArray((ZArray)node);

                case ZingNodeType.Accept:
                    return this.VisitAccept((AcceptStatement)node);

                case ZingNodeType.Assert:
                    return this.VisitAssert((AssertStatement)node);

                case ZingNodeType.Assume:
                    return this.VisitAssume((AssumeStatement)node);

                case ZingNodeType.Async:
                    return this.VisitAsync((AsyncMethodCall)node);

                case ZingNodeType.Atomic:
                    return this.VisitAtomic((AtomicBlock)node);

                case ZingNodeType.AttributedStatement:
                    return this.VisitAttributedStatement((AttributedStatement)node);

                case ZingNodeType.Chan:
                    return this.VisitChan((Chan)node);

                case ZingNodeType.Choose:
                    return this.VisitChoose((UnaryExpression)node);

                case ZingNodeType.EventPattern:
                    return this.VisitEventPattern((EventPattern)node);

                case ZingNodeType.Event:
                    return this.VisitEventStatement((EventStatement)node);

                case ZingNodeType.In:
                    return this.VisitIn((BinaryExpression)node);

                case ZingNodeType.JoinStatement:
                    return this.VisitJoinStatement((JoinStatement)node);

                case ZingNodeType.InvokePlugin:
                    return this.VisitInvokePlugin((InvokePluginStatement)node);

                case ZingNodeType.InvokeSched:
                    return this.VisitInvokeSched((InvokeSchedulerStatement)node);

                case ZingNodeType.Range:
                    return this.VisitRange((Range)node);

                case ZingNodeType.ReceivePattern:
                    return this.VisitReceivePattern((ReceivePattern)node);

                case ZingNodeType.Select:
                    return this.VisitSelect((Select)node);

                case ZingNodeType.Send:
                    return this.VisitSend((SendStatement)node);

                case ZingNodeType.Set:
                    return this.VisitSet((Set)node);

                case ZingNodeType.Self:
                    return VisitSelf((SelfExpression)node);

                case ZingNodeType.Trace:
                    return this.VisitTrace((TraceStatement)node);

                case ZingNodeType.TimeoutPattern:
                    return this.VisitTimeoutPattern((TimeoutPattern)node);

                case ZingNodeType.Try:
                    return this.VisitZTry((ZTry)node);

                case ZingNodeType.WaitPattern:
                    return this.VisitWaitPattern((WaitPattern)node);

                case ZingNodeType.With:
                    return this.VisitWith((With)node);

                default:
                    return base.Visit(node);
            }
        }

        public override Statement VisitGoto(Goto Goto)
        {
            if (Goto == null) return null;
            Goto.TargetLabel = (Identifier)this.VisitIdentifier(Goto.TargetLabel);
            return Goto;
        }

        private ZArray VisitArray(ZArray array)
        {
            if (array == null) return null;
            ZArray result = (ZArray)base.VisitTypeNode((ArrayType)array);
            return result;
        }

        private AssertStatement VisitAssert(AssertStatement assert)
        {
            if (assert == null) return null;
            assert.booleanExpr = this.VisitExpression(assert.booleanExpr);
            return assert;
        }

        private AcceptStatement VisitAccept(AcceptStatement accept)
        {
            if (accept == null) return null;
            accept.booleanExpr = this.VisitExpression(accept.booleanExpr);
            return accept;
        }

        private AssumeStatement VisitAssume(AssumeStatement assume)
        {
            if (assume == null) return null;
            assume.booleanExpr = this.VisitExpression(assume.booleanExpr);
            return assume;
        }

        private EventPattern VisitEventPattern(EventPattern ep)
        {
            if (ep == null) return null;
            ep.channelNumber = this.VisitExpression(ep.channelNumber);
            ep.messageType = this.VisitExpression(ep.messageType);
            ep.direction = this.VisitExpression(ep.direction);
            return ep;
        }

        private EventStatement VisitEventStatement(EventStatement Event)
        {
            if (Event == null) return null;
            Event.channelNumber = this.VisitExpression(Event.channelNumber);
            Event.messageType = this.VisitExpression(Event.messageType);
            Event.direction = this.VisitExpression(Event.direction);
            return Event;
        }

        private TraceStatement VisitTrace(TraceStatement trace)
        {
            if (trace == null) return null;
            trace.Operands = this.VisitExpressionList(trace.Operands);
            return trace;
        }

        private InvokePluginStatement VisitInvokePlugin(InvokePluginStatement InvokePlugin)
        {
            if (InvokePlugin == null) return null;
            InvokePlugin.Operands = this.VisitExpressionList(InvokePlugin.Operands);
            return InvokePlugin;
        }

        private InvokeSchedulerStatement VisitInvokeSched(InvokeSchedulerStatement InvokeSched)
        {
            if (InvokeSched == null) return null;
            InvokeSched.Operands = this.VisitExpressionList(InvokeSched.Operands);
            return InvokeSched;
        }

        private AsyncMethodCall VisitAsync(AsyncMethodCall async)
        {
            if (async == null) return null;
            AsyncMethodCall result = (AsyncMethodCall)base.VisitExpressionStatement(async);
            return result;
        }

        private AtomicBlock VisitAtomic(AtomicBlock atomic)
        {
            if (atomic == null) return null;
            AtomicBlock result = (AtomicBlock)this.VisitBlock(atomic);
            return result;
        }

        private AttributedStatement VisitAttributedStatement(AttributedStatement attributedStmt)
        {
            attributedStmt.Attributes = this.VisitAttributeList(attributedStmt.Attributes);
            attributedStmt.Statement = (Statement)this.Visit(attributedStmt.Statement);

            return attributedStmt;
        }

        public override AttributeList VisitAttributeList(AttributeList attributes)
        {
            AttributeList list = new AttributeList();
            for (int i = 0; i < attributes.Count; i++)
            {
                AttributeNode a = attributes[i];
                list.Add(VisitAttributeNode(a));
            }
            return list;
        }

        private Chan VisitChan(Chan chan)
        {
            if (chan == null) return null;
            Chan result = (Chan)this.VisitTypeAlias(chan);
            return result;
        }

        private UnaryExpression VisitChoose(UnaryExpression expr)
        {
            if (expr == null) return null;
            UnaryExpression result = (UnaryExpression)base.VisitUnaryExpression(expr);
            return result;
        }

        private BinaryExpression VisitIn(BinaryExpression expr)
        {
            if (expr == null) return null;
            BinaryExpression result = (BinaryExpression)base.VisitBinaryExpression(expr);
            return result;
        }

        private JoinStatement VisitJoinStatement(JoinStatement joinstmt)
        {
            if (joinstmt == null) return null;

            for (int i = 0, n = joinstmt.joinPatternList.Length; i < n; i++)
                joinstmt.joinPatternList[i] = (JoinPattern)this.Visit(joinstmt.joinPatternList[i]);

            joinstmt.statement = (Statement)this.Visit(joinstmt.statement);
            joinstmt.attributes = this.VisitAttributeList(joinstmt.attributes);
            return joinstmt;
        }

        private Range VisitRange(Range range)
        {
            if (range == null) return null;
            Range result = (Range)this.VisitConstrainedType(range);
            result.Min = this.VisitExpression(range.Min);
            result.Max = this.VisitExpression(range.Max);
            return result;
        }

        private ReceivePattern VisitReceivePattern(ReceivePattern rp)
        {
            if (rp == null) return null;
            rp.channel = this.VisitExpression(rp.channel);
            rp.data = this.VisitExpression(rp.data);
            return rp;
        }

        private Select VisitSelect(Select select)
        {
            if (select == null) return null;

            for (int i = 0, n = select.joinStatementList.Length; i < n; i++)
                select.joinStatementList[i] = (JoinStatement)this.VisitJoinStatement(select.joinStatementList[i]);

            return select;
        }

        private SendStatement VisitSend(SendStatement send)
        {
            if (send == null) return null;
            send.channel = this.VisitExpression(send.channel);
            send.data = this.VisitExpression(send.data);

            return send;
        }

        private Set VisitSet(Set @set)
        {
            if (@set == null) return null;
            Set result = (Set)this.VisitTypeAlias(@set);
            return result;
        }

        private SelfExpression VisitSelf(SelfExpression self)
        {
            return self;
        }

        [SuppressMessage("Microsoft.Performance", "CA1801:AvoidUnusedParameters")]
        private TimeoutPattern VisitTimeoutPattern(TimeoutPattern tp)
        {
            return tp;
        }

        private WaitPattern VisitWaitPattern(WaitPattern wp)
        {
            if (wp == null) return null;
            wp.expression = this.VisitExpression(wp.expression);
            return wp;
        }

        private ZTry VisitZTry(ZTry Try)
        {
            if (Try == null) return null;

            Try.Body = this.VisitBlock(Try.Body);

            for (int i = 0, n = Try.Catchers.Length; i < n; i++)
                Try.Catchers[i] = this.VisitWith(Try.Catchers[i]);

            return Try;
        }

        private With VisitWith(With with)
        {
            if (with == null) return null;
            with.Block = this.VisitBlock(with.Block);
            return with;
        }
    }
}

#endif