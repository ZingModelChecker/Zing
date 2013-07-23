using System;
using System.Compiler;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Microsoft.Zing
{
    /// <summary>
    /// Walks an IR, replacing it with the C#-style IR for the code we wish to generate for Zing.
    /// </summary>
    internal sealed class Normalizer : System.Compiler.StandardVisitor
    {
        public Normalizer(Splicer splicer, AttributeList attributes, bool secondOfTwo)
        {
            this.splicer = splicer;
            this.attributes = attributes;
            this.secondOfTwo = secondOfTwo;
        }

        public Normalizer(bool secondOfTwo)
            : this(null, null, secondOfTwo)
        {
        }

        //
        // JoinStatement nodes can be traversed in two ways - to generate a "runnable"
        // expression, or to generate the body of their basic block. We separate out
        // the first kind of traversal into the following two methods.
        //
        public Expression GetRunnablePredicate(JoinStatement joinStatement)
        {
            Expression fullExpr = null;

            for (int i=0, n = joinStatement.joinPatternList.Length; i < n ;i++)
            {
                Expression jpExpr = (Expression) this.Visit(joinStatement.joinPatternList[i]);

                if (fullExpr == null)
                    fullExpr = jpExpr;
                else
                    fullExpr = new BinaryExpression(fullExpr, jpExpr, NodeType.LogicalAnd, joinStatement.SourceContext);
            }
            return fullExpr;
        }

        // These visitor methods are called only from GetRunnablePredicate

        private Expression VisitReceivePattern(ReceivePattern rp)
        {
            Expression receivePred = Templates.GetExpressionTemplate("ReceivePatternPredicate");

            Replacer.Replace(receivePred, "_chanType", rp.channel.Type.Name);
            Replacer.Replace(receivePred, "_chanExpr", this.VisitExpression(rp.channel));

            return receivePred;
        }

        private Expression VisitWaitPattern(WaitPattern wp)
        {
            return this.VisitExpression(wp.expression);
        }

        [SuppressMessage("Microsoft.Performance", "CA1801:AvoidUnusedParameters")]
        private Expression VisitTimeoutPattern(TimeoutPattern tp)
        {
            return (Expression) new Literal(true, SystemTypes.Boolean);
        }

        [SuppressMessage("Microsoft.Performance", "CA1801:AvoidUnusedParameters")]
        private Expression VisitEventPattern(EventPattern ep)
        {
            return (Expression) new Literal(true, SystemTypes.Boolean);
        }


        // Are we generating the "second half" of a statement?
        private bool secondOfTwo;
        private Splicer splicer;
        private AttributeList attributes;

        private bool inExpressionStatement;

        private static Identifier refLocals = new Identifier("locals");
        private static Identifier refInputs = new Identifier("inputs");
        private static Identifier refOutputs= new Identifier("outputs");

        public override Node Visit(Node node)
        {
            if (node == null) return null;
            switch (((ZingNodeType)node.NodeType))
            {
                case ZingNodeType.Array:
                case ZingNodeType.Atomic:
                case ZingNodeType.Chan:
                case ZingNodeType.Choose:
                case ZingNodeType.Range:
                case ZingNodeType.Set:
                case ZingNodeType.Try:
                case ZingNodeType.With:
                case ZingNodeType.Select:
                    Debug.Assert(false, "Invalid node type in Microsoft.Zing.Normalizer");
                    return null;
                case ZingNodeType.Accept:
                    return this.VisitAccept((AcceptStatement)node);
                case ZingNodeType.Assert:
                    return this.VisitAssert((AssertStatement) node);
                case ZingNodeType.Assume:
                    return this.VisitAssume((AssumeStatement) node);
                case ZingNodeType.Async:
                    return this.VisitAsync((AsyncMethodCall) node);
                case ZingNodeType.EventPattern:
                    return this.VisitEventPattern((EventPattern) node);
                case ZingNodeType.Event:
                    return this.VisitEventStatement((EventStatement) node);
                case ZingNodeType.In:
                    return this.VisitIn((BinaryExpression) node);
                case ZingNodeType.JoinStatement:
                    return this.VisitJoinStatement((JoinStatement) node);
                case ZingNodeType.InvokePlugin:
                    return this.VisitInvokePlugin((InvokePluginStatement)node);
                case ZingNodeType.InvokeSched:
                    return this.VisitInvokeSched((InvokeSchedulerStatement)node);
                case ZingNodeType.ReceivePattern:
                    return this.VisitReceivePattern((ReceivePattern) node);
                case ZingNodeType.Self:
                    return this.VisitSelf((SelfExpression)node);
                case ZingNodeType.Send:
                    return this.VisitSend((SendStatement) node);
                case ZingNodeType.TimeoutPattern:
                    return this.VisitTimeoutPattern((TimeoutPattern) node);
                case ZingNodeType.Trace:
                    return this.VisitTrace((TraceStatement) node);
                case ZingNodeType.WaitPattern:
                    return this.VisitWaitPattern((WaitPattern) node);
                case ZingNodeType.Yield:
                    return this.VisitYield((YieldStatement)node);
                default:
                    return base.Visit(node);
            }
        }

        //
        // Re-write expressions to reference globals, locals, parameters, and the heap
        // in the appropriate way for our runtime.
        //

        public override Expression VisitBinaryExpression(BinaryExpression binaryExpression)
        {
            if (binaryExpression.NodeType == NodeType.ExplicitCoercion)
            {
                binaryExpression.NodeType = NodeType.Castclass;
                binaryExpression.Operand1 = this.VisitExpression(binaryExpression.Operand1);
                return binaryExpression;
            }
            else if (binaryExpression.NodeType == NodeType.Castclass)
            {
                if (binaryExpression.Operand1 is Literal)
                    return this.VisitExpression(binaryExpression.Operand1);

                // For type-casts, we want to ignore the "type" operand since these were
                // inserted by the checker and needn't be examined here. The form of their
                // member bindings is not understood by the normalizer.
                binaryExpression.Operand1 = this.VisitExpression(binaryExpression.Operand1);

                return binaryExpression;
            }
            else
                return base.VisitBinaryExpression(binaryExpression);
        }

        public override Expression VisitExpression(Expression expression)
        {
            if (expression == null) return null;

            switch (expression.NodeType) {
                case NodeType.AddressDereference:
                    return VisitExpression(((AddressDereference) expression).Address);

                case NodeType.MemberBinding:
                    return VisitMemberBindingExpression((MemberBinding)expression);

                default:
                    return base.VisitExpression(expression);
            }
        }

        [SuppressMessage("Microsoft.Performance", "CA1800:DoNotCastUnnecessarily")]
        private Expression VisitMemberBindingExpression(MemberBinding binding)
        {
            if (binding.BoundMember.NodeType != NodeType.Field)
            {
                Debug.Assert(false, "MemberBinding scenario not yet supported");
                return null;
            }

            if (binding.BoundMember is ParameterField)
            {
                Parameter param = ((ParameterField)binding.BoundMember).Parameter;
                if ((param.Flags & ParameterFlags.Out) != 0)
                    return new QualifiedIdentifier(refOutputs, param.Name);
                else
                    return new QualifiedIdentifier(refInputs, param.Name);
            }

            Field field = (Field)binding.BoundMember;
            Expression targetObject = binding.TargetObject;
            // for output parameters
            while (targetObject is AddressDereference)
                targetObject = ((AddressDereference)targetObject).Address;

            if (field.IsStatic)
            {
                // This is a reference to a global variable.
                Expression globalAccess = Templates.GetExpressionTemplate("GlobalFieldAccess");
                Replacer.Replace(globalAccess, "_fieldName",
                    new Identifier(string.Format(CultureInfo.InvariantCulture,
                        "{0}_{1}", field.DeclaringType.Name.Name, field.Name.Name)));

                return globalAccess;
            }
            else if (field.DeclaringType is BlockScope)
            {
                return new QualifiedIdentifier(refLocals, field.Name);
            }
            else if (targetObject is ImplicitThis || targetObject is This)
            {
                Expression thisExpr = Templates.GetExpressionTemplate("ThisFieldAccess");
                Replacer.Replace(thisExpr, "_objectType", targetObject.Type.Name);
                Replacer.Replace(thisExpr, "_fieldName", field.Name);

                return thisExpr;
            }
            else if (targetObject is MemberBinding || targetObject is Indexer)
            {
                // Need to distinguish between class field and struct field access.
                // Structs are not on the heap, so the pointer deref isn't appropriate.
                if (!field.DeclaringType.IsPrimitive && field.DeclaringType is Struct)
                {
                    // we're gradually transforming struct
                    // fields into properties so far, we've
                    // done it for globals only, locals will
                    // be added next, then heap objects...
                    string propName = field.Name.Name;
                    bool pFlatten = true;
                    Expression prefix = null;

                    // search for pattern var.struct1...structn.member
                    while (true)
                    {
                        Field vField = null;
                        MemberBinding vBinding = targetObject as MemberBinding;

                        if (vBinding != null)
                        {
                            ParameterField pf = vBinding.BoundMember as ParameterField;
                            if (pf != null)
                            {
                                // param.struct1...structn.member
                                Parameter param = pf.Parameter;
                                propName = param.Name.Name + "_" + propName;
                                if ((param.Flags & ParameterFlags.Out) != 0)
                                    prefix = refOutputs;
                                else
                                    prefix = refInputs;
                                break;
                            }
                        }
                        else
                        {
                            pFlatten = false;
                            break;
                        }

                        vField = vBinding.BoundMember as Field;
                        if (vField == null)
                        {
                            pFlatten = false;
                            break;
                        }

                        propName = vField.Name.Name + "_" + propName;

                        if (vField.IsStatic)
                        {
                            // global.struct1...structn.member
                            propName = vField.DeclaringType.Name.Name + "_" + propName;
                            prefix = new QualifiedIdentifier(Identifier.For("application"),
                                Identifier.For("globals"));
                            break;
                        }
                        else if (vField.DeclaringType is BlockScope)
                        {
                            // local.struct1...structn.member
                            prefix = refLocals;
                            break;
                        }

                        if (vField.DeclaringType.IsPrimitive || !(vField.DeclaringType is Struct))
                        {
                            // can't match the pattern. let's bail
                            prefix = null;
                            pFlatten = false;
                            break;
                        }

                        binding = vBinding;
                        targetObject = binding.TargetObject;
                        // for output parameters
                        while (targetObject is AddressDereference)
                            targetObject = ((AddressDereference)targetObject).Address;
                    }

                    if (pFlatten)
                    {
                        Debug.Assert(prefix != null);
                        propName = "__strprops_" + propName;
                        return new QualifiedIdentifier(prefix, Identifier.For(propName));
                    }
                    else
                    {
                        Expression structRefExpr = Templates.GetExpressionTemplate("StructFieldAccess");
                        Replacer.Replace(structRefExpr, "_structExpr", this.VisitExpression(targetObject));
                        Replacer.Replace(structRefExpr, "_fieldName", field.Name);

                        return structRefExpr;
                    }
                }
                else
                {
                    Expression derefExpr = Templates.GetExpressionTemplate("ClassFieldAccess");
                    Replacer.Replace(derefExpr, "_objectType", field.DeclaringType.Name);
                    Replacer.Replace(derefExpr, "_ptrExpr", this.VisitExpression(targetObject));
                    Replacer.Replace(derefExpr, "_fieldName", field.Name);

                    return derefExpr;
                }
            }
            else
                return base.VisitExpression(binding);
        }

        public override Expression VisitMethodCall(MethodCall call)
        {
            // We only reach this point for calls to predicate methods within "wait"
            // join conditions.
            ZMethod method = (ZMethod) ((MemberBinding) call.Callee).BoundMember;

            ExpressionList ctorArgs;
            QualifiedIdentifier methodClass = new QualifiedIdentifier(
                new QualifiedIdentifier(
                    new QualifiedIdentifier(Identifier.For("Z"), Identifier.For("Application")),
                    method.DeclaringType.Name),
                method.Name);

            MethodCall predCall =
                new MethodCall(
                    new QualifiedIdentifier(Identifier.For("p"), Identifier.For("CallPredicateMethod")),
                    new ExpressionList(
                        new Construct(
                            new MemberBinding(null, new TypeExpression(methodClass)),
                            ctorArgs = new ExpressionList())));

            ctorArgs.Add(Identifier.For("application"));

            if (!method.IsStatic)
                ctorArgs.Add(this.VisitExpression(((MemberBinding) call.Callee).TargetObject));

            for (int i=0, n=call.Operands.Count; i < n ;i++)
                ctorArgs.Add(this.VisitExpression(call.Operands[i]));

            return predCall;
        }


        public override Expression VisitUnaryExpression(UnaryExpression unaryExpression)
        {
            if (unaryExpression.NodeType == NodeType.Sizeof)
            {
                if (unaryExpression.Operand is MemberBinding)
                {
                    Expression sizeofExpr = Templates.GetExpressionTemplate("Sizeof");
                    Replacer.Replace(sizeofExpr, "_sizeofOperand", this.VisitExpression(unaryExpression.Operand));
                    return sizeofExpr;
                }
                else if (unaryExpression.Operand is Literal)
                {
                    // TODO: This only works for fixed-size arrays. It should be disallowed
                    // (by the checker) for variable size arrays.
                    Literal opLiteral = (Literal) unaryExpression.Operand;
                    if (opLiteral.Value is ZArray)
                    {
                        ZArray opArrayType = (ZArray) opLiteral.Value;

                        return new Literal(opArrayType.Sizes[0], SystemTypes.Int32);
                    }
                }
            }

            return base.VisitUnaryExpression (unaryExpression);
        }

        public override Expression VisitIndexer(Indexer indexer)
        {
            Expression indexerExpr = Templates.GetExpressionTemplate("IndexerAccess");
            Replacer.Replace(indexerExpr, "_arrayType", indexer.Object.Type.Name);
            Replacer.Replace(indexerExpr, "_ptrExpr", this.VisitExpression(indexer.Object));
            Expression intTypeRestore = Templates.GetExpressionTemplate("IntTypeRestoration");
            Replacer.Replace(intTypeRestore, "_enumValue", this.VisitExpression(indexer.Operands[0]));
            Replacer.Replace(indexerExpr, "_indexExpr", intTypeRestore);
            return indexerExpr;
        }

        public override Expression VisitMemberBinding(MemberBinding binding)
        {
            Debug.Assert(false, "Reached a member binding node unexpectedly");
            return null;
        }

		public bool visitingField;

		public Expression VisitFieldInitializer(Expression expr) 
		{
			visitingField = true;
			Expression e = VisitExpression(expr);
			visitingField = false;
			return e;
		}

        public override Expression VisitConstruct(Construct cons)
        {
            if (cons == null) return null;
            //
            // Wrap all Zing allocations in a call to application.Allocate
            //
            Expression ptrAllocation;
			ZArray arrayNode = cons.Type as ZArray;

			if (visitingField)
			{			
				if (arrayNode != null)
					ptrAllocation = Templates.GetExpressionTemplate("InitializerArrayPtrAllocation");
				// Added by Jiri Adamek
                else if (((MemberBinding)cons.Constructor).Type is NativeZOM)
                    ptrAllocation = Templates.GetExpressionTemplate("InitializerNativeZOMPtrAllocation");
                // END of added code
                else
					ptrAllocation = Templates.GetExpressionTemplate("InitializerPtrAllocation");
			}
			else 
			{
				if (arrayNode != null)
					ptrAllocation = Templates.GetExpressionTemplate("ArrayPtrAllocation");
                // Added by Jiri Adamek
                else if (((MemberBinding)cons.Constructor).Type is NativeZOM)
                    ptrAllocation = Templates.GetExpressionTemplate("NativeZOMPtrAllocation");
                // END of added code
                else
					ptrAllocation = Templates.GetExpressionTemplate("PtrAllocation");
			}

			if (arrayNode != null)
			{
				if (arrayNode.Sizes == null)
				{
					// This is a variable-sized array
					Replacer.Replace(ptrAllocation, "_size", this.VisitExpression(cons.Operands[0]));
				}
				else
				{
					// This is a constant-sized array
					Replacer.Replace(ptrAllocation, "_size", new Literal(arrayNode.Sizes[0], SystemTypes.Int32));
				}
			}
            Replacer.Replace(ptrAllocation, "_Constructor", cons.Type.Name);
            Replacer.Replace(ptrAllocation, "_ClassName", new Literal(cons.Type.Name.ToString(), SystemTypes.String));
            return ptrAllocation;
        }

        public override Expression VisitLiteral(Literal literal)
        {
            // All nulls get translated to our equivalent (zero)
            if (literal.Value == null)
                literal = new Literal(0, SystemTypes.UInt32);

            if (literal.Type != null && literal.Type is EnumNode)
            {
                // Enum field references get turned into constants by the resolver.
                // When they're referenced, we need to emit a suitable cast to make
                // them compatible with their intended usage.

                //    int EnumTypeRestoration = ((_enumType) _enumValue);
                Expression enumTypeRestore = Templates.GetExpressionTemplate("EnumTypeRestoration");
                Replacer.Replace(enumTypeRestore, "_enumType", literal.Type.Name);
                Replacer.Replace(enumTypeRestore, "_enumValue", literal);
                return enumTypeRestore;
            }

            return literal;
        }

        public override Expression VisitThis(This This)
        {
            if (This == null) return null;
            return (Expression) new Identifier("This");
        }

        public override Expression VisitImplicitThis(ImplicitThis implicitThis)
        {
            if (implicitThis == null) return null;
            return (Expression) new Identifier("This");
        }


        //
        // Custom code generation for misc standard statements
        //

        public override Statement VisitReturn(Return Return)
        {
            if (Return == null) return null;
            if (Return.Expression == null) return null;

            Return.Expression = this.VisitExpression(Return.Expression);
            Statement zingReturn = Templates.GetStatementTemplate("SetReturnValue");
            Replacer.Replace(zingReturn, "_rval", Return.Expression);
            return zingReturn;
        }

        public Statement VisitYield (YieldStatement Yield)
        {
            return null;
        }

        public override Statement VisitExpressionStatement(ExpressionStatement statement)
        {
            // Check for statements that require special handling
            AssignmentExpression assignmentExpr = statement.Expression as AssignmentExpression;
            AssignmentStatement assignmentStatement = null;
            MethodCall methodCall = statement.Expression as MethodCall;
            UnaryExpression choose = null;

            if (assignmentExpr != null)
            {
                assignmentStatement = assignmentExpr.AssignmentStatement as AssignmentStatement;
                if (assignmentStatement != null && assignmentStatement.Source is MethodCall)
                    methodCall = (MethodCall) assignmentStatement.Source;

                if (assignmentStatement != null && assignmentStatement.Source is UnaryExpression &&
                    assignmentStatement.Source.NodeType == (NodeType) ZingNodeType.Choose)
                    choose = (UnaryExpression) assignmentStatement.Source;
            }

            if (methodCall != null)
            {
                Block stmtBlock = new Block();
                stmtBlock.Statements = new StatementList();

                // The following if statement (and the if-branch) added by Jiri Adamek
                // Reason: the NativeZOM method calls are generated differntly from
                //         common Zing method calls
                // The original code is in the else-branch
                if (((MemberBinding)methodCall.Callee).BoundMember.DeclaringType is NativeZOM)
                {
                    GenerateNativeZOMCall(stmtBlock, methodCall, statement is AsyncMethodCall, assignmentStatement);
                }
                else
                {

                    if (this.secondOfTwo)
                        GenerateMethodReturn(stmtBlock, assignmentStatement, methodCall);
                    else
                        GenerateMethodCall(stmtBlock, methodCall, statement is AsyncMethodCall);
                }

                return stmtBlock;
            }
            else if (choose != null)
            {
                Statement chooseStmt;

                if (this.secondOfTwo)
                {
                    // Finishing a "choose" is always the same...
                    chooseStmt = Templates.GetStatementTemplate("FinishChoose");
                    TypeNode tn = choose.Type;
                    if (tn is Set || tn is ZArray || tn is Class || tn is Chan)
                        Replacer.Replace(chooseStmt, "_targetType", new Identifier("Pointer"));
                    else
                        Replacer.Replace(chooseStmt, "_targetType", this.VisitExpression(choose.Type.Name));

                    Replacer.Replace(chooseStmt, "_target", this.VisitExpression(assignmentStatement.Target));
                }
                else
                {
                    // Starting a "choose" is different for the static and dynamic cases
                    if (choose.Operand.Type.FullName == "Boolean")
                    {
                        chooseStmt = Templates.GetStatementTemplate("StartChooseByBoolType");
                    }
                    else if (choose.Operand.Type == SystemTypes.Type)
                    {
                        // static
                        Literal lit = choose.Operand as Literal;
                        Debug.Assert(lit != null);
                        TypeNode tn = lit.Value as TypeNode;
                        Debug.Assert(tn != null);
                        chooseStmt = Templates.GetStatementTemplate("StartChooseByType");
                        Replacer.Replace(chooseStmt, "_typeExpr", new QualifiedIdentifier(new Identifier("Application"), tn.Name));
                    }
                    else
                    {
                        // dynamic
                        chooseStmt = Templates.GetStatementTemplate("StartChooseByValue");
                        Replacer.Replace(chooseStmt, "_ptrExpr", this.VisitExpression(choose.Operand));
                    }
                }

                return chooseStmt;
            }
            else if (assignmentStatement != null && assignmentStatement.Target.Type is Set &&
                assignmentStatement.Source is BinaryExpression)
            {
                BinaryExpression binaryExpression = (BinaryExpression) assignmentStatement.Source;
                Statement setOpStmt;

                if (binaryExpression.Operand1.Type == binaryExpression.Operand2.Type)
                {
                    if (binaryExpression.NodeType == NodeType.Add)
                        setOpStmt = Templates.GetStatementTemplate("SetAddSet");
                    else
                        setOpStmt = Templates.GetStatementTemplate("SetRemoveSet");
                }
                else
                {
                    if (binaryExpression.NodeType == NodeType.Add)
                        setOpStmt = Templates.GetStatementTemplate("SetAddItem");
                    else
                        setOpStmt = Templates.GetStatementTemplate("SetRemoveItem");
                }

                Replacer.Replace(setOpStmt, "_ptrExpr", this.VisitExpression(binaryExpression.Operand1));
                Replacer.Replace(setOpStmt, "_itemExpr", this.VisitExpression(binaryExpression.Operand2));

                return setOpStmt;
            }
            else
            {
                // The default scenario...

                inExpressionStatement = true;
                Expression newExpr = this.VisitExpression(statement.Expression);
                inExpressionStatement = false;

                //
                // In some scenarios, a simple assignment becomes something much more
                // complicated. We have to detect when such a case has occurred and
                // return the more complex statement directly rather than leaving it
                // embedded within an ExpressionStatement.
                //
                AssignmentExpression newAssignmentExpr = newExpr as AssignmentExpression;
                if (newAssignmentExpr != null && !(newAssignmentExpr.AssignmentStatement is AssignmentStatement))
                    return newAssignmentExpr.AssignmentStatement;

                Statement result = new ExpressionStatement(newExpr, statement.SourceContext);
                return result;
            }
        }

        public override Statement VisitBranch(Branch branch)
        {
            Debug.Assert(false, "Unexpected Branch node in Normalizer");
            return null;
        }

        public override Statement VisitAssignmentStatement(AssignmentStatement assignment)
        {
            if (assignment.Source == null || assignment.Target == null)
                return null;

            Expression normalizedSource = this.VisitExpression(assignment.Source);
            Expression normalizedTarget = this.VisitExpression(assignment.Target);


            assignment.Source = normalizedSource;
            assignment.Target = normalizedTarget;

            if (inExpressionStatement)
                return assignment;
            else
            {
                // This form shows up in initialized local variables. We have to wrap the
                // assignment back up to get the decompiler to generate appropriate code.
                ExpressionStatement exprStmt = new ExpressionStatement(new AssignmentExpression(assignment));
                exprStmt.SourceContext = assignment.SourceContext;
                return exprStmt;
            }
        }

        public override Expression VisitAssignmentExpression(AssignmentExpression assignment)
        {
            if (assignment == null) return null;
            AssignmentExpression newAssignment = new AssignmentExpression();
            newAssignment.AssignmentStatement = (Statement)this.Visit(assignment.AssignmentStatement);

            // For symbolic execution, assignment statements become method calls. So we
            // have to rewrite the IR so that these are no longer buried inside an
            // AssignmentStatement.
            ExpressionStatement exprStmt = newAssignment.AssignmentStatement as ExpressionStatement;
            if (exprStmt != null)
                return exprStmt.Expression;
            else
                return newAssignment;
        }

        private void GenerateMethodReturn(Block block, AssignmentStatement assignmentStatement, MethodCall call)
        {
            ZMethod method = (ZMethod) ((MemberBinding) call.Callee).BoundMember;

            // Eventually, this will be checked by an earlier phase.
            Debug.Assert(method.Parameters.Count == call.Operands.Count);

            // process output parameters and the return value;
            for (int i=0, n = call.Operands.Count; i < n ;i++)
            {
                Parameter param = method.Parameters[i];
                if ((param.Flags & ParameterFlags.Out) != 0 && call.Operands[i] != null && method.Parameters[i] != null)
                {
                    Statement assignOutParam = Templates.GetStatementTemplate("FetchOutputParameter");
                    Replacer.Replace(assignOutParam, "_dest",
                        this.VisitExpression(((UnaryExpression) call.Operands[i]).Operand));
                    Replacer.Replace(assignOutParam, "_paramName", new Identifier("_Lfc_" + method.Parameters[i].Name.Name));
                    Replacer.Replace(assignOutParam, "_Callee", method.Name);
                    Replacer.Replace(assignOutParam, "_CalleeClass", method.DeclaringType.Name);
                    block.Statements.Add(assignOutParam);
                }
            }

            if (assignmentStatement != null)
            {
                Statement assignReturnValue = Templates.GetStatementTemplate("FetchReturnValue");
                Replacer.Replace(assignReturnValue, "_dest", this.VisitExpression(assignmentStatement.Target));
                Replacer.Replace(assignReturnValue, "_CalleeClass", method.DeclaringType.Name);
                Replacer.Replace(assignReturnValue, "_Callee", method.Name);

                block.Statements.Add(assignReturnValue);
            }

            Statement stmt = Templates.GetStatementTemplate("InvalidateLastFunctionCompleted");
            block.Statements.Add(stmt);
        }

        private void GenerateMethodCall(Block block, MethodCall call, bool callIsAsync)
        {
            ZMethod method = (ZMethod) ((MemberBinding) call.Callee).BoundMember;

            // Eventually, this will be checked by an earlier phase.
            Debug.Assert(method.Parameters.Count == call.Operands.Count);

            Statement createCallFrame;
            if (method.DeclaringType is Interface)
            {
                createCallFrame = Templates.GetStatementTemplate("CreateCallFrameForInterface");
                Replacer.Replace(createCallFrame, "_thisExpr", this.VisitExpression(((MemberBinding)call.Callee).TargetObject));
                Replacer.Replace(createCallFrame, "_CreateMethod", new Identifier("Create" + method.Name.Name));
            }
            else
            {
                createCallFrame = Templates.GetStatementTemplate("CreateCallFrame");
            }
            Replacer.Replace(createCallFrame, "_calleeClass", new Identifier(method.DeclaringType.FullName));
            Replacer.Replace(createCallFrame, "_Callee", method.Name);
            block.Statements.Add(createCallFrame);

            // process input parameters
            for (int i=0, n = call.Operands.Count; i < n ;i++)
            {
                Parameter param = method.Parameters[i];

                if ((param.Flags & ParameterFlags.Out) == 0 && call.Operands[i] != null && method.Parameters[i] != null)
                {
                    Statement assignInParam = Templates.GetStatementTemplate("SetInputParameter");
                    Replacer.Replace(assignInParam, "_src", this.VisitExpression(call.Operands[i]));
                    Replacer.Replace(assignInParam, "_paramName", new Identifier("priv_" + method.Parameters[i].Name.Name));
                    block.Statements.Add(assignInParam);
                }
            }

            if (!method.IsStatic)
            {
                Statement setThis = Templates.GetStatementTemplate("SetThis");
                Replacer.Replace(setThis, "_thisExpr", this.VisitExpression(((MemberBinding) call.Callee).TargetObject));
                block.Statements.Add(setThis);
            }

			if (callIsAsync)
			{
				ExpressionStatement asyncInvoke = (ExpressionStatement) Templates.GetStatementTemplate("InvokeAsyncMethod");

                Replacer.Replace(asyncInvoke, "_methodName",
					new Literal(method.DeclaringType.Name.Name + "." + method.Name.Name, SystemTypes.String));
                Replacer.Replace(asyncInvoke, "_context", splicer.SourceContextConstructor(call.SourceContext));
                Replacer.Replace(asyncInvoke, "_contextAttr", splicer.ContextAttributeConstructor(attributes));

				block.Statements.Add(asyncInvoke);
			}
			else
			{
				block.Statements.Add(Templates.GetStatementTemplate("InvokeMethod"));
				block.Statements.Add(Templates.GetStatementTemplate("SetIsCall"));
			}
        }

        // The method added by Jiri Adamek
        // It generates NAtiveZOM calls
        private void GenerateNativeZOMCall(Block block, MethodCall call, bool callIsAsync, AssignmentStatement assignmentStatement)
        {
            ZMethod method = (ZMethod)((MemberBinding)call.Callee).BoundMember;

            // Eventually, this will be checked by an earlier phase.
            Debug.Assert(method.Parameters.Count == call.Operands.Count);

            // Asynchronous calls
            Debug.Assert(!callIsAsync, "async not supporrted for NativeZOM calls");

            // Debugging - parameters
            for (int i = 0, n = call.Operands.Count; i < n; i++)
            {
                Parameter param = method.Parameters[i];

                Debug.Assert(param != null);
                
                // In fact, call.operands[i] MAY BE null due to the error recovery (if the type of the 
                // expression does not match the type in the method definition)
                //
                // Debug.Assert(call.Operands[i] != null);
                
                Debug.Assert((param.Flags & ParameterFlags.Out) == 0, "out parameters not supported for NativeZOM calls");
            }            

            Expression typename = new QualifiedIdentifier(new Identifier("Microsoft.Zing"), method.DeclaringType.Name);
            Expression callee;

            if (!method.IsStatic)
            {
                callee = Templates.GetExpressionTemplate("NativeZOMCallee");
                Replacer.Replace(callee, "_TypeName", typename);
                Expression pointer = this.VisitExpression(((MemberBinding)call.Callee).TargetObject);
                Replacer.Replace(callee, "_Pointer", pointer);
                Replacer.Replace(callee, "_MethodName", method.Name);
            }
            else
            {
                callee = Templates.GetExpressionTemplate("NativeZOMStaticCall");                
                Replacer.Replace(callee, "_ClassName", typename);
                Replacer.Replace(callee, "_MethodName", method.Name);
            }
            
            ExpressionList argumentList = new ExpressionList();
            argumentList.Add(Templates.GetExpressionTemplate("NativeZOMCallFirstArgument"));
            foreach (Expression operand in call.Operands) argumentList.Add(this.VisitExpression(operand));                        
            MethodCall nativeCall = new MethodCall(callee, argumentList);
            
            Statement newStatement;
            if (assignmentStatement != null)
            {
                newStatement = Templates.GetStatementTemplate("NativeZOMCallWithAssignment");
                Replacer.Replace(newStatement, "_Dest", this.VisitExpression(assignmentStatement.Target));
                Replacer.Replace(newStatement, "_Source", nativeCall);
            }
            else
            {
                newStatement = new ExpressionStatement(nativeCall);
            }            
            
            block.Statements.Add(newStatement);
        }


        //
        // Handle code generation unique to various Zing statements
        //

        private Statement VisitAssert(AssertStatement assert)
        {
            Statement assertStmt;


            assertStmt = Templates.GetStatementTemplate(
                assert.Comment != null ? "AssertWithComment" : "AssertWithoutComment");

            Replacer.Replace(assertStmt, "_exprString", new Literal(assert.booleanExpr.SourceContext.SourceText, SystemTypes.String));
            Replacer.Replace(assertStmt, "_expr", this.VisitExpression(assert.booleanExpr));

            if (assert.Comment != null)
                Replacer.Replace(assertStmt, "_comment", new Literal(assert.Comment, SystemTypes.String));

            return assertStmt;
        }

        private Statement VisitAccept (AcceptStatement accept)
        {
            Statement acceptStmt;
            acceptStmt = Templates.GetStatementTemplate("Accept");
            Replacer.Replace(acceptStmt, "_expr", this.VisitExpression(accept.booleanExpr));
            return acceptStmt;
        }

        private Statement VisitAssume(AssumeStatement assume)
        {
            Statement assumeStmt;

            if (assume == null || assume.booleanExpr == null)
                return null;
            
            assumeStmt = Templates.GetStatementTemplate("Assume");
            Replacer.Replace(assumeStmt, "_exprString", new Literal(assume.booleanExpr.SourceContext.SourceText, SystemTypes.String));
            Replacer.Replace(assumeStmt, "_expr", this.VisitExpression(assume.booleanExpr));

            return assumeStmt;
        }

        private Statement VisitEventStatement(EventStatement Event)
        {
            Statement eventStmt = Templates.GetStatementTemplate("Event");

            Replacer.Replace(eventStmt, "_chanExpr", this.VisitExpression(Event.channelNumber));
            Replacer.Replace(eventStmt, "_msgExpr", this.VisitExpression(Event.messageType));
            Replacer.Replace(eventStmt, "_dirExpr", this.VisitExpression(Event.direction));
            Replacer.Replace(eventStmt, "_context", splicer.SourceContextConstructor(Event.SourceContext));
            Replacer.Replace(eventStmt, "_contextAttr", splicer.ContextAttributeConstructor(this.attributes));

            return eventStmt;
        }


        private Statement VisitInvokePlugin(InvokePluginStatement InvokePlugin)
        {
            Statement InvokePluginStmt = Templates.GetStatementTemplate("InvokePlugin");

            // Append the user's arguments to the call to StateImpl.Trace

            ExpressionStatement exprStmt = InvokePluginStmt as ExpressionStatement;
            Debug.Assert(exprStmt != null);

            MethodCall mcall = exprStmt.Expression as MethodCall;
            Debug.Assert(mcall != null);

            ExpressionList operands = this.VisitExpressionList(InvokePlugin.Operands);

            for (int i = 0, n = operands.Count; i < n; i++)
                mcall.Operands.Add(operands[i]);

            return InvokePluginStmt;
        }

        private Statement VisitInvokeSched (InvokeSchedulerStatement InvokeSched)
        {
            Statement InvokeSchedStmt = Templates.GetStatementTemplate("InvokeSched");

            // Append the user's arguments to the call to StateImpl.Trace

            ExpressionStatement exprStmt = InvokeSchedStmt as ExpressionStatement;
            Debug.Assert(exprStmt != null);

            MethodCall mcall = exprStmt.Expression as MethodCall;
            Debug.Assert(mcall != null);

            ExpressionList operands = this.VisitExpressionList(InvokeSched.Operands);

            for (int i = 0, n = operands.Count; i < n; i++)
                mcall.Operands.Add(operands[i]);

            return InvokeSchedStmt;
        }
        private Statement VisitTrace(TraceStatement trace)
        {
            Statement traceStmt = Templates.GetStatementTemplate("Trace");

            Replacer.Replace(traceStmt, "_context", splicer.SourceContextConstructor(trace.SourceContext));
            Replacer.Replace(traceStmt, "_contextAttr", splicer.ContextAttributeConstructor(this.attributes));

            // Append the user's arguments to the call to StateImpl.Trace

            ExpressionStatement exprStmt = traceStmt as ExpressionStatement;
            Debug.Assert(exprStmt != null);

            MethodCall mcall = exprStmt.Expression as MethodCall;
            Debug.Assert(mcall != null);

            ExpressionList operands = this.VisitExpressionList(trace.Operands);

            for (int i=0, n = operands.Count; i < n ;i++)
                mcall.Operands.Add(operands[i]);

            return traceStmt;
        }

        private Expression VisitIn(BinaryExpression expr)
        {
            if (expr.Operand1 == null || expr.Operand2 == null)
                return null;

            Expression inExpr = Templates.GetExpressionTemplate("SetMembershipTest");
            Replacer.Replace(inExpr, "_setOperand", this.VisitExpression(expr.Operand2));
            Replacer.Replace(inExpr, "_itemOperand", this.VisitExpression(expr.Operand1));
            return inExpr;
        }

        private Statement VisitAsync(AsyncMethodCall async)
        {
            return (Statement) this.VisitExpressionStatement((ExpressionStatement) async);
        }

        private Statement VisitJoinStatement(JoinStatement joinstmt)
        {
            //
            // The first block of a join statement pair only tests the condition.
            // It needs no executable statements.
            //
            if (!this.secondOfTwo)
                return null;

            bool anyEvents = false;

            Block stmtBlock = new Block();
            stmtBlock.Statements = new StatementList();

            for (int i=0, n=joinstmt.joinPatternList.Length; i < n ;i++)
            {
                JoinPattern jp = joinstmt.joinPatternList[i];
                ReceivePattern receivePattern = jp as ReceivePattern;
                WaitPattern waitPattern = jp as WaitPattern;
                EventPattern eventPattern = jp as EventPattern;

                if (receivePattern != null)
                {
                    // Construct a statement to actually perform the receive.
                    Statement receiveStmt = Templates.GetStatementTemplate("ReceivePattern");
                    Replacer.Replace(receiveStmt, "_chanExpr", this.VisitExpression(receivePattern.channel));
                    Replacer.Replace(receiveStmt, "_chanType", receivePattern.channel.Type.Name);
                    Replacer.Replace(receiveStmt, "_target", this.VisitExpression(receivePattern.data));
                    Replacer.Replace(receiveStmt, "_context", splicer.SourceContextConstructor(jp.SourceContext));
                    Replacer.Replace(receiveStmt, "_contextAttr", splicer.ContextAttributeConstructor(this.attributes));

                    //
                    // If the type is complex, we need to cast to Z.Pointer instead of the given type
                    //
                    TypeNode tn = receivePattern.data.Type;
                    if (tn is Set || tn is ZArray || tn is Class || tn is Chan)
                        Replacer.Replace(receiveStmt, "_targetType", new Identifier("Pointer"));
                    else
                        Replacer.Replace(receiveStmt, "_targetType", receivePattern.data.Type.Name);

                    stmtBlock.Statements.Add(receiveStmt);;
                }
                else if (eventPattern != null)
                {
                    Statement eventStmt = Templates.GetStatementTemplate("Event");

                    Replacer.Replace(eventStmt, "_chanExpr", this.VisitExpression(eventPattern.channelNumber));
                    Replacer.Replace(eventStmt, "_msgExpr", this.VisitExpression(eventPattern.messageType));
                    Replacer.Replace(eventStmt, "_dirExpr", this.VisitExpression(eventPattern.direction));
                    Replacer.Replace(eventStmt, "_context", splicer.SourceContextConstructor(eventPattern.SourceContext));
                    Replacer.Replace(eventStmt, "_contextAttr", splicer.ContextAttributeConstructor(this.attributes));

                    stmtBlock.Statements.Add(eventStmt);

                    anyEvents = true;
                }
            }

            if (joinstmt.visible && !anyEvents)
            {
                Statement eventStmt = Templates.GetStatementTemplate("TauEvent");
                Replacer.Replace(eventStmt, "_context", splicer.SourceContextConstructor(joinstmt.SourceContext));
                Replacer.Replace(eventStmt, "_contextAttr", splicer.ContextAttributeConstructor(this.attributes));

                stmtBlock.Statements.Add(eventStmt);
            }

            return stmtBlock;
        }

        private Expression VisitSelf(SelfExpression self)
        {
            var _self = Templates.GetExpressionTemplate("SelfAccess");
            return _self;
        }

        private Statement VisitSend(SendStatement send)
        {
            Statement sendStmt = Templates.GetStatementTemplate("Send");
            Replacer.Replace(sendStmt, "_chanExpr", this.VisitExpression(send.channel));
            Replacer.Replace(sendStmt, "_msgExpr", this.VisitExpression(send.data));
            Replacer.Replace(sendStmt, "_chanType", send.channel.Type.Name);
            Replacer.Replace(sendStmt, "_context", splicer.SourceContextConstructor(send.SourceContext));
            Replacer.Replace(sendStmt, "_contextAttr", splicer.ContextAttributeConstructor(this.attributes));

            return sendStmt;
        }
    }
}
