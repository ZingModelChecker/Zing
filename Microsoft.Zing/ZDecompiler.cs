using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Compiler;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;

namespace Microsoft.Zing
{
    public class ZingDecompiler : StandardVisitor
    {
        public ZingDecompiler(CodeGeneratorOptions options)
        {
            this.options = options;

            this.braceOnNewLine = (options.BracingStyle != "Block");

            indentStrings = new string[maxLevel];

            indentStrings[0] = string.Empty;
            indentStrings[1] = options.IndentString;

            for (int i = 2; i < maxLevel; i++)
                indentStrings[i] = indentStrings[i - 1] + indentStrings[1];
        }

        #region Code-generation helpers

        private void In()
        {
            indentLevel++;
            if (indentLevel >= maxLevel)
                throw new InvalidOperationException("Maximum indentation level exceeded");
        }

        private void Out()
        {
            indentLevel--;
            Debug.Assert(indentLevel >= 0);
        }

        private void WriteLine(string format, params object[] args)
        {
            if (!string.IsNullOrEmpty(currentLine))
                WriteFinish(string.Empty);

            lines.Add(indentStrings[indentLevel] +
                string.Format(CultureInfo.InvariantCulture, format, args));
        }

        private void WriteLine(string str)
        {
            if (!string.IsNullOrEmpty(currentLine))
                WriteFinish(string.Empty);

            lines.Add(indentStrings[indentLevel] + str);
        }

        private void Write(string format, params object[] args)
        {
            currentLine += string.Format(CultureInfo.InvariantCulture, format, args);
        }

        private void Write(string str)
        {
            currentLine += str;
        }

        private void WriteStart(string format, params object[] args)
        {
            if (!string.IsNullOrEmpty(currentLine))
                WriteFinish(string.Empty);

            currentLine = indentStrings[indentLevel] +
                string.Format(CultureInfo.InvariantCulture, format, args);
        }

        private void WriteStart(string str)
        {
            if (!string.IsNullOrEmpty(currentLine))
                WriteFinish(string.Empty);

            currentLine = indentStrings[indentLevel] + str;
        }

#if UNUSED
        private void WriteFinish(string format, params object[] args)
        {
            lines.Add(currentLine + string.Format(format, args));
            currentLine = string.Empty;
        }
#endif

        private void WriteFinish(string str)
        {
            lines.Add(currentLine + str);
            currentLine = string.Empty;
        }

        private string currentLine = string.Empty;
        private List<string> lines;

        #endregion Code-generation helpers

        private int SourceLength { get { return currentLine.Length; } }

        private int indentLevel;            // current indentation level (during decompilation)
        private string[] indentStrings;
        private const int maxLevel = 30;
        private bool braceOnNewLine;
        private CodeGeneratorOptions options;

        public void Decompile(Node node, TextWriter tw)
        {
            lines = new List<string>(1000);
            indentLevel = 0;

            this.Visit(node);

            foreach (string s in lines)
                tw.WriteLine(s);
        }

        [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        public override Node Visit(Node node)
        {
            if (node == null) return null;

            //
            // Short-circuit unsupported node types here and throw an exception
            //
            switch (node.NodeType)
            {
                case NodeType.Yield:
                case NodeType.AddressDereference:
                case NodeType.AliasDefinition:
                case NodeType.Assembly:
                case NodeType.AssemblyReference:
                case NodeType.Attribute:
                case NodeType.Base:
                case NodeType.BlockExpression:
                case NodeType.Branch:
                case NodeType.Catch:
                case NodeType.Composition:
                case NodeType.ConstrainedType:
                case NodeType.ConstructArray:
                case NodeType.ConstructDelegate:
                case NodeType.ConstructFlexArray:
                case NodeType.ConstructIterator:
                case NodeType.ConstructTuple:
                case NodeType.Continue:
                case NodeType.DelegateNode:
                case NodeType.DoWhile:
                case NodeType.EndFilter:
                case NodeType.EndFinally:
                case NodeType.Event:
                case NodeType.Exit:
                case NodeType.ExpressionSnippet:
                case NodeType.FaultHandler:
                case NodeType.FieldInitializerBlock:
                case NodeType.Filter:
                case NodeType.Finally:
                case NodeType.For:
                case NodeType.Interface:
                case NodeType.InterfaceExpression:
                case NodeType.InstanceInitializer:
                case NodeType.Local:
                case NodeType.LRExpression:
                case NodeType.Module:
                case NodeType.NameBinding:
                case NodeType.NamedArgument:
                case NodeType.PrefixExpression:
                case NodeType.PostfixExpression:
                case NodeType.Property:
                case NodeType.Repeat:
                case NodeType.SetterValue:
                case NodeType.StatementSnippet:
                case NodeType.StaticInitializer:
                case NodeType.Switch:
                case NodeType.SwitchCase:
                case NodeType.SwitchInstruction:
                case NodeType.Typeswitch:
                case NodeType.TypeswitchCase:
                case NodeType.Try:
                case NodeType.TypeAlias:
                case NodeType.TypeMemberSnippet:
                case NodeType.TypeParameter:
                    throw new InvalidOperationException(
                        string.Format(CultureInfo.CurrentUICulture,
                        "Invalid node type for Zing ({0})", node.NodeType));

                default:
                    break;
            }

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
                    if (node.NodeType == NodeType.TypeExpression)
                        return this.VisitTypeExpression((TypeExpression)node);

                    return base.Visit(node);
            }
        }

        /// <summary>
        /// This method is called in contexts where surrounding parentheses
        /// are required, but we wish to avoid extraneous ones. If the
        /// expression is a BinaryExpression or UnaryExpression, then we
        /// need not supply our own parens here.
        /// </summary>
        /// <param name="expr"></param>
        /// <returns></returns>
        private Expression VisitParenthesizedExpression(Expression expr)
        {
            if (expr is BinaryExpression)
                this.VisitExpression(expr);
            else
            {
                Write("(");
                this.VisitExpression(expr);
                Write(")");
            }
            return expr;
        }

        private TypeExpression VisitTypeExpression(TypeExpression tExpr)
        {
            this.Visit(tExpr.Expression);

            return tExpr;
        }

        #region Zing nodes

        private ZArray VisitArray(ZArray array)
        {
            if (array.IsDynamic)
                WriteStart("array {0}[] ", array.Name.Name);
            else
                WriteStart("array {0}[{1}] ", array.Name.Name, array.Sizes[0]);

            this.VisitTypeReference(array.ElementType);
            WriteFinish(";");

            return array;
        }

        private AssertStatement VisitAssert(AssertStatement assert)
        {
            if (assert.Comment != null)
            {
                WriteStart("assert(");
                this.VisitExpression(assert.booleanExpr);
                Write(", @\"{0}\"", assert.Comment.Replace("\"", "\"\""));
                WriteFinish(");");
            }
            else
            {
                WriteStart("assert");
                this.VisitParenthesizedExpression(assert.booleanExpr);
                WriteFinish(";");
            }
            return assert;
        }

        private AcceptStatement VisitAccept(AcceptStatement accept)
        {
            WriteStart("accept");
            this.VisitParenthesizedExpression(accept.booleanExpr);
            WriteFinish(";");

            return accept;
        }

        private AssumeStatement VisitAssume(AssumeStatement assume)
        {
            WriteStart("assume");
            this.VisitParenthesizedExpression(assume.booleanExpr);
            WriteFinish(";");

            return assume;
        }

        private EventStatement VisitEventStatement(EventStatement Event)
        {
            WriteStart("event(");
            this.VisitExpression(Event.ChannelNumber);
            Write(", ");
            this.VisitExpression(Event.MessageType);
            Write(", ");
            this.VisitExpression(Event.Direction);
            WriteFinish(");");

            return Event;
        }

        private TraceStatement VisitTrace(TraceStatement trace)
        {
            WriteStart("trace(");
            this.VisitExpressionList(trace.Operands);
            WriteFinish(");");

            return trace;
        }

        private InvokePluginStatement VisitInvokePlugin(InvokePluginStatement InvokePlugin)
        {
            WriteStart("invokeplugin(");
            this.VisitExpressionList(InvokePlugin.Operands);
            WriteFinish(");");

            return InvokePlugin;
        }

        private InvokeSchedulerStatement VisitInvokeSched(InvokeSchedulerStatement InvokeSched)
        {
            WriteStart("invokescheduler(");
            this.VisitExpressionList(InvokeSched.Operands);
            WriteFinish(");");

            return InvokeSched;
        }

        private AsyncMethodCall VisitAsync(AsyncMethodCall async)
        {
            WriteLine("async");
            this.VisitExpressionStatement((ExpressionStatement)async);

            return async;
        }

        private AtomicBlock VisitAtomic(AtomicBlock atomic)
        {
            WriteStart("atomic");
            return (AtomicBlock)this.VisitBlock((Block)atomic);
        }

        private AttributedStatement VisitAttributedStatement(AttributedStatement attributedStmt)
        {
            this.VisitAttributeList(attributedStmt.Attributes);
            this.Visit(attributedStmt.Statement);
            return attributedStmt;
        }

        private Chan VisitChan(Chan chan)
        {
            WriteStart("chan {0} ", chan.Name.Name);
            this.VisitTypeReference(chan.ChannelType);
            WriteFinish(";");

            return chan;
        }

        private UnaryExpression VisitChoose(UnaryExpression expr)
        {
            Write("choose");
            this.VisitParenthesizedExpression(expr.Operand);

            return expr;
        }

        private BinaryExpression VisitIn(BinaryExpression expr)
        {
            Write("(");
            this.Visit(expr.Operand1);
            Write(" in ");
            this.Visit(expr.Operand2);
            Write(")");

            return expr;
        }

        private Range VisitRange(Range range)
        {
            WriteStart("range {0} ", range.Name.Name);
            this.VisitExpression(range.Min);
            Write(" .. ");
            this.VisitExpression(range.Max);
            WriteFinish(";");

            return range;
        }

        private SendStatement VisitSend(SendStatement send)
        {
            WriteStart("send(");
            this.VisitExpression(send.channel);
            Write(", ");
            this.VisitExpression(send.data);
            WriteFinish(");");

            return send;
        }

        private Set VisitSet(Set @set)
        {
            WriteStart("set {0} ", @set.Name.Name);
            this.VisitTypeReference(@set.SetType);
            WriteFinish(";");

            return @set;
        }

        private Select VisitSelect(Select select)
        {
            WriteStart("select ");

            if (select.deterministicSelection)
                Write("first ");

            if (select.Visible)
                Write("visible ");

            if (select.validEndState)
                Write("end ");

            if (this.braceOnNewLine)
            {
                WriteFinish(string.Empty);
                WriteLine("{");
            }
            else
                WriteFinish("{");

            In();

            for (int i = 0, n = select.joinStatementList.Length; i < n; i++)
                this.VisitJoinStatement(select.joinStatementList[i]);

            Out();
            WriteLine("}");

            return select;
        }

        private JoinStatement VisitJoinStatement(JoinStatement joinstmt)
        {
            this.VisitAttributeList(joinstmt.Attributes);
            WriteStart(string.Empty);
            for (int i = 0, n = joinstmt.joinPatternList.Length; i < n; i++)
            {
                this.Visit(joinstmt.joinPatternList[i]);

                if ((i + 1) < n)
                    Write(" && ");
            }

            Write(" -> ");
            bool indentStmt = !(joinstmt.statement is Block);

            if (indentStmt)
                In();
            this.Visit(joinstmt.statement);
            if (indentStmt)
                Out();

            return joinstmt;
        }

        private WaitPattern VisitWaitPattern(WaitPattern wp)
        {
            Write("wait ");
            this.VisitParenthesizedExpression(wp.expression);

            return wp;
        }

        private ReceivePattern VisitReceivePattern(ReceivePattern rp)
        {
            Write("receive(");
            this.Visit(rp.channel);
            Write(", ");
            this.Visit(rp.data);
            Write(")");

            return rp;
        }

        private EventPattern VisitEventPattern(EventPattern ep)
        {
            Write("event(");
            this.Visit(ep.ChannelNumber);
            Write(", ");
            this.Visit(ep.MessageType);
            Write(", ");
            this.Visit(ep.Direction);
            Write(")");

            return ep;
        }

        private TimeoutPattern VisitTimeoutPattern(TimeoutPattern tp)
        {
            Write("timeout");

            return tp;
        }

        private ZTry VisitZTry(ZTry Try)
        {
            WriteStart("try");
            this.VisitBlock(Try.Body);

            WriteLine("with");
            WriteLine("{");
            In();
            for (int i = 0, n = Try.Catchers.Length; i < n; i++)
                this.VisitWith(Try.Catchers[i]);
            Out();
            WriteLine("}");
            return Try;
        }

        private With VisitWith(With with)
        {
            if (with.Name != null)
                WriteStart("{0} -> ", with.Name.Name);
            else
                WriteStart("* -> ");

            this.VisitBlock(with.Block);

            return with;
        }

        public override Statement VisitThrow(Throw Throw)
        {
            WriteLine("raise {0};", ((Identifier)Throw.Expression).Name);

            return Throw;
        }

        #endregion Zing nodes

        #region CCI nodes

        public override Expression VisitAssignmentExpression(AssignmentExpression assignment)
        {
            return base.VisitAssignmentExpression(assignment);
        }

        public override Statement VisitAssignmentStatement(AssignmentStatement assignment)
        {
            this.VisitExpression(assignment.Target);
            Write(" {0} ", GetAssignmentOperator(assignment.Operator));
            this.VisitExpression(assignment.Source);
            return assignment;
        }

        public override Expression VisitAttributeConstructor(AttributeNode attribute)
        {
            throw new NotImplementedException("Node type not yet supported");
        }

        public override AttributeNode VisitAttributeNode(AttributeNode attribute)
        {
            // Ignore the ParamArray attribute
            if (attribute.Constructor is MemberBinding)
            {
                MemberBinding mb = (MemberBinding)attribute.Constructor;

                if (mb.BoundMember.DeclaringType.Name.Name == "ParamArrayAttribute")
                    return attribute;
            }

            WriteStart("[");
            this.VisitExpression(attribute.Constructor);
            Write("(");
            this.VisitExpressionList(attribute.Expressions);
            WriteFinish(")]");
            return attribute;
        }

        public override AttributeList VisitAttributeList(AttributeList attributes)
        {
            return base.VisitAttributeList(attributes);
        }

        public override Expression VisitBinaryExpression(BinaryExpression binaryExpression)
        {
            Write("(");
            if ((binaryExpression.NodeType == NodeType.Castclass) ||
                (binaryExpression.NodeType == NodeType.ExplicitCoercion))
            {
                Write("(");
                this.VisitExpression(binaryExpression.Operand2);
                Write(") ");
                this.VisitExpression(binaryExpression.Operand1);
            }
            else
            {
                this.VisitExpression(binaryExpression.Operand1);
                Write(" {0} ", GetBinaryOperator(binaryExpression.NodeType));
                this.VisitExpression(binaryExpression.Operand2);
            }
            Write(")");
            return binaryExpression;
        }

        public override Block VisitBlock(Block block)
        {
            // We special-case this because it's how a null statement is
            // represented.
            if (block.Statements == null)
            {
                WriteLine(";");
                return block;
            }

            if (this.braceOnNewLine)
            {
                WriteFinish(string.Empty);
                WriteLine("{");
            }
            else
                WriteFinish(" {");

            In();
            Block result = base.VisitBlock(block);
            Out();
            WriteLine("}");
            return result;
        }

        public override Class VisitClass(Class Class)
        {
            WriteStart("class {0}", Class.Name.Name);

            if (this.braceOnNewLine)
            {
                WriteFinish(string.Empty);
                WriteLine("{");
            }
            else
                WriteFinish(" {");

            In();
            Class.Members = this.VisitMemberList(Class.Members);
            Out();
            WriteLine("};");

            return Class;
        }

        [SuppressMessage("Microsoft.Security", "CA2122:DoNotIndirectlyExposeMethodsWithLinkDemands")]
        public override MemberList VisitMemberList(MemberList members)
        {
            if (members == null) return null;
            for (int i = 0, n = members.Count; i < n; i++)
            {
                Member mem = members[i];
                if (mem == null) continue;
                this.Visit(mem);

                if (this.options.BlankLinesBetweenMembers && (i + 1) < n)
                    WriteLine(string.Empty);
            }
            return members;
        }

        public override Expression VisitConstruct(Construct cons)
        {
            Write("new ");

            this.VisitExpression(cons.Constructor);

            // This only happens for variable-length arrays, and only one
            // parameter is permitted.
            if (cons.Operands.Count > 0)
            {
                Write("[");
                this.VisitExpression(cons.Operands[0]);
                Write("]");
            }

            return cons;
        }

        public override EnumNode VisitEnumNode(EnumNode enumNode)
        {
            WriteStart("enum {0}", enumNode.Name.Name);

            if (this.braceOnNewLine)
            {
                WriteFinish(string.Empty);
                WriteLine("{");
            }
            else
                WriteFinish(" {");

            In();
            for (int i = 0, n = enumNode.Members.Count; i < n; i++)
            {
                Field field = (Field)enumNode.Members[i];

                if (!field.IsSpecialName)
                {
                    WriteStart(field.Name.Name);
                    WriteFinish(",");
                }
            }
            Out();
            WriteLine("};");
            return enumNode;
        }

        public override Expression VisitExpression(Expression expression)
        {
            Expression result = base.VisitExpression(expression);
            return result;
        }

        public override ExpressionList VisitExpressionList(ExpressionList list)
        {
            if (list == null) return null;
            for (int i = 0, n = list.Count; i < n; i++)
            {
                this.VisitExpression(list[i]);

                if (i < (n - 1))
                    Write(", ");
            }
            return list;
        }

        public override Statement VisitExpressionStatement(ExpressionStatement statement)
        {
            WriteStart(string.Empty);

            int len = this.SourceLength;
            base.VisitExpressionStatement(statement);
            if (len != this.SourceLength)
                Write(";");

            WriteFinish(string.Empty);

            return statement;
        }

        public override Field VisitField(Field field)
        {
            if (field == null) return null;
            WriteStart(GetFieldQualifiers(field));
            this.VisitTypeReference(field.Type);
            Write(" {0}", field.Name.Name);
            if (field.Initializer != null)
            {
                Write(" = ");
                field.Initializer = this.VisitExpression(field.Initializer);
            }
            WriteFinish(";");
            return field;
        }

        public override Statement VisitForEach(ForEach forEach)
        {
            WriteStart("foreach (");
            this.VisitTypeReference(forEach.TargetVariableType);
            Write(" ");
            this.VisitExpression(forEach.TargetVariable);
            Write(" in ");
            this.VisitExpression(forEach.SourceEnumerable);
            Write(")");
            this.VisitBlock(forEach.Body);
            return forEach;
        }

        public override Statement VisitGoto(Goto Goto)
        {
            WriteLine("goto {0};", Goto.TargetLabel.Name);
            return Goto;
        }

        public override Expression VisitIdentifier(Identifier identifier)
        {
            Write(identifier.Name);
            return identifier;
        }

        public override Statement VisitIf(If If)
        {
            WriteStart("if ");
            this.VisitParenthesizedExpression(If.Condition);
            Write("");
            this.VisitBlock(If.TrueBlock);
            if (If.FalseBlock != null)
            {
                WriteStart("else");
                this.VisitBlock(If.FalseBlock);
            }

            return If;
        }

        public override Expression VisitIndexer(Indexer indexer)
        {
            this.VisitExpression(indexer.Object);
            Write("[");
            this.VisitExpressionList(indexer.Operands);
            Write("]");
            return indexer;
        }

        public override Statement VisitLabeledStatement(LabeledStatement lStatement)
        {
            if (lStatement.Statement is Block && !this.braceOnNewLine)
                WriteStart("{0}:", lStatement.Label.Name);
            else
                WriteLine("{0}:", lStatement.Label.Name);

            return base.VisitLabeledStatement(lStatement);
        }

        public override Expression VisitLiteral(Literal literal)
        {
            // TODO: probably need special cases here for the various integer & real types.
            // Also need to investigate quoting behavior in string literals.
            if (literal.Value == null)
                Write("null");
            else if (literal.Value is string)
                Write("@\"{0}\"", literal.Value.ToString().Replace("\"", "\"\""));
            else if (literal.Value is bool)
                Write(((bool)literal.Value) ? "true" : "false");
            else if (literal.Value is char)
                Write("'\\x{0:X4}'", ((ushort)(char)literal.Value));
            else if (literal.Value is TypeNode)
                this.VisitTypeReference((TypeNode)literal.Value);
            else if (literal.Type == SystemTypes.UInt64)
                Write("{0}ul", literal.Value);
            else
                Write("{0}", literal.Value);

            return literal;
        }

        public override Expression VisitMethodCall(MethodCall call)
        {
            if (call == null) return null;

            QualifiedIdentifier id = call.Callee as QualifiedIdentifier;
            if (id != null)
            {
                if (id.Identifier.Name == ".ctor")
                    return call;
            }

            this.VisitExpression(call.Callee);
            Write("(");
            this.VisitExpressionList(call.Operands);
            Write(")");
            return call;
        }

        public override Expression VisitMemberBinding(MemberBinding memberBinding)
        {
            // TODO: Just guessing here for now - need to understand this better.

            if (memberBinding == null) return null;
            if (memberBinding.TargetObject != null)
                this.VisitExpression(memberBinding.TargetObject);
            else if (memberBinding.BoundMember is TypeExpression)
                this.VisitExpression(((TypeExpression)memberBinding.BoundMember).Expression);
            else if (memberBinding.BoundMember is TypeNode)
                this.VisitTypeReference((TypeNode)memberBinding.BoundMember);
            else if (memberBinding.BoundMember is InstanceInitializer)
                this.VisitTypeReference(((InstanceInitializer)memberBinding.BoundMember).DeclaringType);
            else
                throw new NotImplementedException("Unexpected scenario in VisitMemberBinding");
            return memberBinding;
        }

        public override Method VisitMethod(Method method)
        {
            if (method == null) return null;

            WriteStart(GetMethodQualifiers(method));
            method.ReturnType = this.VisitTypeReference(method.ReturnType);
            Write(" {0}(", method.Name.Name);
            this.VisitParameterList(method.Parameters);
            Write(")");
            this.VisitBlock(method.Body);
            return method;
        }

        public override Namespace VisitNamespace(Namespace nspace)
        {
            this.VisitTypeNodeList(nspace.Types);
            return nspace;
        }

        [SuppressMessage("Microsoft.Security", "CA2122:DoNotIndirectlyExposeMethodsWithLinkDemands")]
        public override TypeNodeList VisitTypeNodeList(TypeNodeList types)
        {
            if (types == null) return null;
            for (int i = 0; i < types.Count; i++)
            {
                types[i] = (TypeNode)this.Visit(types[i]);

                if (this.options.BlankLinesBetweenMembers && (i + 1 < types.Count))
                    WriteLine(string.Empty);
            }
            return types;
        }

        public override Expression VisitParameter(Parameter parameter)
        {
            if (parameter == null) return null;
            Write("{0}", GetParameterDirection(parameter.Flags));
            this.VisitTypeReference(parameter.Type);
            Write(" ");
            if (parameter.DefaultValue != null)
                throw new ArgumentException("Unexpected parameter default value");
            Write("{0}", parameter.Name.Name);
            return parameter;
        }

        public override ParameterList VisitParameterList(ParameterList parameterList)
        {
            if (parameterList == null) return null;
            for (int i = 0, n = parameterList.Count; i < n; i++)
            {
                this.VisitParameter(parameterList[i]);

                if (i < (n - 1))
                    Write(", ");
            }
            return parameterList;
        }

        public override Expression VisitQualifiedIdentifier(QualifiedIdentifier qualifiedIdentifier)
        {
            this.VisitExpression(qualifiedIdentifier.Qualifier);
            Write(".");
            this.VisitIdentifier(qualifiedIdentifier.Identifier);
            return qualifiedIdentifier;
        }

        public override Statement VisitReturn(Return Return)
        {
            if (Return == null) return null;

            WriteStart("return");

            if (Return.Expression != null)
            {
                Write(" ");
                this.VisitExpression(Return.Expression);
            }
            WriteFinish(";");
            return Return;
        }

        public override StatementList VisitStatementList(StatementList statements)
        {
            if (statements == null)
            {
                WriteLine(";");
                return null;
            }

            for (int i = 0, n = statements.Count; i < n; i++)
                this.Visit(statements[i]);

            return statements;
        }

        public override Struct VisitStruct(Struct Struct)
        {
            WriteStart("struct {0}", Struct.Name.Name);

            if (this.braceOnNewLine)
            {
                WriteFinish(string.Empty);
                WriteLine("{");
            }
            else
                WriteFinish(" {");

            In();
            this.VisitMemberList(Struct.Members);
            Out();
            WriteLine("};");

            return Struct;
        }

        public override Expression VisitThis(This This)
        {
            Write("this");
            return This;
        }

        private static string TranslateTypeName(string typeName)
        {
            switch (typeName)
            {
                case "Object": return "object";
                case "Void": return "void";
                case "Byte": return "byte";
                case "SByte": return "sbyte";
                case "Int16": return "short";
                case "UInt16": return "ushort";
                case "Int32": return "int";
                case "UInt32": return "uint";
                case "Int64": return "long";
                case "UInt64": return "ulong";
                case "Single": return "float";
                case "Double": return "double";
                case "Decimal": return "decimal";
                case "String": return "string";
                case "Char": return "char";
                case "Boolean": return "bool";
                default:
                    return null;
            }
        }

        public override TypeNode VisitTypeReference(TypeNode type)
        {
            string nativeType = null;

            ArrayTypeExpression ate = type as ArrayTypeExpression;
            ReferenceTypeExpression rte = type as ReferenceTypeExpression;
            InterfaceExpression ie = type as InterfaceExpression;
            TypeExpression te = type as TypeExpression;

            if (type.Name != null)
                nativeType = TranslateTypeName(type.Name.Name);

            if (nativeType != null)
                Write(nativeType);
            else if (te != null)
                this.VisitExpression(te.Expression);
            else if (ie != null)
                this.VisitExpression(ie.Expression);
            else if (ate != null)
            {
                this.VisitTypeReference(ate.ElementType);
                if (ate.Rank > 1)
                    throw new NotImplementedException("Multidimensional arrays not yet implemented");

                Write("[]");
            }
            else if (rte != null)
            {
                // This occurs with "out" parameters where we've already noted the unique
                // nature of the parameter via the parameter direction. Here, we can just
                // recurse on the ElementType and ignore the indirection. For other scenarios,
                // (e.g. unsafe code?) this would be incorrect.
                this.VisitTypeReference(rte.ElementType);
            }
            else if (type.FullName != null && type.FullName.Length > 0)
            {
                // ISSUE: Is this ever a good idea?
                int sepPos = type.FullName.IndexOf('+');
                if (sepPos > 0)
                    Write(type.FullName.Substring(sepPos + 1));
                else
                    Write(type.FullName);
            }
            else if (type.Name != null)
                Write(type.Name.Name);
            else
                throw new NotImplementedException("Unsupported type reference style");

            return type;
        }

        public override Expression VisitUnaryExpression(UnaryExpression unaryExpression)
        {
            bool isFunctionStyle;
            string opString;

            opString = GetUnaryOperator(unaryExpression.NodeType, out isFunctionStyle);

            if (isFunctionStyle)
            {
                Write("{0}(", opString);
                this.VisitExpression(unaryExpression.Operand);
                Write(")");
            }
            else
            {
                Write(opString);
                this.VisitExpression(unaryExpression.Operand);
            }
            return unaryExpression;
        }

        public override Statement VisitVariableDeclaration(VariableDeclaration variableDeclaration)
        {
            WriteStart("");
            this.VisitTypeReference(variableDeclaration.Type);
            Write(" {0}", variableDeclaration.Name.Name);
            if (variableDeclaration.Initializer != null)
            {
                Write(" = ");
                this.Visit(variableDeclaration.Initializer);
            }
            WriteFinish(";");

            return variableDeclaration;
        }

        public override Statement VisitWhile(While While)
        {
            WriteStart("while ");
            this.VisitParenthesizedExpression(While.Condition);
            this.VisitBlock(While.Body);
            return While;
        }

        private static string GetMethodQualifiers(Method method)
        {
            string qualifiers = string.Empty;

            ZMethod zm = method as ZMethod;

            if (method.IsStatic)
                qualifiers += "static ";

            if (zm != null && zm.Atomic)
                qualifiers += "atomic ";

            if (zm != null && zm.Activated)
                qualifiers += "activate ";

            return qualifiers;
        }

        private static string GetParameterDirection(ParameterFlags flags)
        {
            switch (flags)
            {
                case ParameterFlags.None: return string.Empty;
                case ParameterFlags.In: return string.Empty;
                case ParameterFlags.Out: return "out ";
                case ParameterFlags.In | ParameterFlags.Out: return "ref ";
                default:
                    throw new ArgumentException("Unexpected ParameterFlags value");
            }
        }

        private static string GetAssignmentOperator(NodeType op)
        {
            Debug.Assert(op == NodeType.Nop, "Invalid assignment operator type");
            return "=";
        }

        private static string GetFieldQualifiers(Field field)
        {
            string qualifiers = string.Empty;

            if (field.IsStatic)
                qualifiers += "static ";

            return qualifiers;
        }

        [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        private static string GetBinaryOperator(NodeType nodeType)
        {
            switch (nodeType)
            {
                case NodeType.Add:
                case NodeType.Add_Ovf_Un:
                case NodeType.Add_Ovf: return "+";
                case NodeType.And: return "&";
                case NodeType.Div: return "/";
                case NodeType.Eq: return "==";
                case NodeType.Ge: return ">=";
                case NodeType.Gt: return ">";
                case NodeType.Le: return "<=";
                case NodeType.LogicalAnd: return "&&";
                case NodeType.LogicalOr: return "||";
                case NodeType.Lt: return "<";
                case NodeType.Mul: return "*";
                case NodeType.Ne: return "!=";
                case NodeType.Or: return "|";
                case NodeType.Shl: return "<<";
                case NodeType.Shr: return ">>";
                case NodeType.Xor: return "^";
                case NodeType.Rem: return "%";
                case NodeType.Sub:
                case NodeType.Sub_Ovf_Un:
                case NodeType.Sub_Ovf: return "-";

                default:
                    throw new NotImplementedException("Binary operator not supported");
            }
        }

        private static string GetUnaryOperator(NodeType nodeType, out bool isFunctionStyle)
        {
            isFunctionStyle = false;

            switch (nodeType)
            {
                case NodeType.LogicalNot: return "!";
                case NodeType.Not: return "~";
                case NodeType.Neg: return "-";
                case NodeType.UnaryPlus: return "+";
                case NodeType.Sizeof: isFunctionStyle = true; return "sizeof";
                case NodeType.OutAddress: return "out ";
                case NodeType.RefAddress: return "ref ";
                case NodeType.Parentheses: isFunctionStyle = true; return string.Empty;

                default:
                    throw new NotImplementedException("Unary operator not supported");
            }
        }
    }

        #endregion CCI nodes
}