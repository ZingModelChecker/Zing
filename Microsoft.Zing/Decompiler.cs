using System;
using System.Collections.Generic;
using System.Compiler;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text;

namespace Microsoft.Zing
{
    internal class Decompiler : StandardVisitor
    {
        public Decompiler()
            : this(2)
        {
        }

        public Decompiler(int indentationSize)
        {
            this.indentationSize = indentationSize;

            indentStrings = new string[maxLevel];

            indentStrings[0] = string.Empty;
            indentStrings[1] = string.Empty;
            for (int i = 0; i < indentationSize; i++)
                indentStrings[1] += " ";

            for (int i = 2; i < maxLevel; i++)
                indentStrings[i] = indentStrings[i - 1] + indentStrings[1];
        }

        private static string[] keywords = new string[] {
            "abstract", "as",       "base",         "bool",     "break",
            "byte",     "case",     "catch",        "char",     "checked",
            "class",    "const",    "continue",     "decimal",  "default",
            "delegate", "do",       "double",       "else",     "enum",
            "event",    "explicit", "extern",       "false",    "finally",
            "fixed",    "float",    "for",          "foreach",  "goto",
            "if",       "implicit", "in",           "int",      "interface",
            "internal", "is",       "lock",         "long",     "namespace",
            "new",      "null",     "object",       "operator", "out",
            "override", "params",   "private",      "protected","public",
            "readonly", "ref",      "return",       "sbyte",    "sealed",
            "short",    "sizeof",   "stackalloc",   "static",   "string",
            "struct",   "switch",   "this",         "throw",    "true",
            "try",      "typeof",   "uint",         "ulong",    "unchecked",
            "unsafe",   "ushort",   "using",        "virtual",  "void",
            "volatile", "while", "yield"
        };

        private static SortedList<string, string> keywordList;

        static Decompiler()
        {
            keywordList = new SortedList<string, string>(keywords.Length);

            foreach (string s in keywords)
                keywordList.Add(s, s);
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
#if ENABLE_INDENTATION
            lines.Add(indentStrings[indentLevel] +
                string.Format(CultureInfo.InvariantCulture, format, args));
#else
            lines.Add(string.Format(format, args));
#endif
        }

        private void WriteLine(string str)
        {
#if ENABLE_INDENTATION
            lines.Add(indentStrings[indentLevel] + str);
#else
            lines.Add(str);
#endif
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
#if ENABLE_INDENTATION
            currentLine = indentStrings[indentLevel] +
                string.Format(CultureInfo.InvariantCulture, format, args);
#else
            currentLine = string.Format(format, args);
#endif
        }

        private void WriteStart(string str)
        {
#if ENABLE_INDENTATION
            currentLine = indentStrings[indentLevel] + str;
#else
            currentLine = str;
#endif
        }

#if UNUSED
        private void WriteFinish(string format, params object[] args)
        {
            lines.Add(currentLine + string.Format(format, args));
        }
#endif

        private void WriteFinish(string str)
        {
            lines.Add(currentLine + str);
        }

        private string currentLine;
        private List<string> lines;

        #endregion Code-generation helpers

        private int SourceLength { get { return currentLine.Length; } }

        private StringWriter source;        // holds the generated code
        private int indentationSize;        // # of spaces per indentation level
        private int indentLevel;            // current indentation level (during decompilation)
        private string[] indentStrings;
        private const int maxLevel = 30;

        // Used in "for" statements to suppress the semicolon on the "incrementer" portion
        // and use comma as a statement separator
        private bool processingForIncrementers;

        public string Decompile(Node node)
        {
            lines = new List<string>(1000);
            indentLevel = 0;
            processingForIncrementers = false;

            this.Visit(node);

            int totalSize = 0;
            foreach (string s in lines)
                totalSize += s.Length + 2;

            source = new StringWriter(new StringBuilder(totalSize + 100),
                CultureInfo.InvariantCulture);

            foreach (string s in lines)
                source.WriteLine(s);

            return source.ToString();
        }

        public override Statement VisitGotoCase(GotoCase gotoCase)
        {
            WriteStart("goto case ");
            this.VisitExpression(gotoCase.CaseLabel);
            WriteFinish(";");

            return gotoCase;
        }

        public override Statement VisitLocalDeclarationsStatement(LocalDeclarationsStatement localDecls)
        {
            if (localDecls.Constant)
                WriteStart("const ");
            else
                WriteStart(string.Empty);

            this.VisitTypeReference(localDecls.Type);
            Write(" ");
            this.VisitLocalDeclarationList(localDecls.Declarations);
            WriteFinish(";");

            return localDecls;
        }

        public override LocalDeclarationList VisitLocalDeclarationList(LocalDeclarationList localDeclList)
        {
            if (localDeclList == null) return null;
            for (int i = 0, n = localDeclList.Count; i < n; i++)
            {
                this.VisitLocalDeclaration(localDeclList[i]);

                if (i < (n - 1))
                    Write(", ");
            }
            return localDeclList;
        }

        public override LocalDeclaration VisitLocalDeclaration(LocalDeclaration localDecl)
        {
            this.VisitIdentifier(localDecl.Name);

            if (localDecl.InitialValue != null)
            {
                Write(" = ");
                this.VisitExpression(localDecl.InitialValue);
            }
            return localDecl;
        }

        public override Expression VisitAddressDereference(AddressDereference addr)
        {
            throw new NotImplementedException("Node type not yet supported");
        }

        public override AliasDefinition VisitAliasDefinition(AliasDefinition aliasDefinition)
        {
            WriteStart("using {0}=", aliasDefinition.Alias.Name);
            this.VisitExpression(aliasDefinition.AliasedExpression);
            WriteFinish(";");
            return aliasDefinition;
        }

        public override AssemblyNode VisitAssembly(AssemblyNode assembly)
        {
            throw new NotImplementedException("Node type not yet supported");
        }

        public override AssemblyReference VisitAssemblyReference(AssemblyReference assemblyReference)
        {
            throw new NotImplementedException("Node type not yet supported");
        }

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
            // Ignore the ParamArray & DefaultMember attributes
            if (attribute.Constructor is MemberBinding)
            {
                MemberBinding mb = (MemberBinding)attribute.Constructor;

                string attrName = mb.BoundMember.DeclaringType.Name.Name;
                if (attrName == "ParamArrayAttribute" || attrName == "DefaultMemberAttribute")
                    return attribute;
            }

            // Ignore the ParamArray & DefaultMember attributes
            if (attribute.Constructor is Literal)
            {
                Literal lit = (Literal)attribute.Constructor;
                if (lit.Value is Class)
                {
                    Class c = (Class)lit.Value;
                    if (c.Name.Name == "ParamArrayAttribute" || c.Name.Name == "DefaultMemberAttribute")
                        return attribute;
                }
            }

            WriteStart("[{0}", GetAttributeTarget(attribute.Target));
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

        public override Expression VisitBase(Base Base)
        {
            Write("base");
            return Base;
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
#if NOLINEBREAK
                Write(" " + GetBinaryOperator(binaryExpression.NodeType) + " ");
#else
				WriteFinish(" {0} ", GetBinaryOperator(binaryExpression.NodeType));
				WriteStart(string.Empty);
#endif
                this.VisitExpression(binaryExpression.Operand2);
            }
            Write(")");
            return binaryExpression;
        }

        public override Block VisitBlock(Block block)
        {
            WriteLine("{");
            In();
            base.VisitBlock(block);
            Out();
            WriteLine("}");
            return block;
        }

        public override Expression VisitBlockExpression(BlockExpression blockExpression)
        {
            throw new NotImplementedException("Node type not yet supported");
        }

        public override Statement VisitBranch(Branch branch)
        {
            throw new NotImplementedException("Node type not yet supported");
        }

        public override Statement VisitCatch(Catch Catch)
        {
            WriteStart("catch (");
            this.VisitTypeReference(Catch.TypeExpression);
            if (Catch.Variable != null)
            {
                Write(" ");
                this.VisitExpression(Catch.Variable);
            }
            WriteFinish(")");
            this.VisitBlock(Catch.Block);
            return Catch;
        }

        public override Class VisitClass(Class Class)
        {
            this.VisitAttributeList(Class.Attributes);

            WriteStart("{0}class ", GetTypeQualifiers(Class));
            this.VisitIdentifier(Class.Name);
            WriteFinish(string.Empty);

            bool hasInterfaces = Class.Interfaces != null && Class.Interfaces.Count > 0;

            if (Class.BaseClass != null || hasInterfaces)
            {
                In();
                WriteStart(": ");

                if (Class.BaseClass != null)
                {
                    Write(Class.BaseClass.FullName);

                    if (hasInterfaces)
                        Write(", ");
                }

                this.VisitInterfaceReferenceList(Class.Interfaces);

                WriteFinish(string.Empty);
                Out();
            }

            // TODO: TemplateArguments & TemplateParameters not handled

            WriteLine("{");
            In();

            this.VisitMemberList(Class.Members);

            Out();
            WriteLine("}");

            return Class;
        }

        public override MemberList VisitMemberList(MemberList members)
        {
            if (members == null) return null;
            for (int i = 0, n = members.Count; i < n; i++)
            {
                Member mem = members[i];
                if (mem == null) continue;
                //this.VisitAttributeList(mem.Attributes);
                this.Visit(mem);
            }
            return members;
        }

        public override Node VisitComposition(Composition comp)
        {
            throw new NotImplementedException("Node type not yet supported");
        }

        public override Expression VisitConstruct(Construct cons)
        {
            Write("new ");

            TypeExpression te = cons.Constructor.Type as TypeExpression;

            if (te != null)
                this.VisitExpression(te.Expression);
            else if (cons.Constructor.Type.FullName != null)
                Write(cons.Constructor.Type.FullName);
            else
                throw new NotImplementedException("Unexpected constructor style");

            Write("(");
            this.VisitExpressionList(cons.Operands);
            Write(")");
            return cons;
        }

        public override Expression VisitConstructArray(ConstructArray consArr)
        {
            if (consArr.Rank > 1)
                throw new NotImplementedException("Multi-dimensional arrays not yet supported");

            Write("new ");
            this.VisitTypeReference(consArr.ElementType);
            Write("[");
            if (consArr.Operands != null)
                this.VisitExpressionList(consArr.Operands);
            Write("]");
            if (consArr.Initializers != null)
            {
                Write(" { ");
                this.VisitExpressionList(consArr.Initializers);
                Write(" }");
            }
            return consArr;
        }

        public override Expression VisitConstructDelegate(ConstructDelegate consDelegate)
        {
            throw new NotImplementedException("Node type not yet supported");
        }

        public override Expression VisitConstructFlexArray(ConstructFlexArray consArr)
        {
            throw new NotImplementedException("Node type not yet supported");
        }

        public override Expression VisitConstructIterator(ConstructIterator consIterator)
        {
            throw new NotImplementedException("Node type not yet supported");
        }

        public override TypeNode VisitConstrainedType(ConstrainedType cType)
        {
            throw new NotImplementedException("Node type not yet supported");
        }

        public override Statement VisitContinue(Continue Continue)
        {
            WriteLine("continue;");
            return Continue;
        }

        public override DelegateNode VisitDelegateNode(DelegateNode delegateNode)
        {
            throw new NotImplementedException("Node type not yet supported");
        }

        public override Statement VisitDoWhile(DoWhile doWhile)
        {
            WriteLine("do");
            this.VisitBlock(doWhile.Body);
            WriteStart("while (");
            this.VisitExpression(doWhile.Condition);
            WriteFinish(");");
            return doWhile;
        }

        public override Statement VisitEndFilter(EndFilter endFilter)
        {
            throw new NotImplementedException("Node type not yet supported");
        }

        public override Statement VisitEndFinally(EndFinally endFinally)
        {
            throw new NotImplementedException("Node type not yet supported");
        }

        public override EnumNode VisitEnumNode(EnumNode enumNode)
        {
            WriteStart("{0}enum ", GetTypeQualifiers(enumNode));
            this.VisitIdentifier(enumNode.Name);
            Write(" : ");
            this.VisitTypeReference(enumNode.UnderlyingType);
            WriteFinish(string.Empty);
            WriteLine("{");
            In();
            for (int i = 0, n = enumNode.Members.Count; i < n; i++)
            {
                Field field = (Field)enumNode.Members[i];

                if (!field.IsSpecialName)
                {
                    WriteStart(string.Empty);
                    this.VisitIdentifier(field.Name);
                    if (field.Initializer != null)
                    {
                        Write(" = ");
                        this.VisitExpression(field.Initializer);
                    }
                    WriteFinish(",");
                }
            }
            Out();
            WriteLine("};");
            return enumNode;
        }

        public override Event VisitEvent(Event evnt)
        {
            throw new NotImplementedException("Node type not yet supported");
        }

        public override Statement VisitExit(Exit exit)
        {
            WriteLine("break;");
            return exit;
        }

        public override Expression VisitExpression(Expression expression)
        {
            return base.VisitExpression(expression);
        }

        public override ExpressionList VisitExpressionList(ExpressionList expressions)
        {
            if (expressions == null) return null;
            for (int i = 0, n = expressions.Count; i < n; i++)
            {
                this.VisitExpression(expressions[i]);

                if (i < (n - 1))
                {
#if NOLINEBREAK
                    Write(", ");
#else
					WriteFinish(",");
					WriteStart(String.Empty);
#endif
                }
            }
            return expressions;
        }

        public override Expression VisitExpressionSnippet(ExpressionSnippet snippet)
        {
            throw new NotImplementedException("Node type not yet supported");
        }

        public override Statement VisitExpressionStatement(ExpressionStatement statement)
        {
            if (!this.processingForIncrementers)
                WriteStart(string.Empty);

            int len = this.SourceLength;
            base.VisitExpressionStatement(statement);
            if (len != this.SourceLength && !this.processingForIncrementers)
                Write(";");

            if (!this.processingForIncrementers)
                WriteFinish(string.Empty);

            return statement;
        }

        public override Statement VisitFaultHandler(FaultHandler faultHandler)
        {
            throw new NotImplementedException("Node type not yet supported");
        }

        public override Field VisitField(Field field)
        {
            if (field == null) return null;
            WriteStart(GetFieldQualifiers(field));
            this.VisitTypeReference(field.Type);
            Write(" ");
            this.VisitIdentifier(field.Name);
            if (field.Initializer != null)
            {
                Write(" = ");
                field.Initializer = this.VisitExpression(field.Initializer);
            }
            WriteFinish(";");
            return field;
        }

        public override Block VisitFieldInitializerBlock(FieldInitializerBlock block)
        {
            // TODO: is this ever interesting??

            return base.VisitFieldInitializerBlock(block);
        }

        public override Statement VisitFilter(Filter filter)
        {
            throw new NotImplementedException("Node type not yet supported");
        }

        public override Statement VisitFinally(Finally Finally)
        {
            if (Finally == null) return null;
            WriteLine("finally");
            this.VisitBlock(Finally.Block);
            return Finally;
        }

        public override Statement VisitFor(For For)
        {
            WriteLine("for (");
            In();
            if (((StatementList)For.Initializer).Count > 0)
                this.VisitStatementList(For.Initializer);
            else
                WriteLine(";");
            WriteStart(string.Empty);
            this.VisitExpression(For.Condition);
            WriteFinish(";");
            this.processingForIncrementers = true;
            WriteStart(string.Empty);
            this.VisitStatementList(For.Incrementer);
            this.processingForIncrementers = false;
            Out();
            WriteLine(")");
            this.VisitBlock(For.Body);
            return For;
        }

        public override Statement VisitForEach(ForEach forEach)
        {
            WriteStart("foreach (");
            this.VisitTypeReference(forEach.TargetVariableType);
            Write(" ");
            this.VisitExpression(forEach.TargetVariable);
            Write(" in ");
            this.VisitExpression(forEach.SourceEnumerable);
            WriteFinish(")");
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
            if (keywordList.ContainsKey(identifier.Name))
                Write("@");

            Write(identifier.Name);
            return identifier;
        }

        public override Statement VisitIf(If If)
        {
            WriteStart("if (");
            this.VisitExpression(If.Condition);
            WriteFinish(")");
            this.VisitBlock(If.TrueBlock);
            if (If.FalseBlock != null)
            {
                WriteLine("else");
                this.VisitBlock(If.FalseBlock);
            }

            return If;
        }

        public override Expression VisitImplicitThis(ImplicitThis implicitThis)
        {
            throw new NotImplementedException("Node type not yet supported");
        }

        public override Expression VisitIndexer(Indexer indexer)
        {
            this.VisitExpression(indexer.Object);
            Write("[");
            this.VisitExpressionList(indexer.Operands);
            Write("]");
            return indexer;
        }

        public override Interface VisitInterface(Interface Interface)
        {
            this.VisitAttributeList(Interface.Attributes);

            WriteStart("{0}interface ", GetTypeQualifiers(Interface));
            this.VisitIdentifier(Interface.Name);
            WriteFinish(string.Empty);

            if (Interface.Interfaces != null && Interface.Interfaces.Count > 0)
            {
                In();

                WriteStart(": ");
                this.VisitInterfaceReferenceList(Interface.Interfaces);
                WriteFinish(string.Empty);

                Out();
            }

            WriteLine("{");
            In();

            this.VisitMemberList(Interface.Members);

            Out();
            WriteLine("}");

            return Interface;
        }

        public override Interface VisitInterfaceReference(Interface Interface)
        {
            if (Interface == null) return null;
            Write(Interface.Name.Name);
            return Interface;
        }

        public override InterfaceList VisitInterfaceReferenceList(InterfaceList interfaceReferences)
        {
            if (interfaceReferences == null) return null;
            for (int i = 0, n = interfaceReferences.Count; i < n; i++)
            {
                this.VisitTypeReference(interfaceReferences[i]);

                if (i < (n - 1))
                    Write(", ");
            }
            return interfaceReferences;
        }

        public override InstanceInitializer VisitInstanceInitializer(InstanceInitializer cons)
        {
            WriteStart("{0}", GetMethodQualifiers(cons));
            this.VisitIdentifier(cons.DeclaringType.Name);
            Write("(");
            In();
            cons.Parameters = this.VisitParameterList(cons.Parameters);
            Out();
            Write(")");

            // ISSUE: Is the constructor initializer always statement 1, following the field initializer?

            // Handle the constructor initializer
            if (cons.Body.Statements.Count >= 2)
            {
                for (int n = 0; n < 2; n++)
                {
                    if (cons.Body.Statements[n] is ExpressionStatement)
                    {
                        ExpressionStatement es = (ExpressionStatement)cons.Body.Statements[n];

                        if (es.Expression is MethodCall)
                        {
                            MethodCall mc = (MethodCall)es.Expression;

                            if (mc.Callee is QualifiedIdentifier)
                            {
                                QualifiedIdentifier qi = (QualifiedIdentifier)mc.Callee;

                                if (qi.Identifier.Name == ".ctor")
                                {
                                    // Bingo - this is the constructor initializer

                                    if (mc.Operands != null || !(qi.Qualifier is Base))
                                    {
                                        // We have a non-default initializer, so we must emit it
                                        Write(" : ");
                                        this.VisitExpression(qi.Qualifier);
                                        Write("(");
                                        this.VisitExpressionList(mc.Operands);
                                        Write(")");
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            WriteFinish(string.Empty);

            this.VisitBlock(cons.Body);

            //this.VisitTypeReferenceList(method.ImplementedTypes);
            //this.VisitExpressionList(method.Requires);
            //this.VisitExpressionList(method.Ensures);
            return cons;
        }

        public override Statement VisitLabeledStatement(LabeledStatement lStatement)
        {
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

        public override Expression VisitLocal(Local local)
        {
            throw new NotImplementedException("Node type not yet supported");
        }

        public override Expression VisitLRExpression(LRExpression expr)
        {
            throw new NotImplementedException("Node type not yet supported");
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
#if NOLINEBREAK
            Write("(");
#else
			WriteFinish("(");
			WriteStart(String.Empty);
#endif
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

            if (method.IsSpecialName)
                return method;

            WriteStart(GetMethodQualifiers(method));
            method.ReturnType = this.VisitTypeReference(method.ReturnType);
            // TODO
            //method.ImplementedTypes = this.VisitTypeReferenceList(method.ImplementedTypes);
            Write(" ");
            this.VisitIdentifier(method.Name);
            Write("(");
            this.VisitParameterList(method.Parameters);
            if (method.IsAbstract)
                WriteFinish(");");
            else
                WriteFinish(")");
            // TODO
            //method.Requires = this.VisitExpressionList(method.Requires);
            //method.Ensures = this.VisitExpressionList(method.Ensures);
            if (method.Body != null)
                this.VisitBlock(method.Body);
            return method;
        }

        public override Module VisitModule(Module module)
        {
            throw new NotImplementedException("Node type not yet supported");
        }

        public override ModuleReference VisitModuleReference(ModuleReference moduleReference)
        {
            throw new NotImplementedException("Node type not yet supported");
        }

        public override Expression VisitNameBinding(NameBinding nameBinding)
        {
            throw new NotImplementedException("Node type not yet supported");
        }

        public override Expression VisitNamedArgument(NamedArgument namedArgument)
        {
            throw new NotImplementedException("Node type not yet supported");
        }

        public override Namespace VisitNamespace(Namespace nspace)
        {
            if (nspace.Name.Name.Length > 0)
            {
                WriteLine("namespace {0}", nspace.Name.Name);
                WriteLine("{");
                In();
            }

            base.VisitNamespace(nspace);

            if (nspace.Name.Name.Length > 0)
            {
                Out();
                WriteLine("}");
            }

            return nspace;
        }

        public override Expression VisitParameter(Parameter parameter)
        {
            if (parameter == null) return null;
            if (parameter.Attributes != null && parameter.Attributes.Count == 1)
            {
                if (parameter.Attributes[0].Constructor is MemberBinding)
                {
                    MemberBinding mb = (MemberBinding)parameter.Attributes[0].Constructor;

                    if (mb.BoundMember.DeclaringType.Name.Name == "ParamArrayAttribute")
                    {
                        Write("params object[] ");
                        this.VisitIdentifier(parameter.Name);
                        return parameter;
                    }
                }
            }
            this.VisitAttributeList(parameter.Attributes);
            Write("{0}", GetParameterDirection(parameter.Flags));
            this.VisitTypeReference(parameter.Type);
            Write(" ");
            if (parameter.DefaultValue != null)
                throw new NotImplementedException("Unexpected parameter default value");
            this.VisitIdentifier(parameter.Name);
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

        public override Expression VisitPrefixExpression(PrefixExpression pExpr)
        {
            switch (pExpr.Operator)
            {
                case NodeType.Add:
                    Write("++");
                    break;

                case NodeType.Sub:
                    Write("--");
                    break;

                default:
                    throw new InvalidOperationException("Unknown prefix operator");
            }
            this.VisitExpression(pExpr.Expression);
            return pExpr;
        }

        public override Expression VisitPostfixExpression(PostfixExpression pExpr)
        {
            this.VisitExpression(pExpr.Expression);
            switch (pExpr.Operator)
            {
                case NodeType.Add:
                    Write("++");
                    break;

                case NodeType.Sub:
                    Write("--");
                    break;

                default:
                    throw new InvalidOperationException("Unknown postfix operator");
            }
            return pExpr;
        }

        public override Property VisitProperty(Property property)
        {
            if (property == null) return null;

            WriteStart(GetPropertyQualifiers(property));
            this.VisitTypeReference(property.Type);
            if (property.Parameters != null && property.Parameters.Count > 0)
            {
                //
                // Indexer
                //
                Write(" this[");
                this.VisitParameterList(property.Parameters);
                WriteFinish("]");
            }
            else
            {
                //
                // Normal property
                //
                Write(" ");
                this.VisitIdentifier(property.Name);
                WriteFinish(string.Empty);
            }

            WriteLine("{");
            In();
            if (property.Getter != null)
            {
                WriteLine("get");
                this.VisitBlock(property.Getter.Body);
            }
            if (property.Setter != null)
            {
                WriteLine("set");
                this.VisitBlock(property.Setter.Body);
            }
            Out();
            WriteLine("}");

            return property;
        }

        public override Expression VisitQualifiedIdentifier(QualifiedIdentifier qualifiedIdentifier)
        {
            this.VisitExpression(qualifiedIdentifier.Qualifier);
            Write(".");
            this.VisitIdentifier(qualifiedIdentifier.Identifier);
            return qualifiedIdentifier;
        }

        public override Statement VisitRepeat(Repeat repeat)
        {
            throw new NotImplementedException("Node type not yet supported");
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

        public override Expression VisitSetterValue(SetterValue value)
        {
            throw new NotImplementedException("Node type not yet supported");
        }

        public override StatementList VisitStatementList(StatementList statements)
        {
            if (statements == null)
            {
                if (!this.processingForIncrementers)
                    WriteLine(";");
                return null;
            }
            for (int i = 0, n = statements.Count; i < n; i++)
            {
                this.Visit(statements[i]);
                if (this.processingForIncrementers && i < (n - 1))
                    Write(",");
            }
            if (this.processingForIncrementers)
                WriteFinish(string.Empty);

            return statements;
        }

        public override StatementSnippet VisitStatementSnippet(StatementSnippet snippet)
        {
            throw new NotImplementedException("Node type not yet supported");
        }

        public override StaticInitializer VisitStaticInitializer(StaticInitializer cons)
        {
            WriteStart("static ");
            this.VisitIdentifier(cons.DeclaringType.Name);
            Write("(");
            In();
            this.VisitParameterList(cons.Parameters);
            Out();
            WriteFinish(")");
            this.VisitBlock(cons.Body);
            return cons;
        }

        public override Struct VisitStruct(Struct Struct)
        {
            this.VisitAttributeList(Struct.Attributes);

            WriteStart("{0}struct ", GetTypeQualifiers(Struct));
            this.VisitIdentifier(Struct.Name);
            WriteFinish(string.Empty);

            if (Struct.Interfaces != null && Struct.Interfaces.Count > 0)
                throw new NotImplementedException("Struct interfaces not yet supported");

            WriteLine("{");
            In();

            this.VisitMemberList(Struct.Members);

            Out();
            WriteLine("}");

            return Struct;
        }

        public override Statement VisitSwitch(System.Compiler.Switch Switch)
        {
            WriteStart("switch (");
            this.VisitExpression(Switch.Expression);
            WriteFinish(") {");
            In();
            this.VisitSwitchCaseList(Switch.Cases);
            Out();
            WriteLine("}");
            return Switch;
        }

        public override SwitchCase VisitSwitchCase(SwitchCase switchCase)
        {
            if (switchCase.Label != null)
            {
                WriteStart("case ");
                this.VisitExpression(switchCase.Label);
                WriteFinish(":");
            }
            else
                WriteLine("default:");
            this.VisitBlock(switchCase.Body);
            return switchCase;
        }

        public override Statement VisitSwitchInstruction(SwitchInstruction switchInstruction)
        {
            throw new NotImplementedException("Node type not yet supported");
        }

        public override Statement VisitTypeswitch(Typeswitch Typeswitch)
        {
            throw new NotImplementedException("Node type not yet supported");
        }

        public override TypeswitchCase VisitTypeswitchCase(TypeswitchCase typeswitchCase)
        {
            throw new NotImplementedException("Node type not yet supported");
        }

        public override TypeswitchCaseList VisitTypeswitchCaseList(TypeswitchCaseList typeswitchCases)
        {
            throw new NotImplementedException("Node type not yet supported");
        }

        public override Expression VisitTargetExpression(Expression expression)
        {
            throw new NotImplementedException("Node type not yet supported");
        }

        public override Expression VisitTernaryExpression(TernaryExpression expression)
        {
            Write("(");
            this.VisitExpression(expression.Operand1);
            Write(" ? ");
            this.VisitExpression(expression.Operand2);
            Write(" : ");
            this.VisitExpression(expression.Operand3);
            Write(")");
            return expression;
        }

        public override Expression VisitThis(This This)
        {
            Write("this");
            return This;
        }

        public override Statement VisitThrow(Throw Throw)
        {
            WriteStart("throw");
            if (Throw.Expression != null)
            {
                Write(" ");
                this.VisitExpression(Throw.Expression);
            }
            WriteFinish(";");
            return Throw;
        }

        public override Statement VisitTry(Try Try)
        {
            WriteLine("try");
            this.VisitBlock(Try.TryBlock);

            if (Try.Catchers != null)
            {
                for (int i = 0, n = Try.Catchers.Count; i < n; i++)
                    this.VisitCatch(Try.Catchers[i]);
            }
            if (Try.Filters != null)
            {
                for (int i = 0, n = Try.Filters.Count; i < n; i++)
                    this.VisitFilter(Try.Filters[i]);
            }
            if (Try.FaultHandlers != null)
            {
                for (int i = 0, n = Try.FaultHandlers.Count; i < n; i++)
                    this.VisitFaultHandler(Try.FaultHandlers[i]);
            }
            this.VisitFinally(Try.Finally);

            return Try;
        }

        public override TypeAlias VisitTypeAlias(TypeAlias tAlias)
        {
            throw new NotImplementedException("Node type not yet supported");
        }

        public override TypeMemberSnippet VisitTypeMemberSnippet(TypeMemberSnippet snippet)
        {
            throw new NotImplementedException("Node type not yet supported");
        }

        public override TypeModifier VisitTypeModifier(TypeModifier typeModifier)
        {
            throw new NotImplementedException("Node type not yet supported");
        }

        public override TypeNode VisitTypeNode(TypeNode typeNode)
        {
            throw new NotImplementedException("Node type not yet supported");
        }

        public override TypeNode VisitTypeParameter(TypeNode typeParameter)
        {
            throw new NotImplementedException("Node type not yet supported");
        }

        public override TypeNodeList VisitTypeParameterList(TypeNodeList typeParameters)
        {
            throw new NotImplementedException("Node type not yet supported");
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

            if (type.Name != null)
                nativeType = TranslateTypeName(type.Name.Name);

            TypeExpression te = type as TypeExpression;
            InterfaceExpression ie = type as InterfaceExpression;
            ArrayTypeExpression ate = type as ArrayTypeExpression;
            ReferenceTypeExpression rte = type as ReferenceTypeExpression;

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
                this.VisitIdentifier(type.Name);
            else
                throw new NotImplementedException("Unsupported type reference style");

            return type;
        }

        public override TypeNodeList VisitTypeReferenceList(TypeNodeList typeReferences)
        {
            throw new NotImplementedException("Node type not yet supported");
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
            throw new NotImplementedException("Node type not yet supported");
        }

        public override UsedNamespace VisitUsedNamespace(UsedNamespace usedNamespace)
        {
            WriteLine("using {0};", usedNamespace.Namespace.Name);
            return base.VisitUsedNamespace(usedNamespace);
        }

        public override Statement VisitWhile(While While)
        {
            WriteStart("while (");
            this.VisitExpression(While.Condition);
            WriteFinish(")");
            this.VisitBlock(While.Body);
            return While;
        }

        public override Statement VisitYield(Yield Yield)
        {
            throw new NotImplementedException("Node type not yet supported");
        }

        private static string GetTypeQualifiers(TypeNode typeNode)
        {
            string qualifiers = string.Empty;

            if (typeNode.IsPublic)
                qualifiers = "public ";
            else if (typeNode.IsPrivate)
                qualifiers = "private ";
            else if (typeNode.IsFamily)
                qualifiers = "protected ";
            else if (typeNode.IsAssembly || typeNode.IsNestedInternal)
                qualifiers = "internal ";
            else
                throw new ArgumentException("invalid access settings");

            if (typeNode.IsAbstract)
                qualifiers += "abstract ";

            if (typeNode.IsSealed && typeNode is Class)
                qualifiers += "sealed ";

            return qualifiers;
        }

        private static string GetPropertyQualifiers(Property property)
        {
            string qualifiers = string.Empty;

            if (property.IsPublic)
                qualifiers = "public ";
            else if (property.IsPrivate)
                qualifiers = "private ";
            else if (property.IsFamily)
                qualifiers = "protected ";
            else if (property.IsAssembly)
                qualifiers = "internal ";
            else
                throw new ArgumentException("invalid access settings");

            if (property.IsStatic)
                qualifiers += "static ";

            // TODO: what about abstract properties??
            //if (property.IsAbstract)
            //    qualifiers += "abstract ";

            // TODO: what about sealed properties??
            //if (property.IsFinal)
            //    qualifiers += "sealed ";

            if (property.OverridesBaseClassMember)
                qualifiers += "override ";
            else if (property.IsVirtual)
                qualifiers += "virtual ";

            if (property.HidesBaseClassMember)
                qualifiers += "new ";

            return qualifiers;
        }

        private static string GetMethodQualifiers(Method method)
        {
            string qualifiers = string.Empty;

            if (method.IsPublic)
                qualifiers = "public ";
            else if (method.IsPrivate)
                qualifiers = "private ";
            else if (method.IsFamily)
                qualifiers = "protected ";
            else if (method.IsAssembly)
                qualifiers = "internal ";
            else
                throw new ArgumentException("invalid access settings");

            if (method.IsStatic)
                qualifiers += "static ";

            if (method.IsAbstract)
                qualifiers += "abstract ";

            if (method.IsFinal)
                qualifiers += "sealed ";

            if (method.OverridesBaseClassMember)
                qualifiers += "override ";
            else if (method.IsVirtual)
                qualifiers += "virtual ";

            if (method.HidesBaseClassMember)
                qualifiers += "new ";

            return qualifiers;
        }

        private static string GetAttributeTarget(AttributeTargets targetFlags)
        {
            switch (targetFlags)
            {
                case AttributeTargets.Assembly: return "assembly: ";
                case AttributeTargets.Event: return "event: ";
                case AttributeTargets.Field: return "field: ";
                case AttributeTargets.Method: return "method: ";
                case AttributeTargets.Module: return "module: ";
                case AttributeTargets.Parameter: return "param: ";
                case AttributeTargets.Property: return "property: ";
                case AttributeTargets.ReturnValue: return "returnValue: ";
                default:
                case AttributeTargets.All: return string.Empty;
            }
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
            switch (op)
            {
                case NodeType.Add: return "+=";
                case NodeType.And: return "&=";
                case NodeType.Div: return "/=";
                case NodeType.Mul: return "*=";
                case NodeType.Or: return "|=";
                case NodeType.Rem: return "%=";
                case NodeType.Shl: return "<<=";
                case NodeType.Shr: return ">>=";
                case NodeType.Sub: return "-=";
                case NodeType.Xor: return "^=";
                case NodeType.Nop: return "=";
                default:
                    throw new ArgumentException("Invalid assignment operator type");
            }
        }

        private static string GetFieldQualifiers(Field field)
        {
            string qualifiers = string.Empty;

            if (field.IsPublic)
                qualifiers = "public ";
            else if (field.IsPrivate)
                qualifiers = "private ";
            else if (field.IsFamily)
                qualifiers = "protected ";
            else if (field.IsAssembly)
                qualifiers = "internal ";
            else
                throw new ArgumentException("invalid access settings");

            if (field.IsStatic)
                qualifiers += "static ";

            if (field.IsInitOnly)
                qualifiers += "readonly ";

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
                case NodeType.As: return "as";

                case NodeType.Div_Un:
                case NodeType.Div: return "/";
                case NodeType.Eq: return "==";
                case NodeType.Ge: return ">=";
                case NodeType.Gt: return ">";
                case NodeType.Is: return "is";
                case NodeType.Le: return "<=";
                case NodeType.LogicalAnd: return "&&";
                case NodeType.LogicalOr: return "||";
                case NodeType.Lt: return "<";

                case NodeType.Mul_Ovf:
                case NodeType.Mul_Ovf_Un:
                case NodeType.Mul: return "*";
                case NodeType.Ne: return "!=";
                case NodeType.Or: return "|";
                case NodeType.Shl: return "<<";

                case NodeType.Shr_Un:
                case NodeType.Shr: return ">>";
                case NodeType.Xor: return "^";

                case NodeType.Rem_Un:
                case NodeType.Rem: return "%";
                case NodeType.Sub:
                case NodeType.Sub_Ovf_Un:
                case NodeType.Sub_Ovf: return "-";

                // TODO: Finish this
                case NodeType.AddEventHandler:
                case NodeType.Box:
                case NodeType.Castclass:
                case NodeType.Ceq:
                case NodeType.Cgt:
                case NodeType.Cgt_Un:
                case NodeType.Clt:
                case NodeType.Clt_Un:
                case NodeType.Isinst:
                case NodeType.Ldvirtftn:
                case NodeType.Mkrefany:
                case NodeType.Refanyval:
                case NodeType.RemoveEventHandler:
                //case NodeType.Unaligned : // LJW: removed
                case NodeType.Unbox:
                default:
                    Debug.Assert(false, "binary operator not yet implemented");
                    throw new NotImplementedException("Binary operator not yet supported");
            }
        }

        private static string MakeCast(string s)
        {
            return "(" + s + ")";
        }

        [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        private static string GetUnaryOperator(NodeType nodeType, out bool isFunctionStyle)
        {
            isFunctionStyle = false;

            switch (nodeType)
            {
                case NodeType.AddressOf: return "&";
                case NodeType.Decrement: return "--";
                case NodeType.Increment: return "++";
                case NodeType.LogicalNot: return "!";
                case NodeType.Neg: return "-";
                case NodeType.Not: return "~";
                case NodeType.UnaryPlus: return "+";
                case NodeType.Sizeof: isFunctionStyle = true; return "sizeof";
                case NodeType.Typeof: isFunctionStyle = true; return "typeof";
                case NodeType.OutAddress: return "out ";
                case NodeType.RefAddress: return "ref ";
                case NodeType.Parentheses: isFunctionStyle = true; return string.Empty;

                case NodeType.Conv_Ovf_I1:
                case NodeType.Conv_Ovf_I1_Un:
                case NodeType.Conv_I1:
                    return MakeCast(TranslateTypeName(SystemTypes.Int8.Name.Name));

                case NodeType.Conv_Ovf_I2:
                case NodeType.Conv_Ovf_I2_Un:
                case NodeType.Conv_I2:
                    return MakeCast(TranslateTypeName(SystemTypes.Int16.Name.Name));

                case NodeType.Conv_Ovf_I4:
                case NodeType.Conv_Ovf_I4_Un:
                case NodeType.Conv_I4:
                    return MakeCast(TranslateTypeName(SystemTypes.Int32.Name.Name));

                case NodeType.Conv_Ovf_I8:
                case NodeType.Conv_Ovf_I8_Un:
                case NodeType.Conv_I8:
                    return MakeCast(TranslateTypeName(SystemTypes.Int64.Name.Name));

                case NodeType.Conv_Ovf_U1:
                case NodeType.Conv_Ovf_U1_Un:
                case NodeType.Conv_U1:
                    return MakeCast(TranslateTypeName(SystemTypes.UInt8.Name.Name));

                case NodeType.Conv_Ovf_U2:
                case NodeType.Conv_Ovf_U2_Un:
                case NodeType.Conv_U2:
                    return MakeCast(TranslateTypeName(SystemTypes.UInt16.Name.Name));

                case NodeType.Conv_Ovf_U4:
                case NodeType.Conv_Ovf_U4_Un:
                case NodeType.Conv_U4:
                    return MakeCast(TranslateTypeName(SystemTypes.UInt32.Name.Name));

                case NodeType.Conv_Ovf_U8:
                case NodeType.Conv_Ovf_U8_Un:
                case NodeType.Conv_U8:
                    return MakeCast(TranslateTypeName(SystemTypes.UInt64.Name.Name));

                case NodeType.Conv_R4:
                    return MakeCast(TranslateTypeName(SystemTypes.Single.Name.Name));

                case NodeType.Conv_R8:
                    return MakeCast(TranslateTypeName(SystemTypes.Double.Name.Name));

                // TODO: Finish this
                case NodeType.Ckfinite:
                case NodeType.Conv_I:
                case NodeType.Conv_Ovf_I:
                case NodeType.Conv_Ovf_I_Un:
                case NodeType.Conv_Ovf_U:
                case NodeType.Conv_Ovf_U_Un:
                case NodeType.Conv_R_Un:
                case NodeType.Conv_U:
                case NodeType.Ldftn:
                case NodeType.Ldlen:
                case NodeType.Ldtoken:
                case NodeType.Localloc:
                case NodeType.Refanytype:
                //case NodeType.Volatile : //LJW: remove
                default:
                    Debug.Assert(false, "Unary operator not yet implemented");
                    throw new NotImplementedException("Unary operator not yet supported");
            }
        }
    }
}