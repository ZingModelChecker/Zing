using System;
using System.Collections;
using System.Compiler;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using SysError = System.Compiler.Error;
using System.ComponentModel;
using System.ComponentModel.Design.Serialization;
using System.IO;

namespace Microsoft.Zing
{
    /// <summary>
    /// Walk IR checking for semantic errors and repairing it so that subsequent walks need not do error checking
    /// </summary>
    internal sealed class Checker : System.Compiler.Checker
    {
        private Hashtable validChooses;
        private Hashtable validMethodCalls;
        private Hashtable validSetOperations;
        private Hashtable validSelfAccess;

        private bool insideAtomic;

        private Stack selectStatementStack = new Stack();

        private Select currentSelectStatement
        {
            get
            {
                if (selectStatementStack.Count > 0)
                    return (Select) selectStatementStack.Peek();
                else
                    return null;
            }
        }

        private void PushSelectStatement(Select select)
        {
            selectStatementStack.Push(select);
        }

        private void PopSelectStatement()
        {
            selectStatementStack.Pop();
        }

        internal Checker(ErrorHandler errorHandler, TypeSystem typeSystem, TrivialHashtable scopeFor, // LJW: added scopeFor
            TrivialHashtable ambiguousTypes, TrivialHashtable referencedLabels)
            : base(errorHandler, typeSystem, scopeFor, ambiguousTypes, referencedLabels)
        {
            this.validChooses = new Hashtable();
            this.validMethodCalls = new Hashtable();
            this.validSetOperations = new Hashtable();
            this.validSelfAccess = new Hashtable();
        }

        private void HandleError(Node errorNode, Error error, params string[] messageParameters)
        {
            ErrorNode enode = new ZingErrorNode(error, messageParameters);
            enode.SourceContext = errorNode.SourceContext;
            this.ErrorHandler.Errors.Add(enode);
        }

        public override void SerializeContracts(TypeNode type)
        {
            // workaround for bug in the base class. Since we don't need contracts we can just
            // do nothing here.
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
                case ZingNodeType.EventPattern:
                    return this.VisitEventPattern((EventPattern) node);
                case ZingNodeType.Event:
                    return this.VisitEventStatement((EventStatement) node);
                case ZingNodeType.In:
                    return this.VisitBinaryExpression((BinaryExpression) node);
                case ZingNodeType.JoinStatement:
                    return this.VisitJoinStatement((JoinStatement) node);
                case ZingNodeType.InvokePlugin:
                    return this.VisitInvokePlugin((InvokePluginStatement)node);
                case ZingNodeType.InvokeSched:
                    return this.VisitInvokeSched((InvokeSchedulerStatement)node);
                case ZingNodeType.Range:
                    return this.VisitRange((Range) node);
                case ZingNodeType.ReceivePattern:
                    return this.VisitReceivePattern((ReceivePattern) node);
                case ZingNodeType.Select:
                    return this.VisitSelect((Select) node);
                case ZingNodeType.Send:
                    return this.VisitSend((SendStatement) node);
                case ZingNodeType.Self:
                    return this.VisitSelf((SelfExpression)node);
                case ZingNodeType.Set:
                    return this.VisitSet((Set) node);
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
                    return this.VisitYield((YieldStatement) node);

                default:
                    return base.Visit(node);
            }
        }

        //
        // We support iteration over arrays and sets
        //
        public override Statement VisitForEach(ForEach forEach)
        {
            if (forEach == null) return null;
            forEach.TargetVariableType = this.VisitTypeReference(forEach.TargetVariableType);
            forEach.TargetVariable = this.VisitTargetExpression(forEach.TargetVariable);
            forEach.SourceEnumerable = this.VisitExpression(forEach.SourceEnumerable);
            if (forEach.TargetVariableType == null || forEach.TargetVariable == null || forEach.SourceEnumerable == null)
                return null;

            TypeNode collectionType = forEach.SourceEnumerable.Type;
            Set setCollection = collectionType as Set;
            ZArray arrayCollection = collectionType as ZArray;

            TypeNode memberType = null;
            if (setCollection != null)
                memberType = setCollection.SetType;
            if (arrayCollection != null)
                memberType = arrayCollection.ElementType;

            if (memberType == null)
            {
                this.HandleError(forEach.SourceEnumerable, Error.InvalidForeachSource);
                return null;
            }

            if (memberType != forEach.TargetVariableType)
            {
                this.HandleError(forEach.TargetVariable, Error.InvalidForeachTargetType);
                return null;
            }

            forEach.Body = this.VisitBlock(forEach.Body);
            return forEach;
        }

        //
        // Our notion of "sizeof" is more flexible - the operand can be either a type or
        // an expression, so we don't want to use the base checker in that case.
        //
        public override Expression VisitUnaryExpression(UnaryExpression unaryExpression)
        {
            if (unaryExpression.NodeType != NodeType.Sizeof)
                return base.VisitUnaryExpression (unaryExpression);

            unaryExpression.Operand = (Expression) this.Visit(unaryExpression.Operand);

            if (unaryExpression.Operand == null)
                return null;

            if (unaryExpression.Operand is Literal)
            {
                // If the operand is a literal, it must be an array type reference
                Literal operand = (Literal) unaryExpression.Operand;

                if (!(operand.Value is ZArray))
                {
                    this.HandleError(unaryExpression.Operand, Error.InvalidSizeofOperand);
                    return null;
                }
            }
            else
            {
                // If the operand is an expression, it must refer to an array, set, or channel.
                TypeNode opndType = unaryExpression.Operand.Type;

                if (!(opndType is ZArray) && !(opndType is Set) && !(opndType is Chan))
                {
                    this.HandleError(unaryExpression.Operand, Error.InvalidSizeofOperand);
                    return null;
                }
            }

            return unaryExpression;
        }

        public override Method VisitMethod(Method method)
        {
            ZMethod zMethod = base.VisitMethod(method) as ZMethod;

            if (zMethod == null)
                return null;

            if (zMethod.Activated)
            {
                if (!zMethod.IsStatic)
                {
                    this.HandleError(method, Error.ExpectedStaticMethod);
                    return null;
                }

                if (zMethod.ReturnType != SystemTypes.Void)
                {
                    this.HandleError(zMethod, Error.ExpectedVoidMethod);
                    return null;
                }
                if (zMethod.Parameters.Count != 0)
                {
                    this.HandleError(zMethod, Error.ExpectedParameterlessMethod);
                    return null;
                }
            }
            return zMethod;
        }

        //
        // For sets, we need to permit overloading of "+" and "-" but check for invalid combinations
        // of set and element types.
        //
        [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        public override Expression VisitBinaryExpression(BinaryExpression binaryExpression)
        {
            if (binaryExpression == null) return null;

            Expression opnd1 = binaryExpression.Operand1 = this.VisitExpression(binaryExpression.Operand1);
            Expression opnd2 = binaryExpression.Operand2 = this.VisitExpression(binaryExpression.Operand2);

            if (opnd1 == null || opnd2 == null)
                return null;

            
            Set opnd1TypeAsSet = opnd1.Type as Set;
            Set opnd2TypeAsSet = opnd2.Type as Set;

            Literal lit1 = opnd1 as Literal;
            Literal lit2 = opnd2 as Literal;

            if ((opnd1TypeAsSet != null || opnd2TypeAsSet != null) &&
                !this.validSetOperations.Contains(binaryExpression) &&
                binaryExpression.NodeType != (NodeType) ZingNodeType.In)
            {
                this.HandleError(binaryExpression, Error.InvalidSetExpression);
                return null;
            }

            switch (binaryExpression.NodeType)
            {
                case NodeType.Add:
                case NodeType.Sub:
                    if (opnd1TypeAsSet != null)
                    {
                        if (opnd1TypeAsSet.SetType != opnd2.Type && opnd1.Type != opnd2.Type)
                        {
                            if (opnd2TypeAsSet != null)
                                this.HandleError(opnd1, Error.IncompatibleSetTypes);
                            else
                                this.HandleError(opnd2, Error.IncompatibleSetOperand);
                            return null;
                        }
                        return binaryExpression;
                    }
                    break;

                case NodeType.LogicalAnd:
                    {
                        if (lit1 != null && lit1.Value is bool)
                        {
                            if (((bool) lit1.Value) == true)
                                return opnd2;
                            else
                                return opnd1;
                        }

                        if (lit2 != null && lit2.Value is bool)
                        {
                            if (((bool) lit2.Value) == true)
                                return opnd1;
                            else
                                return opnd2;
                        }
                    }

                    break;

                case NodeType.LogicalOr:
                    {
                        if (lit1 != null && lit1.Value is bool)
                        {
                            if (((bool) lit1.Value) == false)
                                return opnd2;
                            else
                                return opnd1;
                        }

                        if (lit2 != null && lit2.Value is bool)
                        {
                            if (((bool) lit2.Value) == false)
                                return opnd1;
                            else
                                return opnd2;
                        }
                    }

                    break;

                case (NodeType) ZingNodeType.In:
                    if (opnd2TypeAsSet == null)
                    {
                        this.HandleError(opnd2, Error.ExpectedSetType);
                        return null;
                    }

                    if (opnd2TypeAsSet.SetType != opnd1.Type)
                    {
                        this.HandleError(opnd1, Error.IncompatibleSetOperand);
                        return null;
                    }
                    return binaryExpression;

                default:
                    break;
            }
            return base.CoerceBinaryExpressionOperands(binaryExpression, opnd1, opnd2);
        }

        public override Statement VisitThrow(Throw Throw)
        {
            // The base Checker will complain that we haven't resolved Throw.Expression
            // but for us that's ok because exceptions are just names.
            return Throw;
        }

        public override Statement VisitExpressionStatement(ExpressionStatement statement)
        {
            // Check for statements that require special handling
            AssignmentExpression assignmentExpr = statement.Expression as AssignmentExpression;
            AssignmentStatement assignmentStatement = null;
            MethodCall methodCall = statement.Expression as MethodCall;
            SelfExpression selfAccess = statement.Expression as SelfExpression;

            UnaryExpression choose = null;

            if (assignmentExpr != null)
            {
                assignmentStatement = assignmentExpr.AssignmentStatement as AssignmentStatement;
                if (assignmentStatement != null && assignmentStatement.Source is MethodCall)
                    methodCall = (MethodCall) assignmentStatement.Source;

                if (assignmentStatement != null && assignmentStatement.Source is UnaryExpression &&
                    assignmentStatement.Source.NodeType == (NodeType) ZingNodeType.Choose)
                    choose = (UnaryExpression) assignmentStatement.Source;

                if (assignmentStatement != null && assignmentStatement.Source is BinaryExpression &&
                    assignmentStatement.Target.Type is Set)
                {
                    BinaryExpression binaryExpression = (BinaryExpression) assignmentStatement.Source;
                    this.validSetOperations.Add(binaryExpression, null);

                    if (SameVariable(assignmentStatement.Target,binaryExpression.Operand1))
                    {
                        // all is well
                    }
                    else if (SameVariable(assignmentStatement.Target, binaryExpression.Operand2))
                    {
                        // swap operands to put the statement in its desired form
                        Expression tmp = binaryExpression.Operand1;
                        binaryExpression.Operand1 = binaryExpression.Operand2;
                        binaryExpression.Operand2 = tmp;
                    }
                    else
                    {
                        this.HandleError(statement, Error.InvalidSetAssignment);
                        return null;
                    }
                }

                //
                // If the source and target types aren't equal, but both are numeric, then we
                // insert an implied cast to the target type. If & when we add an explicit cast
                // operator to Zing, then this can be removed.
                //
                if (assignmentStatement != null &&
                    assignmentStatement.Source != null && assignmentStatement.Source.Type != null &&
                    assignmentStatement.Target != null && assignmentStatement.Target.Type != null &&
                    assignmentStatement.Source.Type != assignmentStatement.Target.Type &&
                    assignmentStatement.Source.Type.IsPrimitiveNumeric &&
                    assignmentStatement.Target.Type.IsPrimitiveNumeric)
                {
                    // Wrap a cast operator around the source expression
                    BinaryExpression binExpr = new BinaryExpression(assignmentStatement.Source,
                        new MemberBinding(null, assignmentStatement.Target.Type),
                        NodeType.Castclass, assignmentStatement.Source.SourceContext);

                    binExpr.Type = assignmentStatement.Target.Type;
                    assignmentStatement.Source = binExpr;
                }
            }

            if (methodCall != null)
                this.validMethodCalls.Add(methodCall, null);

            if (selfAccess != null)
                this.validSelfAccess.Add(selfAccess, null);

            if (choose != null)
                this.validChooses.Add(choose, null);

            return base.VisitExpressionStatement(statement);
        }

        private static bool SameVariable(Expression expr1, Expression expr2)
        {
            MemberBinding mb1 = expr1 as MemberBinding;
            MemberBinding mb2 = expr2 as MemberBinding;

            if (mb1 == null || mb2 == null)
                return false;

            if (mb1.BoundMember.DeclaringType != mb2.BoundMember.DeclaringType)
                return false;

            if (mb1.BoundMember.Name != mb2.BoundMember.Name)
                return false;

            return true;
        }

        public override Expression VisitMethodCall(MethodCall call)
        {
            if (this.inWaitPattern)
            {
                MemberBinding mbCallee = call.Callee as MemberBinding;
                ZMethod calleeMethod = null;
                if (mbCallee != null)
                    calleeMethod = mbCallee.BoundMember as ZMethod;

                if (mbCallee == null || calleeMethod == null)
                    return base.VisitMethodCall(call);

                // Method must return bool (concrete) and have only input params
                if (calleeMethod.ReturnType != SystemTypes.Boolean)
                {
                    this.HandleError(call, Error.InvalidPredicateReturnType);
                    return null;
                }

                for (int i=0, n=calleeMethod.Parameters.Count; i < n ;i++)
                {
                    if (calleeMethod.Parameters[i].IsOut)
                    {
                        this.HandleError(call, Error.UnexpectedPredicateOutputParameter);
                        return null;
                    }
                }
            }
            else if (!this.validMethodCalls.Contains(call))
            {
                this.HandleError(call, Error.EmbeddedMethodCall);
                return null;
            }
            return base.VisitMethodCall(call);
        }

        private UnaryExpression VisitChoose(UnaryExpression expr)
        {
            if (expr == null) return null;

            if (!this.validChooses.Contains(expr))
            {
                this.HandleError(expr, Error.EmbeddedChoose);
                return null;
            }
            return expr;
        }

        //
        // We override VisitTypeReference because the base class will (incorrectly) hit an
        // assertion failure on ArrayTypeExpression nodes.
        //
        public override TypeNode VisitTypeReference(TypeNode type)
        {
            if (type == null) return null;
            if (type.NodeType == NodeType.ArrayTypeExpression) return type;
            return base.VisitTypeReference(type);
        }

        private ZArray VisitArray(ZArray array)
        {
            if (array == null) return null;
            array.domainType = this.VisitTypeReference(array.domainType);
            return (ZArray) base.VisitTypeReference((TypeNode)array);
        }

        private AssertStatement VisitAssert(AssertStatement assert)
        {
            if (assert == null) return null;
            assert.booleanExpr = this.VisitExpression(assert.booleanExpr);

            if (assert.booleanExpr == null)
                return null;

            if (assert.booleanExpr.Type != SystemTypes.Boolean)
            {
                this.HandleError(assert, Error.BooleanExpressionRequired);
                return null;
            }
            return assert;
        }

        private AcceptStatement VisitAccept (AcceptStatement accept)
        {
            if (accept == null) return null;
            accept.booleanExpr = this.VisitExpression(accept.booleanExpr);

            if (accept.booleanExpr == null)
                return null;

            if (accept.booleanExpr.Type != SystemTypes.Boolean)
            {
                this.HandleError(accept, Error.BooleanExpressionRequired);
                return null;
            }
            return accept;
        }

        private EventPattern VisitEventPattern(EventPattern ep)
        {
            if (ep == null) return null;

            if (!currentSelectStatement.visible)
            {
                this.HandleError(ep, Error.InvalidEventPattern);
                return null;
            }

            ep.direction = this.VisitExpression(ep.direction);

            if (ep.direction != null && ep.direction.Type != SystemTypes.Boolean)
            {
                this.HandleError(ep.direction, Error.BooleanExpressionRequired);
                return null;
            }

            ep.channelNumber = this.VisitExpression(ep.channelNumber);

            if (ep.channelNumber != null)
            {
                if (ep.channelNumber.Type == SystemTypes.UInt8)
                {
                    ep.channelNumber = new BinaryExpression(ep.channelNumber, new MemberBinding(null, SystemTypes.Int32),
                        NodeType.Castclass, ep.channelNumber.SourceContext);
                    ep.channelNumber.Type = SystemTypes.Int32;
                }
                else if (ep.channelNumber.Type != SystemTypes.Int32)
                {
                    this.HandleError(ep.channelNumber, Error.IntegerExpressionRequired);
                    return null;
                }
            }
            
            ep.messageType = this.VisitExpression(ep.messageType);

            if (ep.messageType != null)
            {
                if (ep.messageType.Type == SystemTypes.UInt8)
                {
                    ep.messageType = new BinaryExpression(ep.messageType, new MemberBinding(null, SystemTypes.Int32),
                        NodeType.Castclass, ep.messageType.SourceContext);
                    ep.messageType.Type = SystemTypes.Int32;
                }
                else if (ep.messageType.Type != SystemTypes.Int32)
                {
                    this.HandleError(ep.messageType, Error.IntegerExpressionRequired);
                    return null;
                }
            }
            return ep;
        }

        private EventStatement VisitEventStatement(EventStatement Event)
        {
            if (Event == null) return null;
            Event.direction = this.VisitExpression(Event.direction);

            if (Event.direction != null && Event.direction.Type != SystemTypes.Boolean)
            {
                this.HandleError(Event.direction, Error.BooleanExpressionRequired);
                return null;
            }

            Event.channelNumber = this.VisitExpression(Event.channelNumber);
            
            if (Event.channelNumber != null)
            {
                if (Event.channelNumber.Type == SystemTypes.UInt8)
                {
                    Event.channelNumber = new BinaryExpression(Event.channelNumber, new MemberBinding(null, SystemTypes.Int32),
                        NodeType.Castclass, Event.channelNumber.SourceContext);
                    Event.channelNumber.Type = SystemTypes.Int32;
                }
                else if (Event.channelNumber.Type != SystemTypes.Int32)
                {
                    this.HandleError(Event.channelNumber, Error.IntegerExpressionRequired);
                    return null;
                }
            }

            Event.messageType = this.VisitExpression(Event.messageType);
            
            if (Event.messageType != null)
            {
                if (Event.messageType.Type == SystemTypes.UInt8)
                {
                    Event.messageType = new BinaryExpression(Event.messageType, new MemberBinding(null, SystemTypes.Int32),
                        NodeType.Castclass, Event.messageType.SourceContext);
                    Event.messageType.Type = SystemTypes.Int32;
                }
                else if (Event.messageType.Type != SystemTypes.Int32)
                {
                    this.HandleError(Event.messageType, Error.IntegerExpressionRequired);
                    return null;
                }
            }
            return Event;
        }

        private TraceStatement VisitTrace(TraceStatement trace)
        {
            trace.Operands = base.VisitExpressionList(trace.Operands);

            if (trace.Operands == null || trace.Operands.Count == 0)
            {
                this.HandleError(trace, Error.TraceExpectedArguments);
                return null;
            }

            Expression arg0 = trace.Operands[0];
            Literal lit0 = arg0 as Literal;
            if (lit0 == null)
            {
                this.HandleError(trace, Error.ExpectedStringLiteral);
                return null;
            }

            if (!(lit0.Value is string))
            {
                this.HandleError(lit0, Error.ExpectedStringLiteral);
                return null;
            }

            return trace;
        }

        private InvokePluginStatement VisitInvokePlugin(InvokePluginStatement InvokePlugin)
        {
            InvokePlugin.Operands = base.VisitExpressionList(InvokePlugin.Operands);

            if (InvokePlugin.Operands == null || InvokePlugin.Operands.Count == 0)
            {
                this.HandleError(InvokePlugin, Error.InvokePluginExpectedArguments);
                return null;
            }

            Expression arg0 = InvokePlugin.Operands[0];
            Literal lit0 = arg0 as Literal;
            if (lit0 == null)
            {
                this.HandleError(InvokePlugin, Error.ExpectedPluginDllName);
                return null;
            }

            if (!(lit0.Value is string))
            {
                this.HandleError(InvokePlugin, Error.ExpectedPluginDllName);
                return null;
            }

            return InvokePlugin;
        }

        private InvokeSchedulerStatement VisitInvokeSched (InvokeSchedulerStatement InvokeShed)
        {
            InvokeShed.Operands = base.VisitExpressionList(InvokeShed.Operands);

            return InvokeShed;
        }

        private AssumeStatement VisitAssume(AssumeStatement assume)
        {
            if (assume == null) return null;
            assume.booleanExpr = this.VisitExpression(assume.booleanExpr);
            if (assume.booleanExpr != null && assume.booleanExpr.Type != SystemTypes.Boolean)
            {
                this.HandleError(assume, Error.BooleanExpressionRequired);
                return null;
            }
            return assume;
        }

        private SelfExpression VisitSelf (SelfExpression self)
        {
            if(self == null)
            {
                return null;
            }
            return self;
        }

        private YieldStatement VisitYield (YieldStatement yield)
        {
            if (yield == null) return null;
            if (((ZMethod)this.currentMethod).Atomic)
            {
                this.HandleError(yield, Error.IllegalYieldInAtomicBlock);
                return null;
            }
            return yield;
        }

        private AsyncMethodCall VisitAsync(AsyncMethodCall async)
        {
            if (async == null) return null;
            async = (AsyncMethodCall) this.VisitExpressionStatement((ExpressionStatement) async);

            if (async != null && async.Expression is MethodCall)
            {
                ZMethod method = (ZMethod) ((MemberBinding) async.Callee).BoundMember;

                if (method.ReturnType != SystemTypes.Void)
                {
                    this.HandleError(async, Error.InvalidAsyncCallTarget);
                    //return null;  // this is just a warning, for now
                }

                for (int i=0, n = method.Parameters.Count; i < n ;i++)
                {
                    Parameter param = method.Parameters[i];
                    if ((param.Flags & ParameterFlags.Out) != 0)
                    {
                        this.HandleError(async, Error.InvalidAsyncCallTarget);
                        //return null;  // this is just a warning, for now
                    }
                }
            }

            return async;
        }

        private Block VisitAtomic(AtomicBlock atomic)
        {
            Block newAtomic = null;
            if (atomic == null) return null;

            if (((ZMethod) this.currentMethod).Atomic)
            {
                this.HandleError(atomic, Error.AtomicBlockInAtomicMethod);
                return null;
            }

            if (this.insideAtomic)
            {
                this.HandleError(atomic, Error.AtomicBlockNested);
                return null;
            }

            this.insideAtomic = true;
            newAtomic = this.VisitBlock((Block) atomic);
            this.insideAtomic = false;

            return newAtomic;
        }

        private AttributedStatement VisitAttributedStatement(AttributedStatement attributedStmt)
        {
            if (attributedStmt == null) return null;

            attributedStmt.Attributes = this.VisitAttributeList(attributedStmt.Attributes);
            attributedStmt.Statement = (Statement) this.Visit(attributedStmt.Statement);

            return attributedStmt;
        }

        public override AttributeList VisitAttributeList(AttributeList attributes)
        {
            if (attributes == null) return null;

            AttributeList rval = new AttributeList();

            for (int i = 0, n = attributes.Count; i < n; i++)
            {
                rval.Add(this.VisitAttributeNode(attributes[i]));
            }

            return rval;
        }

        public override AttributeNode VisitAttributeNode(AttributeNode attribute)
        {
            if (attribute == null) return null;

            attribute.Constructor = this.VisitExpression(attribute.Constructor);
            attribute.Expressions = this.VisitExpressionList(attribute.Expressions);
            return attribute;
        }

        public override Expression VisitAttributeConstructor(AttributeNode attribute, Node target)
        {
            if (attribute == null) return null;
            MemberBinding mb = attribute.Constructor as MemberBinding;
            if (mb == null) { Debug.Assert(false); return null; }
            InstanceInitializer cons = mb.BoundMember as InstanceInitializer;
            if (cons == null) return null;
            TypeNode t = cons.DeclaringType;
            if (t.IsAssignableTo(SystemTypes.Attribute))
            {
                // NOTE: for Zing, we don't check the attribute target because we're putting
                // attributes on statements, which CCI will never understand.

                //if (!this.CheckAttributeTarget(attribute, target, mb, t)) return null;
                //this.CheckForObsolesence(mb, cons);

                return mb;
            }
            Debug.Assert(false);
            this.HandleError(mb, System.Compiler.Error.NotAnAttribute, this.GetTypeName(t));
            this.HandleRelatedError(t);
            return null;
        }

        private Chan VisitChan(Chan chan)
        {
            if (chan == null) return null;
            chan.ChannelType = this.VisitTypeReference(chan.ChannelType);
            return chan;
        }

        private Range VisitRange(Range range)
        {
            if (range == null) return null;

            range.Min = this.VisitExpression(range.Min);
            range.Max = this.VisitExpression(range.Max);

            return (Range) this.VisitConstrainedType((ConstrainedType) range);
        }

        private JoinStatement VisitJoinStatement(JoinStatement joinstmt)
        {
            if (joinstmt == null) return null;

            JoinPatternList newJoinPatternList = new JoinPatternList();

            for (int i=0, n=joinstmt.joinPatternList.Length; i < n ;i++)
            {
                if (joinstmt.joinPatternList[i] is TimeoutPattern && n != 1)
                {
                    // If we've already see a timeout in this join statement, then
                    // skip any subsequent ones and report an error
                    HandleError(joinstmt.joinPatternList[i], Error.TimeoutNotAlone);
                    return null;
                }

                JoinPattern newJoinPattern = (JoinPattern) this.Visit(joinstmt.joinPatternList[i]);

                if (newJoinPattern != null)
                    newJoinPatternList.Add(newJoinPattern);
            }

            joinstmt.joinPatternList = newJoinPatternList;
            joinstmt.statement = (Statement) this.Visit(joinstmt.statement);
            joinstmt.attributes = this.VisitAttributeList(joinstmt.attributes);

            if (joinstmt.joinPatternList.Length == 0 || joinstmt.statement == null)
                return null;

            return joinstmt;
        }

        private ReceivePattern VisitReceivePattern(ReceivePattern rp)
        {
            if (rp == null) return null;

            rp.channel = this.VisitExpression(rp.channel);
            rp.data = this.VisitExpression(rp.data);

            if (rp.channel == null || rp.data == null)
                return null;

            Chan chanType = rp.channel.Type as Chan;

            if (chanType == null)
            {
                // The channel argument must refer to a channel type
                this.HandleError(rp.channel, Error.ExpectedChannelType);
                return null;
            }

            if (chanType.ChannelType != rp.data.Type)
            {
                // the data argument must match the message type of the channel
                this.HandleError(rp.data, Error.InvalidMessageType);
                return null;
            }

            return rp;
        }

        [SuppressMessage("Microsoft.Performance", "CA1801:AvoidUnusedParameters")]
        private TimeoutPattern VisitTimeoutPattern(TimeoutPattern tp)
        {
            return tp;
        }

        private bool inWaitPattern;

        private WaitPattern VisitWaitPattern(WaitPattern wp)
        {
            if (wp == null) return null;
            inWaitPattern = true;
            wp.expression = this.VisitExpression(wp.expression);
            inWaitPattern = false;
            return wp;
        }

        private Select VisitSelect(Select select)
        {
            if (select == null) return null;

            PushSelectStatement(select);

            JoinStatementList newJoinStatementList = new JoinStatementList();

            int timeoutIndex = -1;

            for (int i=0, n=select.joinStatementList.Length; i < n ;i++)
            {
                if (select.joinStatementList[i].joinPatternList[0] is TimeoutPattern)
                {
                    if (timeoutIndex >= 0)
                    {
                        // If we've already seen a "timeout" join statement, then skip
                        // subsequent ones and report an error.
                        HandleError(select.joinStatementList[i], Error.TooManyTimeouts);
                        continue;
                    }
                    timeoutIndex = i;
                }

                JoinStatement newJoinStatement = (JoinStatement) this.VisitJoinStatement(select.joinStatementList[i]);

                if (newJoinStatement != null)
                    newJoinStatementList.Add(newJoinStatement);
            }

            select.joinStatementList = newJoinStatementList;

            // If a timeout is present and it isn't already last, move it to the
            // end of the list. This will be helpful during code generation.
            if (timeoutIndex >= 0 && timeoutIndex != (select.joinStatementList.Length-1))
            {
                JoinStatement temp;
                temp = select.joinStatementList[select.joinStatementList.Length-1];
                select.joinStatementList[select.joinStatementList.Length-1] = select.joinStatementList[timeoutIndex];
                select.joinStatementList[timeoutIndex] = temp;
            }

            if (select.joinStatementList.Length == 1)
                select.deterministicSelection = true;

            PopSelectStatement();

            if (select.joinStatementList.Length == 0)
                return null;

            return select;
        }

        private SendStatement VisitSend(SendStatement send)
        {
            if (send == null) return null;

            send.channel = this.VisitExpression(send.channel);
            send.data = this.VisitExpression(send.data);

            if (send.channel == null || send.data == null)
                return null;

            Chan chanType = send.channel.Type as Chan;

            if (chanType == null)
            {
                // The channel argument must refer to a channel type
                this.HandleError(send.channel, Error.ExpectedChannelType);
                return null;
            }

            if (chanType.ChannelType != send.data.Type)
            {
                // the data argument must match the message type of the channel
                this.HandleError(send.data, Error.InvalidMessageType);
                return null;
            }

            return send;
        }

        private Set VisitSet(Set @set)
        {
            if (@set == null) return null;
            @set.SetType = this.VisitTypeReference(@set.SetType);
            return @set;
        }

        private ZTry VisitZTry(ZTry Try)
        {
            if (Try == null) return null;

            Try.Body = this.VisitBlock(Try.Body);

            WithList newCatchers = new WithList();

            for (int i=0, n=Try.Catchers.Length; i < n ;i++)
                newCatchers.Add(this.VisitWith(Try.Catchers[i]));

            Try.Catchers = newCatchers;

            return Try;
        }

        private With VisitWith(With with)
        {
            if (with == null) return null;

            with.Block = this.VisitBlock(with.Block);
            return with;
        }

        public override Expression VisitConstruct(Construct cons)
        {
            if (cons == null) return cons;
            MemberBinding mb = cons.Constructor as MemberBinding;
            if (mb == null)
                return null;

            //
            // Verify that mb.BoundMember is a heap-allocated type
            //
            TypeNode tn = cons.Type;
            ZArray arrayNode = tn as ZArray;
            if (tn is Set || arrayNode != null || tn is Class || tn is Chan)
			{
				if (arrayNode != null)
				{
					ExpressionList el = cons.Operands;
					Debug.Assert(el.Count <= 1);
					if (arrayNode.Sizes == null)
					{
						// This is a variable-sized array.  Check that there is exactly 
						// one argument to the constructor of integer type.
						if (el.Count == 0)
						{
							this.HandleError(cons, Error.IntegerExpressionRequired);
							return null;
						}
						Expression e = (Expression) el[0];
						if (e.Type != SystemTypes.Int32)
						{
							this.HandleError(cons, Error.IntegerExpressionRequired);
							return null;
						}
					}
					else 
					{
						// This is a constant-sized array.  Check that there is no
						// argument to the constructor.
						if (el.Count == 1)
						{
							this.HandleError(cons, Error.UnexpectedToken, new string[] { el[0].ToString() });
							return null;
						}
					}
				}
				return cons;
			}
			else
			{
				this.HandleError(cons, Error.ExpectedComplexType);
				return null;
			}

        }
    }
}
