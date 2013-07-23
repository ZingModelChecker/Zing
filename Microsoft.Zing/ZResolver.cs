using System;
using System.Compiler;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Microsoft.Zing
{
	/// <summary>
	/// Walks an IR, mutuating it by replacing identifier nodes with the members/locals they resolve to
	/// </summary>
	internal sealed class Resolver : System.Compiler.Resolver
	{
		public Resolver(ErrorHandler errorHandler, TypeSystem typeSystem)
			: base(errorHandler, typeSystem)
		{
		}

        private void HandleError(Node errorNode, Error error, params string[] messageParameters)
        {
            ErrorNode enode = new ZingErrorNode(error, messageParameters);
            enode.SourceContext = errorNode.SourceContext;
            this.ErrorHandler.Errors.Add(enode);
        }

        public override Node Visit(Node node)
        {
            if (node == null) return null;
            switch (((ZingNodeType)node.NodeType))
            {
                case ZingNodeType.Array:
                    return this.VisitArray((ZArray) node);
                case ZingNodeType.Accept:
                    return this.VisitAccept((AcceptStatement)node);
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
                case ZingNodeType.In:
                    return this.VisitIn((BinaryExpression) node);
				case ZingNodeType.JoinStatement:
					return this.VisitJoinStatement((JoinStatement) node);
				case ZingNodeType.InvokePlugin:
                    return this.VisitInvokePlugin((InvokePluginStatement)node);
                case ZingNodeType.InvokeSched:
                    return this.VisitInvokeShed((InvokeSchedulerStatement)node);
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
				case ZingNodeType.TimeoutPattern:
					return this.VisitTimeoutPattern((TimeoutPattern) node);
                case ZingNodeType.Trace:
                    return this.VisitTrace((TraceStatement) node);
                case ZingNodeType.Try:
					return this.VisitZTry((ZTry) node);
				case ZingNodeType.WaitPattern:
					return this.VisitWaitPattern((WaitPattern) node);
				case ZingNodeType.With:
					return this.VisitWith((With) node);
                case ZingNodeType.Yield:
                    return this.VisitYield((YieldStatement)node);

				default:
					return base.Visit(node);
			}
		}


        public override Expression VisitUnaryExpression(UnaryExpression unaryExpression)
        {
            Expression result = base.VisitUnaryExpression (unaryExpression);

            // The result of sizeof() is always Int32.
            if (unaryExpression.NodeType == NodeType.Sizeof)
                result.Type = SystemTypes.Int32;

            return result;
        }

        public override TypeNode InferTypeOfBinaryExpression(TypeNode t1, TypeNode t2, BinaryExpression binaryExpression)
        {
            //
            // For addition or subtraction operations involving a set (as either operand)
            // the result will be a set of the same type. Later, in the Checker, we'll
            // verify that the combination of operands is valid. Here, we can quickly infer
            // the type of the result.
            //
            switch (binaryExpression.NodeType)
            {
                case NodeType.Add:
                case NodeType.Sub:
                    if (t1 is Set)
                        return t1;
                    else if (t2 is Set)
                        return t2;
                    break;
                case (NodeType) ZingNodeType.In:
                    return SystemTypes.Boolean;
                default:
                    break;
            }
            return base.InferTypeOfBinaryExpression (t1, t2, binaryExpression);
        }

        private BinaryExpression VisitIn(BinaryExpression expr)
        {
            return (BinaryExpression) base.VisitBinaryExpression(expr);
        }

        public override Statement VisitForEach(ForEach forEach)
        {
            Statement rVal = base.VisitForEach (forEach);
            //
            // Create a temporary variable to hold our enumerator position
            //
            forEach.ScopeForTemporaryVariables.Members.Add(
                new Field(forEach.ScopeForTemporaryVariables, null, FieldFlags.Public,
                    new Identifier("____" + forEach.UniqueKey.ToString(CultureInfo.InvariantCulture)),
                    SystemTypes.Int32, new Literal(0, SystemTypes.Int32)));

            // Add the local variables we've added here to the method's "locals" list.

            Class scope;
            for (scope = forEach.ScopeForTemporaryVariables; scope is BlockScope ;scope = scope.BaseClass)
                ;
            
            MethodScope mScope = scope as MethodScope;
            Debug.Assert(mScope != null);

            for (int i=0, n= forEach.ScopeForTemporaryVariables.Members.Count; i < n ; i++)
            {
                Member m = forEach.ScopeForTemporaryVariables.Members[i];
                ZMethod zMethod = mScope.DeclaringMethod as ZMethod;
                Debug.Assert(zMethod != null);
                zMethod.LocalVars.Add((Field) m);
            }

            return rVal;
        }


		private ZArray VisitArray(ZArray array)
		{
			array.domainType = this.VisitTypeReference(array.domainType);
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

            Literal litBool = assume.booleanExpr as Literal;
            if (litBool != null)
            {
                //
                // Optimize away "assume(true);"
                //
                if (litBool.Type == SystemTypes.Boolean && ((bool) litBool.Value) == true)
                    return null;
            }

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

        private InvokePluginStatement VisitInvokePlugin(InvokePluginStatement InvokePlugin)
        {
            InvokePlugin.Operands = this.VisitExpressionList(InvokePlugin.Operands);
            return InvokePlugin;
        }

        private InvokeSchedulerStatement VisitInvokeShed (InvokeSchedulerStatement InvokeSched)
        {
            InvokeSched.Operands = this.VisitExpressionList(InvokeSched.Operands);
            return InvokeSched;
        }

        private TraceStatement VisitTrace(TraceStatement trace)
        {
            trace.Operands = this.VisitExpressionList(trace.Operands);
            return trace;
        }

        private YieldStatement VisitYield(YieldStatement Yield)
        {
            return Yield;
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
            expr.Operand = this.VisitExpression(expr.Operand);

            if (expr.Operand.Type.FullName == "Boolean")
            {
                expr.Type = SystemTypes.Boolean;
            }
            else if (expr.Operand.Type == SystemTypes.Type)
            {
                Literal typeExpr = expr.Operand as Literal;
                Debug.Assert(typeExpr != null);

                if (typeExpr.Value is EnumNode)
                    expr.Type = (TypeNode) typeExpr.Value;
                else if (typeExpr.Value is Range)
                    expr.Type = SystemTypes.Int32;
                else
                {
                    this.HandleError(expr.Operand, Error.InvalidChoiceType);
                    return null;
                }
            }
            else
            {
                // We have a variable - must be an array or set
                if (expr.Operand.Type is ZArray)
                    expr.Type = ((ZArray) expr.Operand.Type).ElementType;
                else if (expr.Operand.Type is Set)
                    expr.Type = ((Set) expr.Operand.Type).SetType;
                else
                {
                    this.HandleError(expr.Operand, Error.InvalidChoiceExpr);
                    return null;
                }
            }
            return expr;
		}

        private JoinStatement VisitJoinStatement(JoinStatement joinstmt)
        {
            if (joinstmt == null) return null;
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

            int numJoinStatements = 0;

            for (int i=0, n=select.joinStatementList.Length; i < n ;i++)
            {
                if (select.joinStatementList[i] != null)
                    numJoinStatements++;

                if (numJoinStatements <= 64)
                    newJoinStatementList.Add((JoinStatement) this.VisitJoinStatement(select.joinStatementList[i]));
                else
                    this.HandleError(select.joinStatementList[i], Error.TooManyJoinStatements);
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

		private Set VisitSet(Set @set)
		{
			@set.SetType = this.VisitTypeReference(@set.SetType);
			return @set;
		}

        [SuppressMessage("Microsoft.Performance", "CA1801:AvoidUnusedParameters")]
        private TimeoutPattern VisitTimeoutPattern(TimeoutPattern tp)
		{
			return tp;
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

		private WaitPattern VisitWaitPattern(WaitPattern wp)
		{
            wp.expression = this.VisitExpression(wp.expression);
            return wp;
		}

		private With VisitWith(With with)
		{
			if (with == null) return null;

			with.Block = this.VisitBlock(with.Block);
			return with;
		}

        private SelfExpression VisitSelf(SelfExpression self)
        {
            if (self == null)
                return null;

            return self;
        }

        #region Overrides to deal with symbolic types

        public override Expression VisitMemberBinding(MemberBinding memberBinding)
        {
            Expression result = base.VisitMemberBinding (memberBinding);
            return result;            
        }

        public override Expression VisitMethodCall(MethodCall call)
        {
            // This is a workaround for a bug in the CCI resolver. It can't cope with calls to methods
            // in which some parameters were nulled out because of an error. We check for that case here
            // and null out the entire method call. Reported to Herman on 10/23/04.
            NameBinding nbCallee = call.Callee as NameBinding;
            if (nbCallee != null && nbCallee.BoundMembers.Count > 0)
            {
                ZMethod calleeMethod = (ZMethod) nbCallee.BoundMembers[0];
                for (int n=calleeMethod.Parameters.Count, i=0; i < n ;i++)
                {
                    if (calleeMethod.Parameters[i] == null)
                        return null;
                }
            }
            // end workaround

            Expression result = base.VisitMethodCall (call);
            return result;
        }

        public override Expression VisitQualifiedIdentifier(QualifiedIdentifier qualifiedIdentifier)
        {
            Expression result = base.VisitQualifiedIdentifier (qualifiedIdentifier);
            return result;
        }

        public override Expression VisitQualifiedIdentifierCore(QualifiedIdentifier qualifiedIdentifier)
        {
            Expression result = base.VisitQualifiedIdentifierCore (qualifiedIdentifier);
            return result;
        }

        public override Statement VisitAssignmentStatement(AssignmentStatement assignment)
        {
            Statement result = base.VisitAssignmentStatement (assignment);
            return result;
        }

        public override Expression VisitNameBindingCore(NameBinding nameBinding)
        {
            Expression result = base.VisitNameBindingCore(nameBinding);
            return result;
        }

        public override Expression VisitNameBinding(NameBinding nameBinding)
        {
            Expression result = base.VisitNameBinding(nameBinding);
            return result;
        }

        public override Expression VisitBinaryExpression(BinaryExpression binaryExpression)
        {
            Expression result = base.VisitBinaryExpression (binaryExpression);
            return result;
        }

        #endregion
	}
}
