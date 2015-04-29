using System.Collections;
using System.Compiler;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Zing
{
    /// <summary>
    /// Walks an IR, mutuating it by replacing identifier nodes with the members/locals they resolve to
    /// </summary>
    internal sealed class Looker : System.Compiler.Looker
    {
        internal Looker(Scope scope, Microsoft.Zing.ErrorHandler errorHandler, TrivialHashtable scopeFor, TypeSystem typeSystem) // LJW: added typeSystem parameter
            : this(scope, errorHandler, scopeFor, typeSystem, null, null, null)
        {
        }

        internal Looker(Scope scope, ErrorHandler errorHandler, TrivialHashtable scopeFor, TypeSystem typeSystem, TrivialHashtable ambiguousTypes,  // LJW: added typeSystem parameter
            TrivialHashtable referencedLabels, Hashtable exceptionNames)
            : base(scope, errorHandler, scopeFor, typeSystem, ambiguousTypes, referencedLabels)
        {
            this.exceptionNames = exceptionNames;
        }

        private void HandleError(Node errorNode, Error error, params string[] messageParameters)
        {
            ErrorNode enode = new ZingErrorNode(error, messageParameters);
            enode.SourceContext = errorNode.SourceContext;
            this.ErrorHandler.Errors.Add(enode);
        }

        public override System.Compiler.Declarer GetDeclarer()
        {
            return new Declarer((Zing.ErrorHandler)this.ErrorHandler);
        }

        private Hashtable exceptionNames;

        private void AddExceptionName(string exceptionName)
        {
            if (exceptionNames != null)
            {
                if (!exceptionNames.Contains(exceptionName))
                    exceptionNames.Add(exceptionName, exceptionName);
            }
        }

        public override Node Visit(Node node)
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

                case ZingNodeType.Event:
                    return this.VisitEventStatement((EventStatement)node);

                case ZingNodeType.EventPattern:
                    return this.VisitEventPattern((EventPattern)node);

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
                    return this.VisitSelf((SelfExpression)node);

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

                case ZingNodeType.Yield:
                    return this.VisitYield((YieldStatement)node);

                default:
                    return base.Visit(node);
            }
        }

        // Override the Looker base to build a list of locals on the current method
        public override Statement VisitVariableDeclaration(VariableDeclaration variableDeclaration)
        {
            if (variableDeclaration == null) return null;
            Statement result = base.VisitVariableDeclaration(variableDeclaration);

            ZMethod zMethod = this.currentMethod as ZMethod;
            Debug.Assert(zMethod != null);

            Field f = variableDeclaration.Field;
            zMethod.LocalVars.Add(f);

            return result;
        }

        #region Handle ambiguous type/expression scenarios

        //
        // For "choose" and "sizeof", the operand may be a type reference or a general
        // expression. If the operand is a single, unqualified identifier, the parser
        // can't distinguish types from expressions and assumes it's looking at a type.
        // Here in the looker, we need to gracefully handle the case where the parser
        // guessed wrong and we're actually looking at an expression. To do this, we
        // override a couple of methods in the base class and reconsider the operand
        // as an expression if it doesn't look like a type.
        //

        private bool ignoreTypeRefErrors;
        private bool encounteredTypeRefError;

        public override Expression VisitUnaryExpression(UnaryExpression unaryExpression)
        {
            if ((unaryExpression.NodeType != NodeType.Sizeof && ((ZingNodeType)unaryExpression.NodeType) != ZingNodeType.Choose)
                || !(unaryExpression.Operand is MemberBinding))
                return base.VisitUnaryExpression(unaryExpression);

            Debug.Assert(unaryExpression.Operand is MemberBinding);
            MemberBinding mb = (MemberBinding)unaryExpression.Operand;
            Debug.Assert(mb.BoundMember is TypeExpression);
            TypeExpression te = (TypeExpression)mb.BoundMember;
            if (te.Expression is Identifier)
            {
                Identifier id = (Identifier)te.Expression;

                this.encounteredTypeRefError = false;

                this.ignoreTypeRefErrors = true;
                unaryExpression.Operand = this.VisitExpression(unaryExpression.Operand);
                this.ignoreTypeRefErrors = false;

                if (encounteredTypeRefError)
                {
                    // If this didn't resolve to a type, then treat it as an expression and let
                    // the resolver deal with it (or report an error).
                    unaryExpression.Operand = this.VisitExpression(id);

                    NameBinding nb = unaryExpression.Operand as NameBinding;
                    if (nb != null && nb.BoundMembers.Count == 0)
                    {
                        this.HandleTypeExpected(nb.Identifier);
                        return null;
                    }
                }
            }

            return unaryExpression;
        }

        public override void HandleTypeExpected(Node offendingNode)
        {
            encounteredTypeRefError = true;

            if (ignoreTypeRefErrors)
                return;

            base.HandleTypeExpected(offendingNode);
        }

        #endregion Handle ambiguous type/expression scenarios

        private ZArray VisitArray(ZArray array)
        {
            array.domainType = this.VisitTypeReference(array.domainType);
            EnumNode en = array.domainType as EnumNode;
            if (en != null)
            {
                array.Sizes = new int[] { en.Members.Count - 1 };
            }

            array.ElementType = this.VisitTypeReference(array.ElementType);
            return (ZArray)base.VisitTypeNode((TypeNode)array);
        }

        private AssertStatement VisitAssert(AssertStatement assert)
        {
            assert.booleanExpr = this.VisitExpression(assert.booleanExpr);
            return assert;
        }

        private AcceptStatement VisitAccept(AcceptStatement accept)
        {
            accept.booleanExpr = this.VisitExpression(accept.booleanExpr);
            return accept;
        }

        private AssumeStatement VisitAssume(AssumeStatement assume)
        {
            assume.booleanExpr = this.VisitExpression(assume.booleanExpr);
            return assume;
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

        private InvokeSchedulerStatement VisitInvokeSched(InvokeSchedulerStatement InvokeSched)
        {
            InvokeSched.Operands = this.VisitExpressionList(InvokeSched.Operands);
            return InvokeSched;
        }

        private AsyncMethodCall VisitAsync(AsyncMethodCall async)
        {
            return (AsyncMethodCall)this.VisitExpressionStatement((ExpressionStatement)async);
        }

        private AtomicBlock VisitAtomic(AtomicBlock atomic)
        {
            return (AtomicBlock)this.VisitBlock((Block)atomic);
        }

        private AttributedStatement VisitAttributedStatement(AttributedStatement attributedStmt)
        {
            attributedStmt.Attributes = this.VisitAttributeList(attributedStmt.Attributes);
            attributedStmt.Statement = (Statement)this.Visit(attributedStmt.Statement);

            return attributedStmt;
        }

        private Chan VisitChan(Chan chan)
        {
            TypeNode tNode = this.VisitTypeExpression((TypeExpression)chan.ChannelType);
            chan.ChannelType = tNode;
            return chan;
        }

        private UnaryExpression VisitChoose(UnaryExpression expr)
        {
            return (UnaryExpression)this.VisitUnaryExpression(expr);
        }

        private EventPattern VisitEventPattern(EventPattern ep)
        {
            ep.channelNumber = this.VisitExpression(ep.channelNumber);
            ep.messageType = this.VisitExpression(ep.messageType);
            ep.direction = this.VisitExpression(ep.direction);

            return ep;
        }

        private BinaryExpression VisitIn(BinaryExpression expr)
        {
            return (BinaryExpression)base.VisitBinaryExpression(expr);
        }

        private JoinStatement VisitJoinStatement(JoinStatement joinstmt)
        {
            JoinPatternList newJoinPatternList = new JoinPatternList();

            for (int i = 0, n = joinstmt.joinPatternList.Length; i < n; i++)
                newJoinPatternList.Add((JoinPattern)this.Visit(joinstmt.joinPatternList[i]));

            joinstmt.joinPatternList = newJoinPatternList;
            joinstmt.statement = (Statement)this.Visit(joinstmt.statement);
            joinstmt.attributes = this.VisitAttributeList(joinstmt.attributes);
            return joinstmt;
        }

        private Range VisitRange(Range range)
        {
            range.Min = this.VisitExpression(range.Min);
            range.Max = this.VisitExpression(range.Max);

            return (Range)this.VisitConstrainedType((ConstrainedType)range);
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

            for (int i = 0, n = select.joinStatementList.Length; i < n; i++)
            {
                JoinStatement js = this.VisitJoinStatement(select.joinStatementList[i]);
                js.visible = select.visible;
                newJoinStatementList.Add(js);
            }

            select.joinStatementList = newJoinStatementList;

            return select;
        }

        private SendStatement VisitSend(SendStatement send)
        {
            send.channel = this.VisitExpression(send.channel);
            send.data = this.VisitExpression(send.data);

            return send;
        }

        private YieldStatement VisitYield(YieldStatement yield)
        {
            return yield;
        }

        private SelfExpression VisitSelf(SelfExpression self)
        {
            return self;
        }

        private Set VisitSet(Set @set)
        {
            @set.SetType = this.VisitTypeExpression((TypeExpression)@set.SetType);
            return @set;
        }

        [SuppressMessage("Microsoft.Performance", "CA1801:AvoidUnusedParameters")]
        private TimeoutPattern VisitTimeoutPattern(TimeoutPattern tp)
        {
            return tp;
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

            for (int i = 0, n = Try.Catchers.Length; i < n; i++)
                newCatchers.Add(this.VisitWith(Try.Catchers[i]));

            Try.Catchers = newCatchers;

            return Try;
        }

        private With VisitWith(With with)
        {
            if (with.Name != null)
                AddExceptionName(with.Name.Name);
            with.Block = this.VisitBlock(with.Block);
            return with;
        }

        public override Statement VisitThrow(Throw Throw)
        {
            if (Throw == null) return null;

            if (!(Throw.Expression is Identifier))
            {
                this.HandleError(Throw.Expression, Error.InvalidExceptionExpression);
                return null;
            }

            AddExceptionName(((Identifier)Throw.Expression).Name);
            return Throw;
        }

        public override Field VisitField(Field field)
        {
            Field result = base.VisitField(field);
            return result;
        }

        public override Expression VisitParameter(Parameter parameter)
        {
            Parameter result = (Parameter)base.VisitParameter(parameter);
            Reference pRefType = result.Type as Reference;
            return result;
        }

        public override Method VisitMethod(Method method)
        {
            method = base.VisitMethod(method);
            return method;
        }
    }
}