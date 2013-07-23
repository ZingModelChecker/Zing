using System;
using System.Compiler;

namespace Microsoft.Zing 
{
    internal class Partitioner: System.Compiler.Partitioner 
    {
        public Partitioner()
        {
        }
        public Partitioner(Visitor callingVisitor)
            : base(callingVisitor)
        {
        }
        public override Node Visit(Node node)
        {
            if (node == null) return null;
            switch (((ZingNodeType)node.NodeType))
            {
                case ZingNodeType.Array:
                    return this.VisitArray((ZArray) node);
                case ZingNodeType.Accept:
                    return this.VisitAccept((AcceptStatement) node);
                case ZingNodeType.Assert:
                    return this.VisitAssert((AssertStatement) node);
                case ZingNodeType.Assume:
                    return this.VisitAssume((AssumeStatement) node);
                case ZingNodeType.Async:
                    return this.VisitAsync((AsyncMethodCall) node);
                case ZingNodeType.Atomic:
                    return this.VisitAtomic((AtomicBlock) node);
                case ZingNodeType.AttributedStatement:
                    return this.VisitAttributedStatement((AttributedStatement) node);
                case ZingNodeType.Chan:
                    return this.VisitChan((Chan) node);
                case ZingNodeType.Choose:
                    return this.VisitChoose((UnaryExpression) node);
                case ZingNodeType.Event:
                    return this.VisitEventStatement((EventStatement) node);
                case ZingNodeType.EventPattern:
                    return this.VisitEventPattern((EventPattern) node);
                case ZingNodeType.JoinStatement:
                    return this.VisitJoinStatement((JoinStatement) node);
                case ZingNodeType.InvokePlugin:
                    return this.VisitInvokePlugin((InvokePluginStatement)node);
                case ZingNodeType.Range:
                    return this.VisitRange((Range) node);
                case ZingNodeType.ReceivePattern:
                    return this.VisitReceivePattern((ReceivePattern) node);
                case ZingNodeType.Select:
                    return this.VisitSelect((Select) node);
                case ZingNodeType.Send:
                    return this.VisitSend((SendStatement) node);
                case ZingNodeType.Set:
                    return this.VisitSet((Set) node);
                case ZingNodeType.Self:
                    return this.VisitSelf((SelfExpression)node);
                case ZingNodeType.Trace:
                    return this.VisitTrace((TraceStatement) node);
                case ZingNodeType.TimeoutPattern:
                    return this.VisitTimeoutPattern((TimeoutPattern) node);
                case ZingNodeType.Try:
                    return this.VisitZTry((ZTry) node);
                case ZingNodeType.WaitPattern:
                    return this.VisitWaitPattern((WaitPattern) node);
                case ZingNodeType.With:
                    return this.VisitWith((With) node);

                default:
                    return base.Visit(node);
            }
        }

        private ZArray VisitArray(ZArray array)
        {
            array.domainType = base.VisitTypeReference(array.domainType);
            return (ZArray) base.VisitTypeNode((TypeNode)array);
        }

        private AssertStatement VisitAssert(AssertStatement assert)
        {
            assert.booleanExpr = this.VisitExpression(assert.booleanExpr);
            return assert;
        }

        private AcceptStatement VisitAccept (AcceptStatement accept)
        {
            accept.booleanExpr = this.VisitExpression(accept.booleanExpr);
            return accept;
        }

        private AssumeStatement VisitAssume(AssumeStatement assume)
        {
            assume.booleanExpr = this.VisitExpression(assume.booleanExpr);
            return assume;
        }

        private EventPattern VisitEventPattern(EventPattern ep)
        {
            ep.channelNumber = this.VisitExpression(ep.channelNumber);
            ep.messageType = this.VisitExpression(ep.messageType);
            ep.direction = this.VisitExpression(ep.direction);

            return ep;
        }

        private EventStatement VisitEventStatement(EventStatement Event)
        {
            Event.channelNumber = this.VisitExpression(Event.channelNumber);
            Event.messageType = this.VisitExpression(Event.messageType);
            Event.direction = this.VisitExpression(Event.direction);
            return Event;
        }

        private TraceStatement VisitTrace(TraceStatement trace)
        {
            trace.Operands = this.VisitExpressionList(trace.Operands);
            return trace;
        }

        private InvokePluginStatement VisitInvokePlugin(InvokePluginStatement InvokePlugin)
        {
            InvokePlugin.Operands = this.VisitExpressionList(InvokePlugin.Operands);
            return InvokePlugin;
        }

        private AsyncMethodCall VisitAsync(AsyncMethodCall async)
        {
            return (AsyncMethodCall) this.VisitExpressionStatement((ExpressionStatement) async);
        }

        private AtomicBlock VisitAtomic(AtomicBlock atomic)
        {
            return (AtomicBlock) this.VisitBlock((Block) atomic);
        }

        private AttributedStatement VisitAttributedStatement(AttributedStatement attributedStmt)
        {
            attributedStmt.Attributes = this.VisitAttributeList(attributedStmt.Attributes);
            attributedStmt.Statement = (Statement) this.Visit(attributedStmt.Statement);

            return attributedStmt;
        }

        private Chan VisitChan(Chan chan)
        {
            chan.ChannelType = this.VisitTypeReference(chan.ChannelType);
            return chan;
        }

        private UnaryExpression VisitChoose(UnaryExpression expr)
        {
            return (UnaryExpression) this.VisitUnaryExpression(expr);
        }

        private JoinStatement VisitJoinStatement(JoinStatement joinstmt)
        {
            JoinPatternList newJoinPatternList = new JoinPatternList();

            for (int i=0, n=joinstmt.joinPatternList.Length; i < n ;i++)
                newJoinPatternList.Add((JoinPattern) this.Visit(joinstmt.joinPatternList[i]));

            joinstmt.joinPatternList = newJoinPatternList;
            joinstmt.statement = (Statement) this.Visit(joinstmt.statement);
            joinstmt.attributes = this.VisitAttributeList(joinstmt.attributes);
            return joinstmt;
        }

        private Range VisitRange(Range range)
        {
            range.Min = this.VisitExpression(range.Min);
            range.Max = this.VisitExpression(range.Max);

            return (Range) this.VisitConstrainedType((ConstrainedType) range);
        }

        private ReceivePattern VisitReceivePattern(ReceivePattern rp)
        {
            rp.channel = this.VisitExpression(rp.channel);
            rp.data = this.VisitExpression(rp.data);

            return rp;
        }

        private Select VisitSelect(Select select)
        {
            JoinStatementList newJoinStatementList = new JoinStatementList();

            for (int i=0, n=select.joinStatementList.Length; i < n ;i++)
                newJoinStatementList.Add((JoinStatement) this.VisitJoinStatement(select.joinStatementList[i]));

            select.joinStatementList = newJoinStatementList;

            return select;
        }

        private SendStatement VisitSend(SendStatement send)
        {
            send.channel = this.VisitExpression(send.channel);
            send.data = this.VisitExpression(send.data);

            return send;
        }

        private Set VisitSet(Set @set)
        {
            @set.SetType = this.VisitTypeReference(@set.SetType);
            return @set;
        }

        private TimeoutPattern VisitTimeoutPattern(TimeoutPattern tp)
        {
            return tp;
        }

        private SelfExpression VisitSelf(SelfExpression self)
        {
            return self;
        }

        private WaitPattern VisitWaitPattern(WaitPattern wp)
        {
            wp.expression = this.VisitExpression(wp.expression);
            return wp;
        }

        private ZTry VisitZTry(ZTry Try)
        {
            Try.Body = this.VisitBlock(Try.Body);

            WithList newCatchers = new WithList();

            for (int i=0, n=Try.Catchers.Length; i < n ;i++)
                newCatchers.Add(this.VisitWith(Try.Catchers[i]));

            Try.Catchers = newCatchers;

            return Try;
        }

        private With VisitWith(With with)
        {
            with.Block = this.VisitBlock(with.Block);
            return with;
        }

        public override Statement VisitThrow(Throw Throw)
        {
            if (Throw == null) return null;
            return Throw;
        }

    }
}