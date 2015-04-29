using System.Compiler;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Zing
{
    public class Duplicator : System.Compiler.Duplicator
    {
        /// <param name="module">The module into which the duplicate IR will be grafted.</param>
        /// <param name="type">The type into which the duplicate Member will be grafted. Ignored if entire type, or larger unit is duplicated.</param>
        public Duplicator(Module module, TypeNode type)
            : base(module, type)
        {
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

        public override Method VisitMethod(Method method)
        {
            if (method == null) return null;

            ZMethod result = (ZMethod)base.VisitMethod(method);

            result.ResetLocals();
            return result;
        }

        //
        // BUGBUG: temporary workaround for CCI bug
        //
        public override TypeAlias VisitTypeAlias(TypeAlias tAlias)
        {
            if (tAlias == null) return null;
            TypeAlias dup = (TypeAlias)this.DuplicateFor[tAlias.UniqueKey];
            if (dup != null) return dup;
            this.DuplicateFor[tAlias.UniqueKey] = dup = (TypeAlias)tAlias.Clone();
            dup.Name = tAlias.Name;
            if (tAlias.AliasedType is ConstrainedType)
                //The type alias defines the constrained type, rather than just referencing it
                dup.AliasedType = this.VisitConstrainedType((ConstrainedType)tAlias.AliasedType);
            else
                dup.AliasedType = this.VisitTypeReference(tAlias.AliasedType);
            dup.DeclaringType = this.TargetType;
            dup.DeclaringModule = this.TargetModule;
            dup.ProvideMembers();
            return dup;
        }

        private ZArray VisitArray(ZArray array)
        {
            if (array == null) return null;
            ZArray result = (ZArray)base.VisitTypeNode((ArrayType)array);
            result.LowerBounds = (int[])array.LowerBounds.Clone();
            result.Sizes = (int[])array.Sizes.Clone();
            result.domainType = this.VisitTypeNode(array.domainType);
            return result;
        }

        private AssertStatement VisitAssert(AssertStatement assert)
        {
            if (assert == null) return null;
            AssertStatement result = (AssertStatement)assert.Clone();
            result.booleanExpr = this.VisitExpression(assert.booleanExpr);
            return result;
        }

        private AcceptStatement VisitAccept(AcceptStatement accept)
        {
            if (accept == null) return null;
            AcceptStatement result = (AcceptStatement)accept.Clone();
            result.booleanExpr = this.VisitExpression(accept.booleanExpr);
            return result;
        }

        private AssumeStatement VisitAssume(AssumeStatement assume)
        {
            if (assume == null) return null;
            AssumeStatement result = (AssumeStatement)assume.Clone();
            result.booleanExpr = this.VisitExpression(assume.booleanExpr);
            return result;
        }

        private EventPattern VisitEventPattern(EventPattern ep)
        {
            if (ep == null) return null;
            EventPattern result = (EventPattern)ep.Clone();
            result.channelNumber = this.VisitExpression(ep.channelNumber);
            result.messageType = this.VisitExpression(ep.messageType);
            result.direction = this.VisitExpression(ep.direction);
            return result;
        }

        private EventStatement VisitEventStatement(EventStatement Event)
        {
            if (Event == null) return null;
            EventStatement result = (EventStatement)Event.Clone();
            result.channelNumber = this.VisitExpression(Event.channelNumber);
            result.messageType = this.VisitExpression(Event.messageType);
            result.direction = this.VisitExpression(Event.direction);
            return result;
        }

        private InvokePluginStatement VisitInvokePlugin(InvokePluginStatement InvokePlugin)
        {
            if (InvokePlugin == null) return null;
            InvokePluginStatement result = (InvokePluginStatement)InvokePlugin.Clone();
            result.Operands = this.VisitExpressionList(InvokePlugin.Operands);
            return result;
        }

        private InvokeSchedulerStatement VisitInvokeSched(InvokeSchedulerStatement InvokeSched)
        {
            if (InvokeSched == null) return null;
            InvokeSchedulerStatement result = (InvokeSchedulerStatement)InvokeSched.Clone();
            result.Operands = this.VisitExpressionList(InvokeSched.Operands);
            return result;
        }

        private TraceStatement VisitTrace(TraceStatement trace)
        {
            if (trace == null) return null;
            TraceStatement result = (TraceStatement)trace.Clone();
            result.Operands = this.VisitExpressionList(trace.Operands);
            return result;
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
            AttributedStatement result = (AttributedStatement)attributedStmt.Clone();
            result.Attributes = this.VisitAttributeList(attributedStmt.Attributes);
            result.Statement = (Statement)this.Visit(attributedStmt.Statement);

            return result;
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
            JoinStatement result = (JoinStatement)joinstmt.Clone();
            result.joinPatternList = new JoinPatternList();

            for (int i = 0, n = joinstmt.joinPatternList.Length; i < n; i++)
                result.joinPatternList.Add((JoinPattern)this.Visit(joinstmt.joinPatternList[i]));

            result.statement = (Statement)this.Visit(joinstmt.statement);
            result.attributes = this.VisitAttributeList(joinstmt.attributes);
            return result;
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
            ReceivePattern result = (ReceivePattern)rp.Clone();
            result.channel = this.VisitExpression(rp.channel);
            result.data = this.VisitExpression(rp.data);
            return result;
        }

        private Select VisitSelect(Select select)
        {
            if (select == null) return null;
            Select result = (Select)select.Clone();

            result.joinStatementList = new JoinStatementList();

            for (int i = 0, n = select.joinStatementList.Length; i < n; i++)
                result.joinStatementList.Add((JoinStatement)this.VisitJoinStatement(select.joinStatementList[i]));

            return result;
        }

        private SendStatement VisitSend(SendStatement send)
        {
            if (send == null) return null;
            SendStatement result = (SendStatement)send.Clone();
            result.channel = this.VisitExpression(send.channel);
            result.data = this.VisitExpression(send.data);

            return result;
        }

        private Set VisitSet(Set @set)
        {
            if (@set == null) return null;
            Set result = (Set)this.VisitTypeAlias(@set);
            return result;
        }

        [SuppressMessage("Microsoft.Performance", "CA1801:AvoidUnusedParameters")]
        private TimeoutPattern VisitTimeoutPattern(TimeoutPattern tp)
        {
            if (tp == null) return null;
            return (TimeoutPattern)tp.Clone();
        }

        private WaitPattern VisitWaitPattern(WaitPattern wp)
        {
            if (wp == null) return null;
            WaitPattern result = (WaitPattern)wp.Clone();
            result.expression = this.VisitExpression(wp.expression);
            return result;
        }

        private ZTry VisitZTry(ZTry Try)
        {
            if (Try == null) return null;
            ZTry result = (ZTry)Try.Clone();

            result.Body = this.VisitBlock(Try.Body);
            result.Catchers = new WithList();

            for (int i = 0, n = Try.Catchers.Length; i < n; i++)
                result.Catchers.Add(this.VisitWith(Try.Catchers[i]));

            return result;
        }

        private With VisitWith(With with)
        {
            if (with == null) return null;
            With result = (With)with.Clone();
            result.Block = this.VisitBlock(with.Block);
            return result;
        }
    }
}