using System;
using System.CodeDom.Compiler;
using System.Compiler;
using System.Diagnostics;
using System.Globalization;

namespace Microsoft.Zing
{
    public sealed class ParserFactory : IParserFactory
    {
        public IParser CreateParser(string fileName, int lineNumber, DocumentText text, Module symbolTable, ErrorNodeList errorNodes, CompilerParameters options)
        {
            Document document = Compiler.CreateZingDocument(fileName, lineNumber, text);
            return new Parser(document, errorNodes, symbolTable, options as ZingCompilerOptions);
        }
    }

    public sealed class Parser : System.Compiler.IParser
    {
        private Token currentToken;
        private Scanner scanner;
        private ErrorNodeList errors;
        private AuthoringSink sink;
        private Module module;
        private SourceContext compoundStatementOpeningContext;
        private bool omitBodies;
        private bool checkArithmetic;

        private static Guid dummyGuid = new Guid();

        public Parser()
        {
        }

        public Parser(Module symbolTable)
        {
            this.module = symbolTable;
        }

        public Parser(Document document, ErrorNodeList errors)
        {
            this.scanner = new Scanner(document, errors, null);
            this.errors = errors;
        }

        public Parser(Document document, ErrorNodeList errors, Module symbolTable, ZingCompilerOptions options)
        {
            this.scanner = new Scanner(document, errors, options);
            this.errors = errors;
            this.module = symbolTable;
            this.checkArithmetic = options.CheckArithmetic;
        }

        private void GetNextToken()
        {
            this.currentToken = this.scanner.GetNextToken();
        }

        private Token GetTokenFor(string terminator)
        {
            Guid dummy = Parser.dummyGuid;
            Document document = new Document(null, 1, terminator, dummy, dummy, dummy);
            Scanner scanner = new Scanner(document, new ErrorNodeList(), null);
            return scanner.GetNextToken();
        }

        public CompilationUnit ParseCompilationUnit(string source, string fname, CompilerParameters parameters, ErrorNodeList errors, AuthoringSink sink, bool noNameMangling)
        {
            Guid dummy = Parser.dummyGuid;
            Document document = new Document(fname, 1, source, dummy, dummy, dummy);
            this.errors = errors;
            this.scanner = new Scanner(document, errors, parameters as ZingCompilerOptions);
            this.currentToken = Token.None;
            this.sink = sink;
            this.errors = errors;
            CompilationUnit cu = new CompilationUnit(Identifier.For(fname));
            try
            {
                this.ParseCompilationUnit(cu, false, noNameMangling, sink);
            }
            catch (Exception e)
            {
                if (System.Diagnostics.Debugger.IsAttached)
                    System.Diagnostics.Debugger.Break();

                System.Diagnostics.Debug.WriteLine(e);

                return null;
            }
            finally
            {
                this.errors = null;
                this.scanner = null;
                this.sink = null;
            }

            return cu;
        }

        public void ParseCompilationUnit(CompilationUnit cu)
        {
            this.ParseCompilationUnit(cu, false, false, null);
        }

        // Method added by Jiri Adamek to handle ZOM classes
        public void LoadZOMAssembly(string zomAssemblyName, Namespace ns)
        {
            // Loads an assembly using its file name.
            AssemblyNode assembly = AssemblyNode.GetAssembly(zomAssemblyName);

            if (assembly == null)
            {
                Console.WriteLine("Warning: cannot open " + zomAssemblyName + " assembly");
                return;
            }

            // Create a new assembly
            AssemblyNode newAssembly = new AssemblyNode();

            // Gets the type names from the loaded assembly and puts all public classes of the
            // "Microsoft.Zing" namespace into the "" namespace of the new assembly;
            // the name duplicities of the new assembly are not tested, as the new assembly is empty
            // at the beginning and in the set of the added types there can be no name duplicities
            // (as the names are taken from the same namespace of an existing assembly)

            foreach (Namespace assembly_namespace in assembly.GetNamespaceList())
            {
                if (assembly_namespace.Name.ToString().Equals("Microsoft.Zing"))
                {
                    foreach (TypeNode tn in assembly_namespace.Types)
                    {
                        if (tn.IsPublic)
                        {
                            Class assembly_class = tn as Class;
                            if (assembly_class != null)
                            {
                                TypeNode zom_class;

                                zom_class = new NativeZOM();
                                zom_class.Attributes = null;
                                zom_class.SourceContext = new SourceContext();
                                zom_class.Flags = TypeFlags.Public;
                                zom_class.DeclaringModule = assembly;
                                zom_class.Name = new Identifier(assembly_class.Name.ToString());
                                zom_class.Namespace = Identifier.For(ns.FullName);

                                foreach (Member member in assembly_class.Members)
                                {
                                    Method assembly_method = member as Method;
                                    if (assembly_method != null)
                                    {
                                        /* Skip constructors and static initializers*/

                                        if (assembly_method is InstanceInitializer ||
                                            assembly_method is StaticInitializer)
                                            continue;

                                        /* Skip the "system" methods of the ZOM class, i.e. the methods
                                         * that are used by the Zing runtime.
                                         */

                                        if (assembly_method.Name.Name.Equals("WriteString") ||
                                            assembly_method.Name.Name.Equals("TraverseFields") ||
                                            assembly_method.Name.Name.Equals("Clone") ||
                                            assembly_method.Name.Name.Equals("GetValue") ||
                                            assembly_method.Name.Name.Equals("SetValue") ||
                                            assembly_method.Name.Name.Equals("get_TypeId") ||
                                            assembly_method.Name.Name.Equals("ToXml"))
                                            continue;

                                        /* Skip non-public methods */
                                        if (!assembly_method.IsPublic)
                                        {
                                            Console.WriteLine("Warning: the " + assembly_method.FullName + " method is not public ==> method skipped");
                                            continue;
                                        }

                                        /* Handle the parameters:
                                         * - the first parameter has to be of the type Microsoft.Zing.Process
                                         * - the first parameter is "hidden" from the Zing programmer (the reference
                                         *   to the current process object is passed automatically by the Zing compiler)
                                         */

                                        if (assembly_method.Parameters.Count < 1 ||
                                            (!assembly_method.Parameters[0].Type.FullName.Equals("Microsoft.Zing.Process")))
                                        {
                                            Console.WriteLine("Warning: the " + assembly_method.FullName + " method does not have the first parameter of the type Microsoft.Zing.Process ==> method skipped");
                                            continue;
                                        }
                                        ParameterList parameters = new ParameterList();
                                        for (int i = 1; i < assembly_method.Parameters.Count; i++)
                                        {
                                            // The Zing "object" type has to be handled in a special way;
                                            // its "mirror in the .NET world" has the type Microsoft.Zing.Pointer

                                            if (assembly_method.Parameters[i].Type.FullName.Equals("Microsoft.Zing.Pointer"))
                                            {
                                                Parameter par = new Parameter(
                                                    new Identifier(assembly_method.Parameters[i].Name.ToString()),
                                                    SystemTypes.Object
                                                );
                                                parameters.Add(par);
                                            }
                                            else
                                            {
                                                parameters.Add(assembly_method.Parameters[i]);
                                            }
                                        }

                                        // The Zing "object" type has to be handled in a special way;
                                        // its "mirror in the .NET world" has the type Microsoft.Zing.Pointer

                                        TypeNode ret;
                                        if (assembly_method.ReturnType.FullName.Equals("Microsoft.Zing.Pointer"))
                                        {
                                            ret = SystemTypes.Object;
                                        }
                                        else
                                        {
                                            ret = assembly_method.ReturnType;
                                        }

                                        ZMethod zom_method = new ZMethod(
                                            zom_class,
                                            assembly_method.Attributes,
                                            assembly_method.Name,
                                            parameters,
                                            ret,
                                            assembly_method.Body
                                            );
                                        zom_method.ThisParameter.Type = zom_class;
                                        if (assembly_method.IsStatic)
                                            zom_method.Flags = zom_method.Flags | MethodFlags.Static;
                                        zom_class.Members.Add(zom_method);
                                    }
                                }

                                //if (this.AddTypeToModule(zom_class))
                                //    ns.Types.Add(zom_class);
                                newAssembly.Types.Add(zom_class);
                            }
                        }
                    }
                    break;
                }
            }

            // Add the new assembly to the list of referenced assemblies of the current module
            this.module.AssemblyReferences.Add(new AssemblyReference(newAssembly));
        }

        public void ParseCompilationUnit(CompilationUnit cu, bool omitBodies, bool noNameMangling, AuthoringSink sink)
        {
            Namespace ns = new Namespace(Identifier.Empty, Identifier.Empty, new AliasDefinitionList(), new UsedNamespaceList(), new NamespaceList(), new TypeNodeList());

            cu.Nodes = new NodeList(ns);
            this.omitBodies = omitBodies;
            this.scanner.disableNameMangling = noNameMangling;
            this.GetNextToken();
            cu.PreprocessorDefinedSymbols = this.scanner.PreprocessorDefinedSymbols;

            // Code added by Jiri Adamek to handle ZOM classes

            // Take the Zing compiler options
            ZingCompilerOptions zoc = cu.Compilation.CompilerParameters as ZingCompilerOptions;

            // Load a ZOM assembly if specified on the command line
            if (zoc != null && zoc.ZomAssemblyName != null) LoadZOMAssembly(zoc.ZomAssemblyName, ns);

            // END of added code

            this.ParseTypeDeclarations(ns, Parser.EndOfFile | Parser.TypeDeclarationStart);
        }

        Expression IParser.ParseExpression()
        {
            return this.ParseExpression(0, null, null);
        }

        public Expression ParseExpression(int startColumn, string terminator, AuthoringSink sink)
        {
            if (Debugger.IsAttached)
                Debugger.Break();

            this.sink = sink;
            TokenSet followers = Parser.EndOfFile;
            if (terminator != null)
                followers |= this.GetTokenFor(terminator);
            this.scanner.endPos = startColumn;
            return this.ParseExpression(followers);
        }

        public void ParseMethodBody(Method method)
        {
            if (Debugger.IsAttached)
                Debugger.Break();
            this.GetNextToken();
            method.Body.Statements = new StatementList();
            this.ParseStatements(method.Body.Statements, Parser.EndOfFile);
        }

        void IParser.ParseStatements(StatementList statements)
        {
            this.ParseStatements(statements, 0, null, null);
        }

        public int ParseStatements(StatementList statements, int startColumn, string terminator, AuthoringSink sink)
        {
            if (Debugger.IsAttached)
                Debugger.Break();
            this.sink = sink;
            TokenSet followers = Parser.EndOfFile;
            if (terminator != null)
                followers |= this.GetTokenFor(terminator);
            this.scanner.endPos = startColumn;
            this.GetNextToken();
            this.ParseStatements(statements, followers);
            return this.scanner.CurrentSourceContext.StartPos;
        }

        void IParser.ParseTypeMembers(TypeNode type)
        {
            this.ParseTypeMembers(type, 0, null, null);
        }

        public int ParseTypeMembers(TypeNode type, int startColumn, string terminator, AuthoringSink sink)
        {
            if (Debugger.IsAttached)
                Debugger.Break();

            this.sink = sink;
            TokenSet followers = Parser.EndOfFile;
            if (terminator != null)
                followers |= this.GetTokenFor(terminator);
            this.scanner.endPos = startColumn;
            this.currentToken = Token.None;
            this.GetNextToken();
            this.ParseTypeMembers(type, followers);
            return this.scanner.CurrentSourceContext.StartPos;
        }

        private void ParseTypeDeclarations(Namespace ns, TokenSet followers)
        {
            for (; ; )
            {
                AttributeList attributes = null; // TODO: this.ParseAttributes(ns, followers|Parser.AttributeOrTypeDeclarationStart);
                SourceContext sctx = this.scanner.CurrentSourceContext;
                Token tok = this.currentToken;
                //TypeFlags flags = this.ParseTypeModifiers();
                switch (this.currentToken)
                {
                    case Token.Interface:
                    case Token.Class:
                    case Token.Struct: this.ParseTypeDeclaration(ns, attributes, sctx, followers); break;
                    case Token.Enum: this.ParseEnumDeclaration(ns, attributes, sctx, followers); break;
                    case Token.Range: this.ParseRangeDeclaration(ns, attributes, sctx, followers); break;
                    case Token.Chan: this.ParseChanDeclaration(ns, attributes, sctx, followers); break;
                    case Token.Set: this.ParseSetDeclaration(ns, attributes, sctx, followers); break;
                    case Token.Array: this.ParseArrayDeclaration(ns, attributes, sctx, followers); break;
                    default:
                        if (!followers[this.currentToken])
                            this.SkipTo(followers /*, Error.BadTokenInType */);
                        return;
                }
            }
        }

        private bool inInterface;

        private void ParseTypeDeclaration(Namespace ns, AttributeList attributes, SourceContext sctx, TokenSet followers)
        {
            TypeNode t;

            Debug.Assert(this.currentToken == Token.Interface || this.currentToken == Token.Class || this.currentToken == Token.Struct);
            if (this.currentToken == Token.Interface)
                t = (TypeNode)new Interface();
            else if (this.currentToken == Token.Class)
                t = (TypeNode)new Class();
            else
                t = (TypeNode)new Struct();

            this.GetNextToken();
            t.Attributes = attributes;
            t.SourceContext = sctx;
            t.Flags |= TypeFlags.Public;
            t.DeclaringModule = this.module;
            t.Name = this.scanner.GetIdentifier();
            t.Namespace = Identifier.For(ns.FullName);
            if (this.AddTypeToModule(t))
                ns.Types.Add(t);

            this.Skip(Token.Identifier);

            // process list of interfaces
            if (this.currentToken == Token.Colon)
            {
                if (t is Interface)
                {
                    // An interface may not extend anything else
                    this.HandleError(Error.UnexpectedToken, this.scanner.GetTokenSource());
                }
                else
                {
                    this.Skip(Token.Colon);
                    t.Interfaces = new InterfaceList();
                    while (true)
                    {
                        if (this.currentToken != Token.Identifier)
                        {
                            this.HandleError(Error.ExpectedIdentifier);
                            break;
                        }
                        Identifier id = this.scanner.GetIdentifier();
                        t.Interfaces.Add(new InterfaceExpression(id));
                        this.Skip(Token.Identifier);
                        if (this.currentToken != Token.Comma)
                            break;
                        this.Skip(Token.Comma);
                    }
                }
            }

            SourceContext blockContext = this.scanner.CurrentSourceContext;
            this.Skip(Token.LeftBrace);

            this.inInterface = (t is Interface);
            this.ParseTypeMembers(t, followers | Token.RightBrace);
            this.inInterface = false;

            int endCol = this.scanner.CurrentSourceContext.StartPos;
            this.ParseBracket(blockContext, Token.RightBrace, followers | Token.Semicolon, Error.ExpectedRightBrace);
            t.SourceContext.EndPos = endCol;
            this.Skip(Token.Semicolon);
            this.SkipTo(followers);
        }

        private static Identifier assemblyId = Identifier.For("assembly");
        private static Identifier eventId = Identifier.For("event");
        private static Identifier fieldId = Identifier.For("field");
        private static Identifier methodId = Identifier.For("method");
        private static Identifier moduleId = Identifier.For("module");
        private static Identifier paramId = Identifier.For("param");
        private static Identifier propertyId = Identifier.For("property");
        private static Identifier returnId = Identifier.For("return");
        private static Identifier typeId = Identifier.For("type");

        private AttributeList ParseAttributes(TokenSet followers)
        {
            if (this.currentToken != Token.LeftBracket) return null;
            AttributeList attributes = new AttributeList();
            // Eliminate the name manging for attributes since they're compiled from C#
            bool savedManglingStatus = this.scanner.disableNameMangling; ;
            this.scanner.disableNameMangling = true;
            while (this.currentToken == Token.LeftBracket)
            {
                SourceContext sctx = this.scanner.CurrentSourceContext;
                this.GetNextToken();

                Identifier id = Identifier.For(this.scanner.GetIdentifierString());
                id.SourceContext = this.scanner.CurrentSourceContext;

                this.GetNextToken();
                SourceContext attrCtx = this.scanner.CurrentSourceContext;
                for (; ; )
                {
                    AttributeNode attr = new AttributeNode();
                    attr.SourceContext = attrCtx;
                    if (this.currentToken == Token.Dot)
                        attr.Constructor = this.ParseQualifiedIdentifier(id, followers | Token.Comma | Token.LeftParenthesis | Token.RightBracket);
                    else
                    {
                        attr.Constructor = new QualifiedIdentifier(
                            new QualifiedIdentifier(
                                new QualifiedIdentifier(Identifier.For("Microsoft"), Identifier.For("Zing")),
                                Identifier.For("Attributes")),
                            id, id.SourceContext);
                    }

                    // Need name mangling on again for the arguments
                    // this.scanner.disableNameMangling = false;
                    this.scanner.disableNameMangling = savedManglingStatus;
                    this.ParseAttributeArguments(attr, followers | Token.Comma | Token.RightBracket);
                    this.scanner.disableNameMangling = true;

                    attr.SourceContext.EndPos = this.scanner.endPos;
                    attributes.Add(attr);
                    if (this.currentToken != Token.Comma) break;
                    this.GetNextToken();
                    if (this.currentToken == Token.RightBracket) break;
                    id = this.scanner.GetIdentifier();
                    this.Skip(Token.Identifier);
                }
                this.ParseBracket(sctx, Token.RightBracket, followers, Error.ExpectedRightBracket);
            }
            this.scanner.disableNameMangling = savedManglingStatus;
            this.SkipTo(followers);
            return attributes;
        }

        private void ParseAttributeArguments(AttributeNode attr, TokenSet followers)
        {
            if (this.currentToken != Token.LeftParenthesis) return;
            SourceContext sctx = this.scanner.CurrentSourceContext;
            this.GetNextToken();
            ExpressionList expressions = attr.Expressions = new ExpressionList();
            bool hadNamedArgument = false;
            while (this.currentToken != Token.RightParenthesis)
            {
                Expression expr = this.ParseExpression(followers | Token.Comma | Token.RightParenthesis);
                AssignmentExpression aExpr = expr as AssignmentExpression;
                if (aExpr != null)
                {
                    AssignmentStatement aStat = (AssignmentStatement)aExpr.AssignmentStatement;
                    Identifier id = aStat.Target as Identifier;
                    if (id == null)
                    {
                        this.HandleError(aStat.Target.SourceContext, Error.ExpectedIdentifier);
                        expr = null;
                    }
                    else
                        expr = new NamedArgument(id, aStat.Source, expr.SourceContext);
                    hadNamedArgument = true;
                }
                else if (hadNamedArgument)
                    this.HandleError(expr.SourceContext, Error.NamedArgumentExpected);
                expressions.Add(expr);
                if (this.currentToken != Token.Comma) break;
                this.GetNextToken();
            }
            this.ParseBracket(sctx, Token.RightParenthesis, followers, Error.ExpectedRightParenthesis);
        }

        private void ParseModifiers(TokenList modifierTokens, SourceContextList modifierContexts)
        {
            for (; ; )
            {
                switch (this.currentToken)
                {
                    case Token.Static:
                    case Token.Atomic:
                    case Token.Activate:
                        modifierTokens.Add(this.currentToken);
                        modifierContexts.Add(this.scanner.CurrentSourceContext);
                        break;

                    default:
                        return;
                }
                this.GetNextToken();
            }
        }

        private void ParseTypeMembers(TypeNode t, TokenSet followers)
        {
            TokenSet startMemberDecl;
            if (t is Interface)
                startMemberDecl = Parser.InterfaceMemberDeclarationStart;
            else if (t is Struct)
                startMemberDecl = Parser.StructMemberDeclarationStart;
            else
                startMemberDecl = Parser.ClassMemberDeclarationStart;

            for (; ; )
            {
                bool isVoid = false;

                TokenList modifierTokens = new TokenList();
                SourceContextList modifierContexts = new SourceContextList();
                this.ParseModifiers(modifierTokens, modifierContexts);
                SourceContext sctx = this.scanner.CurrentSourceContext;
                switch (this.currentToken)
                {
                    case Token.Void:
                        isVoid = true;
                        goto case Token.Identifier;

                    case Token.Bool:
                    case Token.Byte:
                    case Token.SByte:
                    case Token.Short:
                    case Token.UShort:
                    case Token.Int:
                    case Token.UInt:
                    case Token.Long:
                    case Token.ULong:
                    case Token.Float:
                    case Token.Double:
                    case Token.Decimal:
                    case Token.Object:
                    case Token.Identifier:
                    case Token.String:
                        TypeNode memberType = this.ParseBaseTypeExpression(followers | Token.Identifier | Token.Semicolon, true, false);
                        Identifier memberName = this.scanner.GetIdentifier();

                        if (this.currentToken != Token.Identifier)
                        {
                            this.SkipTo(followers | startMemberDecl, Error.ExpectedIdentifier);
                            break;
                        }

                        memberName = this.scanner.GetIdentifier();
                        this.Skip(Token.Identifier);

                        switch (this.currentToken)
                        {
                            case Token.Assign:
                            case Token.Semicolon:
                                if (isVoid)
                                {
                                    this.HandleError(sctx, Error.NoVoidField);
                                    this.SkipTo(followers | Token.Semicolon);
                                    if (this.currentToken == Token.Semicolon)
                                        this.SkipSemiColon(followers);
                                    break;
                                }

                                this.ParseField(t, sctx, modifierTokens, modifierContexts,
                                    memberType, memberName, followers | startMemberDecl);
                                break;

                            case Token.LeftParenthesis: // method
                                this.ParseMethod(t, sctx, modifierTokens, modifierContexts,
                                    memberType, memberName, followers | startMemberDecl);
                                break;

                            default:                // invalid class member
                                // Not sure what's going on - assume a missing semi-colon
                                this.SkipTo(followers | startMemberDecl, Error.ExpectedSemicolon);
                                break;
                        }

                        this.SkipTo(followers | startMemberDecl);
                        break;

                    default:
                        if (!followers[this.currentToken])
                            this.SkipTo(followers);
                        return;
                }
            }
        }

        private void ParseField(TypeNode t, SourceContext sctx,
            TokenList modifierTokens, SourceContextList modifierContexts,
            TypeNode fieldType, Identifier fieldName, TokenSet followers)
        {
            Expression initializer = null;

            if (this.currentToken == Token.Assign)
            {
                if (t is Class)
                {
                    this.GetNextToken();
                    initializer = this.ParseExpression(followers | Token.Semicolon);
                }
                else
                {
                    this.SkipTo(followers | Token.Semicolon, Error.InvalidStructFieldInitializer);
                }
            }

            sctx.EndPos = this.scanner.endPos;
            Field f = new Field();
            f.DeclaringType = t;
            f.Name = fieldName;
            f.Type = fieldType;
            f.Initializer = initializer;
            f.SourceContext = sctx;
            f.Flags = FieldFlags.Public;
            this.SetFieldFlags(modifierTokens, modifierContexts, f);

            t.Members.Add(f);
            this.Skip(Token.Semicolon);

            if (!followers[this.currentToken])
                this.SkipTo(followers);
        }

        private void ParseMethod(TypeNode parentType, SourceContext sctx,
            TokenList modifierTokens, SourceContextList modifierContexts,
            TypeNode type, Identifier name, TokenSet followers)
        {
            ZMethod m = new ZMethod(parentType, null /* attributes */, name, null, type, null);
            parentType.Members.Add(m);
            m.Flags = this.GetMethodFlags(modifierTokens, modifierContexts, parentType, m);
            if ((m.Flags & MethodFlags.Static) == 0)
                m.CallingConvention = CallingConventionFlags.HasThis;
            if (this.inInterface)
            {
                m.Flags |= MethodFlags.Abstract;
                m.Flags |= MethodFlags.Virtual;
                m.Parameters = this.ParseParameters(followers | Token.Semicolon);
                this.Skip(Token.Semicolon);
            }
            else
            {
                m.Parameters = this.ParseParameters(followers | Token.LeftBrace);
                m.Body = this.ParseBody(m, sctx, followers);
            }
            m.HasCompilerGeneratedSignature = false;
        }

        private ParameterList ParseParameters(TokenSet followers)
        {
            SourceContext sctx = this.scanner.CurrentSourceContext;

            if (this.currentToken != Token.LeftParenthesis)
                this.SkipTo(followers, Error.SyntaxError, "(");
            if (this.currentToken == Token.LeftParenthesis)
                this.GetNextToken();

            ParameterList result = new ParameterList();
            if (this.currentToken != Token.RightParenthesis)
            {
                TokenSet followersOrCommaOrRightParenthesis = followers | Token.Comma | Token.RightParenthesis;
                for (; ; )
                {
                    Parameter p = this.ParseParameter(followersOrCommaOrRightParenthesis, false);
                    result.Add(p);
                    if (this.currentToken == Token.Comma)
                        this.GetNextToken();
                    else
                        break;
                }
            }
            this.ParseBracket(sctx, Token.RightParenthesis, followers, Error.ExpectedRightParenthesis);
            return result;
        }

        private Parameter ParseParameter(TokenSet followers, bool allowRefParameters)
        {
            Parameter p = new Parameter();
            p.SourceContext = this.scanner.CurrentSourceContext;
            p.Attributes = null; // TODO: this.ParseAttributes(null, followers|Parser.ParameterTypeStart);
            p.Flags = ParameterFlags.None;
            bool byRef = false;
            if (this.currentToken == Token.Out)
            {
                p.Flags = ParameterFlags.Out;
                byRef = true;
                this.GetNextToken();
            }

            bool voidParam = false;
            if (this.currentToken == Token.Void)
            {
                this.HandleError(Error.NoVoidParameter);
                p.Type = SystemTypes.Object;
                this.GetNextToken();
                voidParam = true;
            }
            else
                p.Type = this.ParseBaseTypeExpression(followers | Token.Identifier, false, false);

            if (byRef) p.Type = new ReferenceTypeExpression(p.Type);
            p.Name = this.scanner.GetIdentifier();
            p.SourceContext.EndPos = this.scanner.endPos;
            if (!voidParam || this.currentToken == Token.Identifier)
                this.Skip(Token.Identifier);

            this.SkipTo(followers);
            return p;
        }

        private Block ParseBody(Method m, object sctx, TokenSet followers)
        {
            m.SourceContext = (SourceContext)sctx;
            m.SourceContext.EndPos = this.scanner.endPos;
            Block b = null;
            if (this.currentToken == Token.LeftBrace)
            {
                b = this.ParseBlock(sctx, followers);
                if (b != null)
                    m.SourceContext.EndPos = b.SourceContext.EndPos + 1;
                this.SkipTo(followers);
            }
            else
                this.SkipTo(followers, Error.ExpectedLeftBrace);
            if (this.omitBodies)
                b = null;
            return b;
        }

        private Block ParseBlock(TokenSet followers)
        {
            Block b = null;
            if (this.currentToken == Token.LeftBrace)
            {
                b = this.ParseBlock(this.scanner.CurrentSourceContext, followers);
                this.SkipTo(followers);
            }
            else
                this.SkipTo(followers, Error.ExpectedLeftBrace);
            return b;
        }

        private Block ParseBlock(object sctx, TokenSet followers)
        {
            Block block = new Block();
            block.Checked = checkArithmetic;
            // block.Checked = false;
            SourceContext ctx = (SourceContext)sctx;
            ctx.EndPos = this.scanner.CurrentSourceContext.EndPos;
            Debug.Assert(this.currentToken == Token.LeftBrace);
            SourceContext blockStart = this.scanner.CurrentSourceContext;
            this.GetNextToken();
            block.SourceContext = this.scanner.CurrentSourceContext;
            block.Statements = this.ParseStatements(followers | Token.RightBrace);
            block.SourceContext.EndPos = this.scanner.CurrentSourceContext.StartPos;
            this.ParseBracket(blockStart, Token.RightBrace, followers, Error.ExpectedRightBrace);
            return block;
        }

        private Block ParseAtomicBlock(TokenSet followers)
        {
            AtomicBlock block = new AtomicBlock();
            block.Checked = checkArithmetic;
            block.SourceContext = this.scanner.CurrentSourceContext;
            Debug.Assert(this.currentToken == Token.Atomic);
            this.GetNextToken();

            if (this.currentToken == Token.LeftBrace)
            {
                SourceContext ctx = this.scanner.CurrentSourceContext;
                ctx.EndPos = this.scanner.CurrentSourceContext.EndPos;
                this.GetNextToken();
                block.Statements = this.ParseStatements(followers | Token.RightBrace);
                block.SourceContext.EndPos = this.scanner.CurrentSourceContext.StartPos;
                this.ParseBracket(ctx, Token.RightBrace, followers, Error.ExpectedRightBrace);
                this.SkipTo(followers);
            }
            else
                this.SkipTo(followers, Error.ExpectedLeftBrace);
            return block;
        }

        private Statement ParseAsyncCall(TokenSet followers)
        {
            AsyncMethodCall a = new AsyncMethodCall();
            a.SourceContext = this.scanner.CurrentSourceContext;

            Debug.Assert(this.currentToken == Token.Async);
            this.GetNextToken();

            a.Expression = this.ParseExpression(followers | Token.Semicolon);
            a.SourceContext.EndPos = this.scanner.CurrentSourceContext.StartPos;

            if (a.Expression.NodeType != NodeType.MethodCall)
            {
                this.HandleError(a.Expression.SourceContext, Error.ExpectedMethodCall);
                a.Expression = null;
            }

            this.SkipSemiColon(followers);
            this.SkipTo(followers);
            return a;
        }

        private Statement ParseAssert(TokenSet followers)
        {
            AssertStatement a = new AssertStatement();
            a.SourceContext = this.scanner.CurrentSourceContext;

            Debug.Assert(this.currentToken == Token.Assert);
            this.GetNextToken();

            SourceContext argContext = this.scanner.CurrentSourceContext;
            this.Skip(Token.LeftParenthesis);

            a.booleanExpr = this.ParseExpression(followers | Token.Comma | Token.RightParenthesis);

            switch (this.currentToken)
            {
                case Token.Comma:
                    this.GetNextToken();

                    a.Comment = this.scanner.GetString();
                    this.Skip(Token.StringLiteral);
                    goto case Token.RightParenthesis;

                case Token.RightParenthesis:
                    this.ParseBracket(argContext, Token.RightParenthesis, followers | Token.RightParenthesis, Error.ExpectedRightParenthesis);
                    break;

                default:
                    this.HandleError(Error.ExpectedCommaOrRightParen);
                    this.SkipTo(followers);
                    return null;
            }

            a.SourceContext.EndPos = this.scanner.CurrentSourceContext.StartPos;
            this.SkipSemiColon(followers);
            this.SkipTo(followers);
            return a;
        }

        private Statement ParseAccept(TokenSet followers)
        {
            AcceptStatement a = new AcceptStatement();
            a.SourceContext = this.scanner.CurrentSourceContext;

            Debug.Assert(this.currentToken == Token.Accept);
            this.GetNextToken();

            SourceContext argContext = this.scanner.CurrentSourceContext;
            this.Skip(Token.LeftParenthesis);

            a.booleanExpr = this.ParseExpression(followers | Token.Comma | Token.RightParenthesis);

            switch (this.currentToken)
            {
                case Token.RightParenthesis:
                    this.ParseBracket(argContext, Token.RightParenthesis, followers | Token.RightParenthesis, Error.ExpectedRightParenthesis);
                    break;

                default:
                    this.HandleError(Error.ExpectedCommaOrRightParen);
                    this.SkipTo(followers);
                    return null;
            }

            a.SourceContext.EndPos = this.scanner.CurrentSourceContext.StartPos;
            this.SkipSemiColon(followers);
            this.SkipTo(followers);
            return a;
        }

        private Statement ParseEvent(TokenSet followers)
        {
            EventStatement e = new EventStatement();
            e.SourceContext = this.scanner.CurrentSourceContext;

            Debug.Assert(this.currentToken == Token.Event);
            this.GetNextToken();
            SourceContext argContext = this.scanner.CurrentSourceContext;
            this.Skip(Token.LeftParenthesis);
            e.channelNumber = this.ParseExpression(followers | Token.Comma);
            this.Skip(Token.Comma);
            e.messageType = this.ParseExpression(followers | Token.Comma);
            this.Skip(Token.Comma);
            e.direction = this.ParseExpression(followers | Token.RightParenthesis);
            this.ParseBracket(argContext, Token.RightParenthesis, followers | Token.Semicolon, Error.ExpectedRightParenthesis);
            e.SourceContext.EndPos = this.scanner.CurrentSourceContext.StartPos;
            this.SkipSemiColon(followers);
            this.SkipTo(followers);
            return e;
        }

        private Statement ParseTrace(TokenSet followers)
        {
            TraceStatement t = new TraceStatement();
            t.SourceContext = this.scanner.CurrentSourceContext;

            Debug.Assert(this.currentToken == Token.Trace);
            this.GetNextToken();

            SourceContext parenStart = this.scanner.CurrentSourceContext;

            if (this.sink != null)
                this.sink.StartParameters(this.scanner.CurrentSourceContext);

            this.Skip(Token.LeftParenthesis);

            int endCol;
            t.Operands = this.ParseArgumentList(followers, out endCol);
            SourceContext parenEnd = this.scanner.CurrentSourceContext;
            parenEnd.StartPos = endCol - 1;
            parenEnd.EndPos = endCol;
            if (this.sink != null)
                this.sink.MatchPair(parenStart, parenEnd);

            t.SourceContext.EndPos = this.scanner.CurrentSourceContext.StartPos;
            this.SkipSemiColon(followers);
            this.SkipTo(followers);
            return t;
        }

        private Statement ParseInvokePlugin(TokenSet followers)
        {
            InvokePluginStatement t = new InvokePluginStatement();
            t.SourceContext = this.scanner.CurrentSourceContext;

            Debug.Assert(this.currentToken == Token.InvokePlugin);
            this.GetNextToken();

            SourceContext parenStart = this.scanner.CurrentSourceContext;

            if (this.sink != null)
                this.sink.StartParameters(this.scanner.CurrentSourceContext);

            this.Skip(Token.LeftParenthesis);

            int endCol;
            t.Operands = this.ParseArgumentList(followers, out endCol);
            SourceContext parenEnd = this.scanner.CurrentSourceContext;
            parenEnd.StartPos = endCol - 1;
            parenEnd.EndPos = endCol;
            if (this.sink != null)
                this.sink.MatchPair(parenStart, parenEnd);

            t.SourceContext.EndPos = this.scanner.CurrentSourceContext.StartPos;
            this.SkipSemiColon(followers);
            this.SkipTo(followers);
            return t;
        }

        private Statement ParseInvokeSched(TokenSet followers)
        {
            InvokeSchedulerStatement t = new InvokeSchedulerStatement();
            t.SourceContext = this.scanner.CurrentSourceContext;

            Debug.Assert(this.currentToken == Token.InvokeShed);
            this.GetNextToken();

            SourceContext parenStart = this.scanner.CurrentSourceContext;

            if (this.sink != null)
                this.sink.StartParameters(this.scanner.CurrentSourceContext);

            this.Skip(Token.LeftParenthesis);

            int endCol;
            t.Operands = this.ParseArgumentList(followers, out endCol);
            SourceContext parenEnd = this.scanner.CurrentSourceContext;
            parenEnd.StartPos = endCol - 1;
            parenEnd.EndPos = endCol;
            if (this.sink != null)
                this.sink.MatchPair(parenStart, parenEnd);

            t.SourceContext.EndPos = this.scanner.CurrentSourceContext.StartPos;
            this.SkipSemiColon(followers);
            this.SkipTo(followers);
            return t;
        }

        private Statement ParseSend(TokenSet followers)
        {
            SendStatement s = new SendStatement();
            s.SourceContext = this.scanner.CurrentSourceContext;

            Debug.Assert(this.currentToken == Token.Send);
            this.GetNextToken();
            SourceContext argContext = this.scanner.CurrentSourceContext;
            this.Skip(Token.LeftParenthesis);
            s.channel = this.ParseExpression(followers | Token.Comma);
            this.Skip(Token.Comma);
            s.data = this.ParseExpression(followers | Token.RightParenthesis);
            this.ParseBracket(argContext, Token.RightParenthesis, followers | Token.Semicolon, Error.ExpectedRightParenthesis);
            s.SourceContext.EndPos = this.scanner.CurrentSourceContext.StartPos;
            this.SkipSemiColon(followers);
            this.SkipTo(followers);
            return s;
        }

        private Statement ParseSelect(TokenSet followers)
        {
            Select s = new Select();
            s.SourceContext = this.scanner.CurrentSourceContext;

            Debug.Assert(this.currentToken == Token.Select);
            this.GetNextToken();

            // optional qualifiers may appear here in any order (but only once)
            for (; ; )
            {
                switch (this.currentToken)
                {
                    case Token.End:
                        if (s.validEndState)
                            this.HandleError(Error.DuplicateModifier, this.scanner.GetTokenSource());
                        else
                            s.validEndState = true;
                        this.GetNextToken();
                        continue;

                    case Token.First:
                        if (s.deterministicSelection)
                            this.HandleError(Error.DuplicateModifier, this.scanner.GetTokenSource());
                        else
                            s.deterministicSelection = true;
                        this.GetNextToken();
                        continue;

                    case Token.Visible:
                        if (s.visible)
                            this.HandleError(Error.DuplicateModifier, this.scanner.GetTokenSource());
                        else
                            s.visible = true;
                        this.GetNextToken();
                        continue;

                    default:
                        break;
                }
                break;
            }

            if (this.currentToken == Token.End)
            {
                s.validEndState = true;
                this.GetNextToken();

                if (this.currentToken == Token.First)
                {
                    s.deterministicSelection = true;
                    this.GetNextToken();
                }
            }
            else if (this.currentToken == Token.First)
            {
                s.deterministicSelection = true;
                this.GetNextToken();
                if (this.currentToken == Token.End)
                {
                    s.validEndState = true;
                    this.GetNextToken();
                }
            }

            if (this.currentToken != Token.LeftBrace)
            {
                this.SkipTo(followers, Error.ExpectedLeftBrace);
                return null;
            }

            SourceContext ctx = this.scanner.CurrentSourceContext;
            this.Skip(Token.LeftBrace);

            s.joinStatementList = new JoinStatementList();
            for (; ; )
            {
                if (this.currentToken == Token.RightBrace)
                    break;
                else if (JoinPatternStart[this.currentToken])
                {
                    JoinStatement js = this.ParseJoinStatement(followers | Parser.JoinPatternStart | Token.RightBrace);
                    if (js != null)
                        s.joinStatementList.Add(js);
                }
                else
                {
                    this.SkipTo(followers, Error.ExpectedRightBraceOrJoinPattern);
                    return null;
                }
            }

            s.SourceContext.EndPos = this.scanner.CurrentSourceContext.StartPos;
            this.ParseBracket(ctx, Token.RightBrace, followers, Error.ExpectedRightBrace);
            this.SkipTo(followers);

            if (s.joinStatementList.Length == 0)
            {
                this.HandleError(Error.ExpectedJoinStatement);
                return null;
            }

            return s;
        }

        private JoinStatement ParseJoinStatement(TokenSet followers)
        {
            JoinStatement js = new JoinStatement();
            js.SourceContext = this.scanner.CurrentSourceContext;

            js.attributes = this.ParseAttributes(followers);

            js.joinPatternList = new JoinPatternList();
            for (; ; )
            {
                if (JoinPatternStart[this.currentToken])
                {
                    JoinPattern jp = this.ParseJoinPattern(followers | Token.LogicalAnd | Token.Arrow);
                    if (jp != null)
                    {
                        js.joinPatternList.Add(jp);

                        if (this.currentToken == Token.LogicalAnd)
                        {
                            this.GetNextToken();
                            continue;
                        }
                        else if (this.currentToken == Token.Arrow)
                        {
                            this.GetNextToken();
                            break;
                        }
                        else
                        {
                            this.SkipTo(followers, Error.ExpectedJoinPatternSeparator);
                            return null;
                        }
                    }
                }
                else
                {
                    this.SkipTo(followers, Error.ExpectedJoinPattern);
                    return null;
                }
            }

            js.statement = this.ParseStatement(followers);

            js.SourceContext.EndPos = this.scanner.CurrentSourceContext.StartPos;
            this.SkipTo(followers);
            return js;
        }

        private JoinPattern ParseJoinPattern(TokenSet followers)
        {
            SourceContext ctx;
            Debug.Assert(Parser.JoinPatternStart[this.currentToken]);

            switch (this.currentToken)
            {
                case Token.Timeout:
                    TimeoutPattern t = new TimeoutPattern();
                    t.SourceContext = this.scanner.CurrentSourceContext;
                    this.GetNextToken();
                    return t;

                case Token.Receive:
                    ReceivePattern r = new ReceivePattern();
                    r.SourceContext = this.scanner.CurrentSourceContext;
                    this.GetNextToken();
                    ctx = this.scanner.CurrentSourceContext;
                    this.Skip(Token.LeftParenthesis);
                    r.channel = this.ParseExpression(followers | Token.Comma);
                    this.Skip(Token.Comma);
                    r.data = this.ParseExpression(followers | Token.RightParenthesis);
                    this.ParseBracket(ctx, Token.RightParenthesis, followers, Error.ExpectedRightParenthesis);
                    return r;

                case Token.Event:
                    EventPattern e = new EventPattern();
                    e.SourceContext = this.scanner.CurrentSourceContext;
                    this.GetNextToken();
                    ctx = this.scanner.CurrentSourceContext;
                    this.Skip(Token.LeftParenthesis);
                    e.channelNumber = this.ParseExpression(followers | Token.Comma);
                    this.Skip(Token.Comma);
                    e.messageType = this.ParseExpression(followers | Token.Comma);
                    this.Skip(Token.Comma);
                    e.direction = this.ParseExpression(followers | Token.RightParenthesis);
                    this.ParseBracket(ctx, Token.RightParenthesis, followers, Error.ExpectedRightParenthesis);
                    return e;

                case Token.Wait:
                    WaitPattern w = new WaitPattern();
                    w.SourceContext = this.scanner.CurrentSourceContext;
                    this.GetNextToken();
                    w.expression = this.ParseParenthesizedExpression(followers);
                    w.SourceContext.EndPos = this.scanner.CurrentSourceContext.StartPos;
                    return w;

                default:
                    break;
            }
            return null;
        }

        private If ParseIf(TokenSet followers)
        {
            If If = new If();
            If.SourceContext = this.scanner.CurrentSourceContext;
            Debug.Assert(this.currentToken == Token.If);
            this.GetNextToken();
            If.Condition = this.ParseParenthesizedExpression(followers | Parser.StatementStart);
            Block b = this.ParseStatementAsBlock(followers | Token.Else);
            If.TrueBlock = b;
            if (b != null)
            {
                If.SourceContext.EndPos = b.SourceContext.EndPos;
                if (b.Statements == null)
                    this.HandleError(b.SourceContext, Error.PossibleMistakenNullStatement);
            }
            if (this.currentToken == Token.Else)
            {
                this.GetNextToken();
                b = this.ParseStatementAsBlock(followers);
                If.FalseBlock = b;
                if (b != null)
                {
                    If.SourceContext.EndPos = b.SourceContext.EndPos;
                    if (b.Statements == null)
                        this.HandleError(b.SourceContext, Error.PossibleMistakenNullStatement);
                }
            }
            return If;
        }

        private Statement ParseGoto(TokenSet followers)
        {
            SourceContext sctx = this.scanner.CurrentSourceContext;
            Debug.Assert(this.currentToken == Token.Goto);
            this.GetNextToken();
            Statement result = new Goto(this.scanner.GetIdentifier());
            this.Skip(Token.Identifier);
            sctx.EndPos = this.scanner.endPos;
            result.SourceContext = sctx;
            this.SkipSemiColon(followers);
            this.SkipTo(followers);
            return result;
        }

        private Return ParseReturn(TokenSet followers)
        {
            Return Return = new Return();
            Return.SourceContext = this.scanner.CurrentSourceContext;
            Debug.Assert(this.currentToken == Token.Return);
            this.GetNextToken();
            if (this.currentToken != Token.Semicolon)
            {
                Expression expr = Return.Expression = this.ParseExpression(followers | Token.Semicolon);
                if (expr != null)
                    Return.SourceContext.EndPos = expr.SourceContext.EndPos;
            }
            this.SkipSemiColon(followers);
            this.SkipTo(followers);
            return Return;
        }

        private YieldStatement ParseYield(TokenSet followers)
        {
            YieldStatement Yield = new YieldStatement();
            Yield.SourceContext = this.scanner.CurrentSourceContext;
            Debug.Assert(this.currentToken == Token.Yield);
            this.GetNextToken();
            this.SkipSemiColon(followers);
            this.SkipTo(followers);
            return Yield;
        }

        private While ParseWhile(TokenSet followers)
        {
            While While = new While();
            While.SourceContext = this.scanner.CurrentSourceContext;
            Debug.Assert(this.currentToken == Token.While);
            this.GetNextToken();
            While.Condition = this.ParseParenthesizedExpression(followers);
            SourceContext savedCompoundStatementOpeningContext = this.compoundStatementOpeningContext;
            While.SourceContext.EndPos = this.scanner.endPos;
            this.compoundStatementOpeningContext = While.SourceContext;
            Block b = this.ParseStatementAsBlock(followers);
            While.Body = b;
            if (b != null)
                While.SourceContext.EndPos = b.SourceContext.EndPos;
            this.SkipTo(followers);
            this.compoundStatementOpeningContext = savedCompoundStatementOpeningContext;
            return While;
        }

        private ForEach ParseForEach(TokenSet followers)
        {
            ForEach forEach = new ForEach();
            forEach.SourceContext = this.scanner.CurrentSourceContext;
            Debug.Assert(this.currentToken == Token.Foreach);
            this.GetNextToken();
            SourceContext sctx = this.scanner.CurrentSourceContext;
            this.Skip(Token.LeftParenthesis);
            forEach.TargetVariableType = this.ParseBaseTypeExpression(followers | Token.Identifier | Token.In | Token.RightParenthesis, false, true);
            if (this.currentToken == Token.In)
            {
                if (forEach.TargetVariableType is TypeExpression && ((TypeExpression)forEach.TargetVariableType).Expression is Identifier)
                {
                    // this case is no longer an error. The type will be supplied by the 'in' expression in the Resolver.
                    forEach.TargetVariable = ((TypeExpression)forEach.TargetVariableType).Expression;
                    forEach.TargetVariableType = null;
                }
                else
                {
                    forEach.TargetVariable = this.scanner.GetIdentifier();
                    this.HandleError(Error.ExpectedIdentifier);
                }
            }
            else
            {
                forEach.TargetVariable = this.scanner.GetIdentifier();
                this.Skip(Token.Identifier);
            }
            this.Skip(Token.In);
            forEach.SourceEnumerable = this.ParseExpression(followers | Token.RightParenthesis);
            this.ParseBracket(sctx, Token.RightParenthesis, followers | Parser.StatementStart, Error.ExpectedRightParenthesis);
            SourceContext savedCompoundStatementOpeningContext = this.compoundStatementOpeningContext;
            forEach.SourceContext.EndPos = this.scanner.endPos;
            this.compoundStatementOpeningContext = forEach.SourceContext;
            Block b = this.ParseStatementAsBlock(followers);
            forEach.Body = b;
            if (b != null)
                forEach.SourceContext.EndPos = b.SourceContext.EndPos;
            this.compoundStatementOpeningContext = savedCompoundStatementOpeningContext;
            return forEach;
        }

        private Expression ParseSelf(TokenSet followers)
        {
            SelfExpression self = new SelfExpression();
            self.SourceContext = this.scanner.CurrentSourceContext;
            return self;
        }

        private Statement ParseTry(TokenSet followers)
        {
            ZTry Try = new ZTry();
            Try.SourceContext = this.scanner.CurrentSourceContext;
            Debug.Assert(this.currentToken == Token.Try);
            this.GetNextToken();
            Try.Body = this.ParseBlock(followers | Token.With);
            Try.Catchers = new WithList();

            this.Skip(Token.With);
            SourceContext ctx = this.scanner.CurrentSourceContext;
            this.Skip(Token.LeftBrace);

            With defaultWith = null;

            for (; ; )
            {
                if (this.currentToken == Token.RightBrace)
                    break;
                else if (this.currentToken == Token.Multiply)
                {
                    if (defaultWith == null)
                    {
                        defaultWith = new With();
                        defaultWith.SourceContext = this.scanner.CurrentSourceContext;
                        defaultWith.Name = null;
                        this.Skip(Token.Multiply);
                        this.Skip(Token.Arrow);
                        defaultWith.Block = this.ParseStatementAsBlock(followers | Token.Identifier | Token.Multiply | Token.RightBrace);
                        defaultWith.SourceContext.EndPos = this.scanner.CurrentSourceContext.StartPos;
                    }
                    else
                    {
                        // Parse the "extra" default handler, but report an error and discard the
                        // IR for this code.
                        With With = new With();
                        With.SourceContext = this.scanner.CurrentSourceContext;
                        this.Skip(Token.Multiply);
                        this.Skip(Token.Arrow);
                        this.ParseStatementAsBlock(followers | Token.Identifier | Token.Multiply | Token.RightBrace);
                        With.SourceContext.EndPos = this.scanner.CurrentSourceContext.StartPos;
                        this.HandleError(With.SourceContext, Error.MultipleDefaultHandlers);
                    }
                }
                else if (this.currentToken == Token.Identifier)
                {
                    With with = new With();
                    with.SourceContext = this.scanner.CurrentSourceContext;
                    with.Name = this.scanner.GetIdentifier();
                    this.Skip(Token.Identifier);
                    this.Skip(Token.Arrow);
                    with.Block = this.ParseStatementAsBlock(followers | Token.Identifier | Token.Multiply | Token.RightBrace);
                    with.SourceContext.EndPos = this.scanner.CurrentSourceContext.StartPos;
                    Try.Catchers.Add(with);
                }
                else
                {
                    this.SkipTo(followers | Token.RightBrace, Error.ExpectedExceptionHandler);
                    break;
                }
            }

            // Add the default "with" at the end of the list to make code generation
            // easier later on.
            if (defaultWith != null)
                Try.Catchers.Add(defaultWith);

            Try.SourceContext.EndPos = this.scanner.CurrentSourceContext.StartPos;
            this.ParseBracket(ctx, Token.RightBrace, followers, Error.ExpectedRightBrace);
            this.SkipTo(followers);
            return Try;
        }

        private Throw ParseRaise(TokenSet followers)
        {
            Throw Throw = new Throw();
            Throw.SourceContext = this.scanner.CurrentSourceContext;
            Debug.Assert(this.currentToken == Token.Raise);
            this.GetNextToken();
            if (this.currentToken != Token.Semicolon)
            {
                Expression expr = Throw.Expression = this.ParseExpression(followers | Token.Semicolon);
                if (expr != null)
                    Throw.SourceContext.EndPos = expr.SourceContext.EndPos;
            }
            this.SkipSemiColon(followers);
            this.SkipTo(followers);
            return Throw;
        }

        private Statement ParseAssume(TokenSet followers)
        {
            AssumeStatement a = new AssumeStatement();
            a.SourceContext = this.scanner.CurrentSourceContext;
            Debug.Assert(this.currentToken == Token.Assume);
            this.GetNextToken();

            a.booleanExpr = this.ParseParenthesizedExpression(followers);

            this.SkipSemiColon(followers);
            this.SkipTo(followers);
            return a;
        }

        private Block ParseStatementAsBlock(TokenSet followers)
        {
            Statement s = this.ParseStatement(followers);
            Block b = s as Block;
            //
            // If the statement is NOT a block, or if it is an atomic block,
            // then it needs to be wrapped up in regular block.
            //
            if (s != null && (b == null || s is AtomicBlock))
            {
                if (s is LabeledStatement)
                    this.HandleError(s.SourceContext, Error.BadEmbeddedStmt);
                b = new Block(new StatementList(1));
                b.Statements.Add(s);
                b.SourceContext = s.SourceContext;
            }
            return b;
        }

        private StatementList ParseStatements(TokenSet followers)
        {
            StatementList statements = new StatementList();
            this.ParseStatements(statements, followers);
            return statements;
        }

        private void ParseStatements(StatementList statements, TokenSet followers)
        {
            TokenSet statementFollowers = followers | Parser.StatementStart;
            if (!statementFollowers[this.currentToken])
            {
                this.SkipTo(statementFollowers, Error.InvalidExprTerm, this.scanner.GetTokenSource());
            }
            while (Parser.StatementStart[this.currentToken])
            {
                Statement s = this.ParseStatement(statementFollowers);
                if (s != null)
                    statements.Add(s);
            }
            this.SkipTo(followers);
        }

        private Statement ParseStatement(TokenSet followers)
        {
            return this.ParseStatement(followers, false);
        }

        private Statement ParseStatement(TokenSet followers, bool preferExpressionToDeclaration)
        {
            switch (this.currentToken)
            {
                case Token.Semicolon: Block b = new Block(null, this.scanner.CurrentSourceContext); this.GetNextToken(); return b;
                case Token.LeftBracket: return this.ParseAttributedStatement(followers);
                case Token.LeftBrace: return this.ParseBlock(followers);
                case Token.Atomic: return this.ParseAtomicBlock(followers);
                case Token.Async: return this.ParseAsyncCall(followers);
                case Token.Accept: return this.ParseAccept(followers);
                case Token.Assert: return this.ParseAssert(followers);
                case Token.Assume: return this.ParseAssume(followers);
                case Token.InvokePlugin: return this.ParseInvokePlugin(followers);
                case Token.InvokeShed: return this.ParseInvokeSched(followers);
                case Token.Trace: return this.ParseTrace(followers);
                case Token.Event: return this.ParseEvent(followers);
                case Token.If: return this.ParseIf(followers);
                case Token.While: return this.ParseWhile(followers);
                case Token.Send: return this.ParseSend(followers);
                case Token.Select: return this.ParseSelect(followers);
                case Token.Goto: return this.ParseGoto(followers);
                case Token.Return: return this.ParseReturn(followers);
                case Token.Foreach: return this.ParseForEach(followers);
                case Token.Try: return this.ParseTry(followers);
                case Token.Raise: return this.ParseRaise(followers);
                case Token.Yield: return this.ParseYield(followers);

                default: return this.ParseExpressionStatementOrDeclaration(false, followers, preferExpressionToDeclaration, true);
            }
        }

        private Statement ParseExpressionStatementOrDeclaration(bool acceptComma, TokenSet followers, bool preferExpressionToDeclaration, bool skipSemicolon)
        {
            SourceContext startingContext = this.scanner.CurrentSourceContext;
            Token tok = this.currentToken;
            TypeNode t = this.ParseBaseTypeExpression(followers | Token.Identifier, false, true);
            if (t == null || this.currentToken != Token.Identifier)
            {
                //Tried to parse a type expression and failed, or clearly not dealing with a declaration.
                //Restore prior state and reparse as expression
                this.scanner.endPos = startingContext.StartPos;
                this.currentToken = Token.None;
                this.GetNextToken();
                TokenSet followersOrColon = followers | Token.Colon;
                Expression e = this.ParseExpression(followersOrColon);
                ExpressionStatement eStat = new ExpressionStatement(e);
                if (e != null) eStat.SourceContext = e.SourceContext;
                Identifier id = null;
                if (this.currentToken == Token.Colon && !acceptComma && (id = e as Identifier) != null)
                    return this.ParseLabeledStatement(id, followers);
                if (!acceptComma || this.currentToken != Token.Comma)
                {
                    if (!preferExpressionToDeclaration)
                    {
                        if (!(e == null || e is AssignmentExpression || e is MethodCall || followers[Token.RightParenthesis]))
                        {
                            this.HandleError(e.SourceContext, Error.IllegalStatement);
                            eStat = null;
                        }
                        if (this.currentToken == Token.Semicolon)
                            this.GetNextToken();
                        else if (skipSemicolon)
                            this.SkipSemiColon(followers);
                    }
                    else if (skipSemicolon && (this.currentToken != Token.RightBrace || !followers[Token.RightBrace]))
                    {
                        if (this.currentToken == Token.Semicolon && followers[Token.RightBrace])
                        {
                            //Dealing with an expression block.
                            this.GetNextToken();
                            if (this.currentToken != Token.RightBrace)
                            {
                                //Not the last expression in the block. Complain if it is not a valid expression statement.
                                if (!(e == null || e is AssignmentExpression || e is MethodCall || followers[Token.RightParenthesis]))
                                    this.HandleError(e.SourceContext, Error.IllegalStatement);
                            }
                        }
                        else
                            this.SkipSemiColon(followers);
                    }
                    this.SkipTo(followers);
                }
                return eStat;
            }
            return this.ParseVariableDeclaration(t, startingContext, followers);
        }

        private Statement ParseVariableDeclaration(TypeNode t, SourceContext ctx, TokenSet followers)
        {
            VariableDeclaration result = new VariableDeclaration();
            result.SourceContext = ctx;
            result.Name = this.scanner.GetIdentifier();
            result.Type = t;
            this.Skip(Token.Identifier);

            if (this.currentToken == Token.Assign)
            {
                this.Skip(Token.Assign);
                result.Initializer = this.ParseExpression(followers | Token.Semicolon);
            }
            result.SourceContext.EndPos = this.scanner.endPos;
            this.SkipSemiColon(followers);
            this.SkipTo(followers);
            return result;
        }

        private LabeledStatement ParseLabeledStatement(Identifier label, TokenSet followers)
        {
            LabeledStatement result = new LabeledStatement();
            result.SourceContext = label.SourceContext;
            result.Label = label;
            Debug.Assert(this.currentToken == Token.Colon);
            this.GetNextToken();
            if (Parser.StatementStart[this.currentToken])
            {
                result.Statement = this.ParseStatement(followers);
                result.SourceContext.EndPos = this.scanner.endPos;
            }
            else
                this.SkipTo(followers, Error.ExpectedSemicolon);
            return result;
        }

        private AttributedStatement ParseAttributedStatement(TokenSet followers)
        {
            AttributedStatement result = new AttributedStatement();
            result.SourceContext = this.scanner.CurrentSourceContext;
            result.Attributes = this.ParseAttributes(followers);
            if (Parser.StatementStart[this.currentToken])
            {
                result.Statement = this.ParseStatement(followers);
                result.SourceContext.EndPos = this.scanner.endPos;
            }
            else
                this.SkipTo(followers, Error.ExpectedSemicolon);
            return result;
        }

        private void ParseEnumDeclaration(Namespace ns, AttributeList attributes, SourceContext sctx, TokenSet followers)
        {
            EnumNode e = new EnumNode();
            e.Attributes = attributes;
            e.Flags |= TypeFlags.Public;
            e.SourceContext = sctx;
            e.DeclaringModule = this.module;
            Debug.Assert(this.currentToken == Token.Enum);
            this.GetNextToken();
            e.Name = this.scanner.GetIdentifier();
            this.Skip(Token.Identifier);
            e.Namespace = Identifier.For(ns.FullName);
            if (this.AddTypeToModule(e))
                ns.Types.Add(e);

            TypeNode t = SystemTypes.Int32;
            e.UnderlyingType = t;
            SourceContext blockContext = this.scanner.CurrentSourceContext;
            this.Skip(Token.LeftBrace);
            Field prevField = null;
            int offset = 0;
            while (this.currentToken != Token.RightBrace)
            {
                SourceContext ctx = this.scanner.CurrentSourceContext;
                Identifier id = this.scanner.GetIdentifier();
                this.Skip(Token.Identifier);
                Field f = new Field(e, null, FieldFlags.Public | FieldFlags.Literal | FieldFlags.Static | FieldFlags.HasDefault, id, e, null);
                e.Members.Add(f);
                f.SourceContext = ctx;
                if (prevField == null)
                    f.DefaultValue = new Literal(offset++, e);
                else
                {
                    f.Initializer = new BinaryExpression(new MemberBinding(null, prevField), Literal.Int32One, NodeType.Add, ctx);
                    prevField = f;
                }
                if (this.currentToken != Token.Comma)
                {
                    if (this.currentToken == Token.Semicolon)
                    {
                        SourceContext sc = this.scanner.CurrentSourceContext;
                        this.GetNextToken();
                        if (this.currentToken == Token.Identifier)
                        {
                            this.HandleError(sc, Error.SyntaxError, ",");
                            continue;
                        }
                        else if (this.currentToken == Token.RightBrace)
                        {
                            this.HandleError(sc, Error.ExpectedRightBrace);
                            break;
                        }
                    }
                    break;
                }
                this.GetNextToken();
            }
            this.ParseBracket(blockContext, Token.RightBrace, followers | Token.Semicolon, Error.ExpectedRightBrace);
            t.SourceContext.EndPos = this.scanner.CurrentSourceContext.StartPos;
            this.Skip(Token.Semicolon);
            this.SkipTo(followers);
        }

        private void ParseRangeDeclaration(Namespace ns, AttributeList attributes, SourceContext sctx, TokenSet followers)
        {
            Range r = new Range();
            r.Attributes = attributes;
            r.SourceContext = sctx;
            r.DeclaringModule = this.module;
            Debug.Assert(this.currentToken == Token.Range);
            this.GetNextToken();
            r.Name = this.scanner.GetIdentifier();
            this.Skip(Token.Identifier);
            r.Namespace = Identifier.For(ns.FullName);
            if (this.AddTypeToModule(r))
                ns.Types.Add(r);

            r.Min = this.ParseExpression(followers | Token.DotDot);

            this.Skip(Token.DotDot);

            r.Max = this.ParseExpression(followers | Token.Semicolon);
            r.SourceContext.EndPos = this.scanner.endPos;
            this.Skip(Token.Semicolon);
            this.SkipTo(followers);
        }

        private void ParseChanDeclaration(Namespace ns, AttributeList attributes, SourceContext sctx, TokenSet followers)
        {
            Chan c = new Chan();
            c.Flags |= TypeFlags.Public;
            c.Attributes = attributes;
            c.SourceContext = sctx;
            c.DeclaringModule = this.module;
            Debug.Assert(this.currentToken == Token.Chan);
            this.GetNextToken();
            c.Name = this.scanner.GetIdentifier();
            this.Skip(Token.Identifier);
            c.Namespace = Identifier.For(ns.FullName);
            if (this.AddTypeToModule(c))
                ns.Types.Add(c);

            c.ChannelType = this.ParseBaseTypeExpression(followers | Token.Semicolon, false, false);
            c.SourceContext.EndPos = this.scanner.endPos;
            this.Skip(Token.Semicolon);
            this.SkipTo(followers);
        }

        private void ParseSetDeclaration(Namespace ns, AttributeList attributes, SourceContext sctx, TokenSet followers)
        {
            Set s = new Set();
            s.Flags |= TypeFlags.Public;
            s.Attributes = attributes;
            s.SourceContext = sctx;
            s.DeclaringModule = this.module;
            Debug.Assert(this.currentToken == Token.Set);
            this.GetNextToken();
            s.Name = this.scanner.GetIdentifier();
            this.Skip(Token.Identifier);
            s.Namespace = Identifier.For(ns.FullName);
            if (this.AddTypeToModule(s))
                ns.Types.Add(s);

            s.SetType = this.ParseBaseTypeExpression(followers | Token.Semicolon, false, false);
            s.SourceContext.EndPos = this.scanner.endPos;
            this.Skip(Token.Semicolon);
            this.SkipTo(followers);
        }

        private void ParseArrayDeclaration(Namespace ns, AttributeList attributes, SourceContext sctx, TokenSet followers)
        {
            ZArray a = new ZArray();
            a.Flags |= TypeFlags.Public;
            a.Attributes = attributes;
            a.SourceContext = sctx;
            a.DeclaringModule = this.module;
            Debug.Assert(this.currentToken == Token.Array);
            this.GetNextToken();
            a.Name = this.scanner.GetIdentifier();
            this.Skip(Token.Identifier);

            a.Namespace = Identifier.For(ns.FullName);

            this.Skip(Token.LeftBracket);

            // TODO: support constant expressions here?

            switch (this.currentToken)
            {
                case Token.IntegerLiteral:
                    a.LowerBounds = new int[] { 0 };
                    a.Sizes = new int[] { (int)this.ParseIntegerLiteral().Value };
                    a.domainType = SystemTypes.Int32;
                    break;

                case Token.HexLiteral:
                    a.LowerBounds = new int[] { 0 };
                    a.Sizes = new int[] { (int)this.ParseHexLiteral().Value };
                    a.domainType = SystemTypes.Int32;
                    break;

                case Token.RightBracket:
                    a.LowerBounds = new int[] { 0 };
                    a.domainType = SystemTypes.Int32;   // variable-size arrays are always indexed by int
                    break;

                default:
                    a.domainType = this.ParseBaseTypeExpression(followers | Token.RightBracket, false, false);
                    break;
            }
            this.Skip(Token.RightBracket);

            a.ElementType = this.ParseBaseTypeExpression(followers | Token.Semicolon, false, false);
            a.Rank = 1;
            a.SourceContext.EndPos = this.scanner.endPos;

            if (a.ElementType != null && a.domainType != null)
            {
                if (this.AddTypeToModule(a))
                    ns.Types.Add(a);
            }

            this.Skip(Token.Semicolon);
            this.SkipTo(followers);
        }

        private bool AddTypeToModule(TypeNode t)
        {
            TypeNode t1 = this.module.GetType(t.Namespace, t.Name);
            if (t1 == null)
            {
                this.module.Types.Add(t);
                return true;
            }
            else
            {
                this.HandleError(t.Name.SourceContext, Error.DuplicateNameInNS, t.Name.ToString(), t.Namespace.ToString());
                if (t1.Name.SourceContext.Document != null)
                    this.HandleError(t1.Name.SourceContext, Error.RelatedErrorLocation);
                return false;
            }
        }

        private void ParseBracket(SourceContext openingContext, Token token, TokenSet followers, Error error)
        {
            if (this.currentToken == token)
            {
                if (this.sink != null)
                    this.sink.MatchPair(openingContext, this.scanner.CurrentSourceContext);
                this.GetNextToken();
                this.SkipTo(followers);
            }
            else
                this.SkipTo(followers, error);
        }

        private Literal ParseHexLiteral()
        {
            Debug.Assert(this.currentToken == Token.HexLiteral);
            string tokStr = this.scanner.GetTokenSource();
            SourceContext ctx = this.scanner.CurrentSourceContext;
            TypeCode tc = this.scanner.ScanNumberSuffix();
            Literal result;
            try
            {
                switch (tc)
                {
                    case TypeCode.Single:
                    case TypeCode.Double:
                    case TypeCode.Decimal:
                        this.HandleError(Error.ExpectedSemicolon);
                        goto default;
                    default:
                        ulong ul = UInt64.Parse(tokStr.Substring(2), System.Globalization.NumberStyles.HexNumber, null);
                        if (ul <= int.MaxValue)
                            result = new Literal((int)ul, SystemTypes.Int32);
                        else if (ul <= uint.MaxValue && (tc == TypeCode.Empty || tc == TypeCode.UInt32))
                            result = new Literal((uint)ul, SystemTypes.UInt32);
                        else if (ul <= long.MaxValue && (tc == TypeCode.Empty || tc == TypeCode.Int64))
                            result = new Literal((long)ul, SystemTypes.Int64);
                        else
                            result = new Literal(ul, SystemTypes.UInt64);
                        break;
                }
            }
            catch (OverflowException)
            {
                this.HandleError(Error.NumericLiteralTooLarge);
                result = new Literal(0, SystemTypes.Int32);
            }
            ctx.EndPos = this.scanner.endPos;
            result.SourceContext = ctx;
            this.GetNextToken();
            return result;
        }

        private Literal ParseIntegerLiteral()
        {
            Debug.Assert(this.currentToken == Token.IntegerLiteral);
            string tokStr = this.scanner.GetTokenSource();
            SourceContext ctx = this.scanner.CurrentSourceContext;
            TypeCode tc = this.scanner.ScanNumberSuffix();
            ctx.EndPos = this.scanner.endPos;
            Literal result;
            try
            {
                switch (tc)
                {
                    case TypeCode.Single:
                        result = new Literal(Single.Parse(tokStr, null), SystemTypes.Single);
                        break;

                    case TypeCode.Double:
                        result = new Literal(Double.Parse(tokStr, null), SystemTypes.Double);
                        break;

                    case TypeCode.Decimal:
                        result = new Literal(Decimal.Parse(tokStr, null), SystemTypes.Decimal);
                        break;

                    default:
                        ulong ul = UInt64.Parse(tokStr, null);
                        if (ul <= int.MaxValue && tc == TypeCode.Empty)
                            result = new Literal((int)ul, SystemTypes.Int32);
                        else if (ul <= uint.MaxValue && (tc == TypeCode.Empty || tc == TypeCode.UInt32))
                            result = new Literal((uint)ul, SystemTypes.UInt32);
                        else if (ul <= long.MaxValue && (tc == TypeCode.Empty || tc == TypeCode.Int64))
                            result = new Literal((long)ul, SystemTypes.Int64);
                        else
                            result = new Literal(ul, SystemTypes.UInt64);
                        break;
                }
            }
            catch (OverflowException)
            {
                this.HandleError(ctx, Error.IntOverflow);
                result = new Literal(0, SystemTypes.Int32);
            }

            result.SourceContext = ctx;
            this.GetNextToken();
            return result;
        }

        private static char[] nonZeroDigits = { '1', '2', '3', '4', '5', '6', '7', '8', '9' };

        private Literal ParseRealLiteral()
        {
            Debug.Assert(this.currentToken == Token.RealLiteral);
            string tokStr = this.scanner.GetTokenSource();
            SourceContext ctx = this.scanner.CurrentSourceContext;
            TypeCode tc = this.scanner.ScanNumberSuffix();
            ctx.EndPos = this.scanner.endPos;
            Literal result;
            string typeName = null;
            try
            {
                switch (tc)
                {
                    case TypeCode.Single:
                        typeName = "float";
                        float fVal = Single.Parse(tokStr, NumberStyles.Any, CultureInfo.InvariantCulture);
                        if (fVal == 0f && tokStr.IndexOfAny(nonZeroDigits) >= 0)
                            this.HandleError(ctx, Error.FloatOverflow, typeName);
                        result = new Literal(fVal, SystemTypes.Single);
                        break;

                    case TypeCode.Empty:
                    case TypeCode.Double:
                        typeName = "double";
                        double dVal = Double.Parse(tokStr, NumberStyles.Any, CultureInfo.InvariantCulture);
                        if (dVal == 0d && tokStr.IndexOfAny(nonZeroDigits) >= 0)
                            this.HandleError(ctx, Error.FloatOverflow, typeName);
                        result = new Literal(dVal, SystemTypes.Double);
                        break;

                    case TypeCode.Decimal:
                        typeName = "decimal";
                        decimal decVal = Decimal.Parse(tokStr, NumberStyles.Any, CultureInfo.InvariantCulture);
                        if (decVal == 0m && tokStr.IndexOfAny(nonZeroDigits) >= 0)
                            this.HandleError(ctx, Error.FloatOverflow, typeName);
                        result = new Literal(decVal, SystemTypes.Decimal);
                        break;

                    default:
                        //TODO: give an error message
                        goto case TypeCode.Empty;
                }
            }
            catch (OverflowException)
            {
                this.HandleError(ctx, Error.FloatOverflow, typeName);
                result = new Literal(0, SystemTypes.Int32);
            }
            result.SourceContext = ctx;
            result.TypeWasExplicitlySpecifiedInSource = tc != TypeCode.Empty;
            this.GetNextToken();
            return result;
        }

        private TypeNode ParseBaseTypeExpression(TokenSet followers, bool voidOk, bool returnNullIfError)
        {
            TypeNode result = null;

            switch (this.currentToken)
            {
                case Token.Bool:
                case Token.Byte:
                case Token.SByte:
                case Token.Short:
                case Token.UShort:
                case Token.Int:
                case Token.UInt:
                case Token.Long:
                case Token.ULong:
                case Token.Float:
                case Token.Double:
                case Token.Decimal:
                case Token.Object:
                case Token.String:
                case Token.Void:
                    TypeNode pType = Parser.TypeNodeFor(this.currentToken);

                    result = new TypeExpression(new Literal(pType, SystemTypes.Type), this.scanner.CurrentSourceContext);
                    result.Name = pType.Name;

                    if (this.currentToken == Token.Void && !voidOk)
                    {
                        if (returnNullIfError)
                        {
                            this.GetNextToken();
                            return null;
                        }
                        else
                            this.HandleError(this.scanner.CurrentSourceContext, Error.UnexpectedVoidType);
                    }

                    this.GetNextToken();
                    break;

                case Token.Identifier:
                    Identifier id = this.scanner.GetIdentifier();

                    this.GetNextToken();
                    TypeExpression te = new TypeExpression(id);
                    result = te;
                    result.SourceContext = te.Expression.SourceContext;
                    break;

                default:
                    if (returnNullIfError)
                        return null;

                    result = new TypeExpression(null);   // create a dummy result so we can proceed
                    this.HandleError(Error.TypeExpected);
                    break;
            }
            //
            // LeftBracket is a follower here because it can start a statement,
            // but it also appears in expressions which we want to consider
            // an error here.
            //
            if (returnNullIfError &&
                (!followers[this.currentToken] || this.currentToken == Token.LeftBracket))
                return null;

            this.SkipTo(followers);
            return result;
        }

        private Expression ParseNew(TokenSet followers)
        {
            SourceContext ctx = this.scanner.CurrentSourceContext;
            Debug.Assert(this.currentToken == Token.New);
            this.GetNextToken();
            TypeNode t = this.ParseBaseTypeExpression(followers, false, false);
            if (t == null) { this.SkipTo(followers, Error.None); return null; }
            ctx.EndPos = t.SourceContext.EndPos;

            ExpressionList el = new ExpressionList();
            if (currentToken == Token.LeftBracket)
            {
                this.GetNextToken();
                Expression e = this.ParseExpression(followers | Token.RightBracket);
                this.ParseBracket(ctx, Token.RightBracket, followers, Error.ExpectedRightBracket);
                el.Add(e);
            }

            Construct cons = new Construct(new MemberBinding(null, t), el);
            cons.SourceContext = ctx;
            return cons;
        }

        private Expression ParseExpression(TokenSet followers)
        {
            TokenSet followersOrInfixOperators = followers | Parser.InfixOperators;
            Expression operand1 = this.ParseUnaryExpression(followersOrInfixOperators);
            if (followers[this.currentToken] && !Parser.InfixOperators[this.currentToken]) return operand1;
            return this.ParseAssignmentExpression(operand1, followers);
        }

        private Expression ParseParenthesizedExpression(TokenSet followers)
        {
            SourceContext sctx = this.scanner.CurrentSourceContext;
            this.Skip(Token.LeftParenthesis);
            Expression result = this.ParseExpression(followers | Token.RightParenthesis);
            this.ParseBracket(sctx, Token.RightParenthesis, followers, Error.ExpectedRightParenthesis);
            //TODO: introduce a special node to keep track of the parentheses for source reconstruction
            return result;
        }

        private Expression ParseUnaryExpression(TokenSet followers)
        {
            Expression expression;
            switch (this.currentToken)
            {
                case Token.Plus:
                case Token.LogicalNot:
                case Token.Subtract:
                case Token.BitwiseNot:
                    UnaryExpression uexpr = new UnaryExpression();
                    uexpr.SourceContext = this.scanner.CurrentSourceContext;
                    uexpr.NodeType = Parser.ConvertToUnaryNodeType(this.currentToken);
                    this.GetNextToken();
                    uexpr.Operand = this.ParseUnaryExpression(followers);
                    uexpr.SourceContext.EndPos = uexpr.Operand.SourceContext.EndPos;
                    expression = uexpr;
                    break;

                default:
                    expression = this.ParsePrimaryExpression(followers);
                    break;
            }
            return expression;
        }

        private Expression ParseAssignmentExpression(Expression operand1, TokenSet followers)
        {
            Debug.Assert(Parser.InfixOperators[this.currentToken]);
            if (this.currentToken == Token.Assign)
            {
                Token assignmentOperator = this.currentToken;
                this.GetNextToken();
                Expression operand2 = this.ParseExpression(followers);
                if (operand1 == null) return null;
                AssignmentStatement statement = new AssignmentStatement(operand1, operand2, Parser.ConvertToBinaryNodeType(assignmentOperator));
                statement.SourceContext = operand1.SourceContext;
                if (operand2 != null)
                    statement.SourceContext.EndPos = operand2.SourceContext.EndPos;
                Expression expression = new AssignmentExpression(statement);
                expression.SourceContext = statement.SourceContext;
                return expression;
            }
            else
            {
                operand1 = this.ParseBinaryExpression(operand1, followers);
                return operand1;
            }
        }

        private Expression ParseBinaryExpression(Expression operand1, TokenSet followers)
        {
            TokenSet unaryFollowers = followers | Parser.InfixOperators;
            Expression expression;
            switch (this.currentToken)
            {
                case Token.Plus:
                case Token.BitwiseAnd:
                case Token.BitwiseOr:
                case Token.BitwiseXor:
                case Token.Divide:
                case Token.Equal:
                case Token.GreaterThan:
                case Token.GreaterThanOrEqual:
                case Token.In:
                case Token.LeftShift:
                case Token.LessThan:
                case Token.LessThanOrEqual:
                case Token.LogicalAnd:
                case Token.LogicalOr:
                case Token.Multiply:
                case Token.NotEqual:
                case Token.Remainder:
                case Token.RightShift:
                case Token.Subtract:
                    Token operator1 = this.currentToken;
                    this.GetNextToken();
                    Expression operand2 = null;
                    operand2 = this.ParseUnaryExpression(unaryFollowers);
                    switch (this.currentToken)
                    {
                        case Token.Plus:
                        case Token.BitwiseAnd:
                        case Token.BitwiseOr:
                        case Token.BitwiseXor:
                        case Token.Divide:
                        case Token.Equal:
                        case Token.GreaterThan:
                        case Token.GreaterThanOrEqual:
                        case Token.In:
                        case Token.LeftShift:
                        case Token.LessThan:
                        case Token.LessThanOrEqual:
                        case Token.LogicalAnd:
                        case Token.LogicalOr:
                        case Token.Multiply:
                        case Token.NotEqual:
                        case Token.Remainder:
                        case Token.RightShift:
                        case Token.Subtract:
                            expression = this.ParseComplexExpression(Token.None, operand1, operator1, operand2, followers, unaryFollowers);
                            break;

                        default:
                            expression = new BinaryExpression(operand1, operand2, Parser.ConvertToBinaryNodeType(operator1));
                            expression.SourceContext = operand1.SourceContext;
                            expression.SourceContext.EndPos = operand2.SourceContext.EndPos;
                            break;
                    }
                    break;

                default:
                    expression = operand1;
                    break;
            }
            return expression;
        }

        private Expression ParseComplexExpression(Token operator0, Expression operand1, Token operator1, Expression operand2, TokenSet followers, TokenSet unaryFollowers)
        {
        restart:
            Token operator2 = this.currentToken;
            this.GetNextToken();
            Expression expression = null;
            Expression operand3 = null;
            operand3 = this.ParseUnaryExpression(unaryFollowers);
            if (Parser.LowerPriority(operator1, operator2))
            {
                switch (this.currentToken)
                {
                    case Token.Plus:
                    case Token.BitwiseAnd:
                    case Token.BitwiseOr:
                    case Token.BitwiseXor:
                    case Token.Divide:
                    case Token.Equal:
                    case Token.GreaterThan:
                    case Token.GreaterThanOrEqual:
                    case Token.In:
                    case Token.LeftShift:
                    case Token.LessThan:
                    case Token.LessThanOrEqual:
                    case Token.LogicalAnd:
                    case Token.LogicalOr:
                    case Token.Multiply:
                    case Token.NotEqual:
                    case Token.Remainder:
                    case Token.RightShift:
                    case Token.Subtract:
                        if (Parser.LowerPriority(operator2, this.currentToken))
                            //Can't reduce just operand2 op2 operand3 because there is an op3 with priority over op2
                            operand2 = this.ParseComplexExpression(operator1, operand2, operator2, operand3, followers, unaryFollowers); //reduce complex expression
                        //Now either at the end of the entire expression, or at an operator that is at the same or lower priority than op1
                        //Either way, operand2 op2 operand3 op3 ... has been reduced to just operand2 and the code below will
                        //either restart this procedure to parse the remaining expression or reduce operand1 op1 operand2 and return to the caller
                        else
                            goto default;
                        break;

                    default:
                        //Reduce operand2 op2 operand3. There either is no further binary operator, or it does not take priority over op2.
                        expression = new BinaryExpression(operand2, operand3, Parser.ConvertToBinaryNodeType(operator2));
                        expression.SourceContext = operand2.SourceContext;
                        expression.SourceContext.EndPos = operand3.SourceContext.EndPos;
                        operand2 = expression;
                        //The code following this will reduce operand1 op1 operand2 and return to the caller
                        break;
                }
            }
            else
            {
                Expression opnd1 = new BinaryExpression(operand1, operand2, Parser.ConvertToBinaryNodeType(operator1));
                opnd1.SourceContext = operand1.SourceContext;
                opnd1.SourceContext.EndPos = operand2.SourceContext.EndPos;
                operand1 = opnd1;
                operand2 = operand3;
                operator1 = operator2;
            }
            //At this point either operand1 op1 operand2 has been reduced, or operand2 op2 operand3 .... has been reduced, so back to just two operands
            switch (this.currentToken)
            {
                case Token.Plus:
                case Token.BitwiseAnd:
                case Token.BitwiseOr:
                case Token.BitwiseXor:
                case Token.Divide:
                case Token.Equal:
                case Token.GreaterThan:
                case Token.GreaterThanOrEqual:
                case Token.In:
                case Token.LeftShift:
                case Token.LessThan:
                case Token.LessThanOrEqual:
                case Token.LogicalAnd:
                case Token.LogicalOr:
                case Token.Multiply:
                case Token.NotEqual:
                case Token.Remainder:
                case Token.RightShift:
                case Token.Subtract:
                    if (operator0 == Token.None || Parser.LowerPriority(operator0, this.currentToken))
                        //The caller is not prepared to deal with the current token, go back to the start of this routine and consume some more tokens
                        goto restart;
                    else
                        goto default; //Let the caller deal with the current token
                default:
                    //reduce operand1 op1 operand2 and return to caller
                    expression = new BinaryExpression(operand1, operand2, Parser.ConvertToBinaryNodeType(operator1));
                    expression.SourceContext = operand1.SourceContext;
                    expression.SourceContext.EndPos = operand2.SourceContext.EndPos;
                    break;
            }
            return expression;
        }

        private Expression ParsePrimaryExpression(TokenSet followers)
        {
            Expression expression = null;
            SourceContext sctx = this.scanner.CurrentSourceContext;
            switch (this.currentToken)
            {
                case Token.New:
                    expression = this.ParseNew(followers | Token.Dot | Token.LeftBracket);
                    break;

                case Token.Identifier:
                    expression = this.scanner.GetIdentifier();
                    if (this.sink != null)
                    {
                        this.sink.StartName((Identifier)expression);
                    }
                    this.GetNextToken();
                    break;

                case Token.Null:
                    expression = new Literal(null, SystemTypes.Object, sctx);
                    this.GetNextToken();
                    break;

                case Token.True:
                    expression = new Literal(true, SystemTypes.Boolean, sctx);
                    this.GetNextToken();
                    break;

                case Token.False:
                    expression = new Literal(false, SystemTypes.Boolean, sctx);
                    this.GetNextToken();
                    break;

                case Token.HexLiteral:
                    expression = this.ParseHexLiteral();
                    break;

                case Token.IntegerLiteral:
                    expression = this.ParseIntegerLiteral();
                    break;

                case Token.RealLiteral:
                    expression = this.ParseRealLiteral();
                    break;

                case Token.StringLiteral:
                    expression = this.scanner.GetStringLiteral();
                    this.GetNextToken();
                    break;

                case Token.Self:
                    expression = ParseSelf(followers);
                    this.GetNextToken();
                    break;

                case Token.This:
                    expression = new This(sctx, false); // LJW added "false, is not a ctor call"
                    this.GetNextToken();
                    break;

                case Token.Typeof:
                    {
                        UnaryExpression uex = new UnaryExpression(null, NodeType.Typeof);
                        uex.SourceContext = sctx;
                        this.GetNextToken();
                        SourceContext openParen = this.scanner.CurrentSourceContext;
                        this.Skip(Token.LeftParenthesis);
                        TypeNode t = this.ParseBaseTypeExpression(followers | Token.RightParenthesis, false, false);
                        uex.Operand = new MemberBinding(null, t);
                        if (t != null) uex.Operand.SourceContext = t.SourceContext;
                        uex.SourceContext.EndPos = this.scanner.endPos;
                        this.ParseBracket(openParen, Token.RightParenthesis, followers, Error.ExpectedRightParenthesis);
                        expression = uex;
                    }
                    break;

                case Token.Sizeof:
                case Token.Choose:
                    //
                    // For both sizeof() and choose() we need to accept either a general expression or
                    // a type expression. Try parsing as a type expression first and if that fails,
                    // re-parse as an expression.
                    //
                    {
                        UnaryExpression uex = new UnaryExpression(null,
                            this.currentToken == Token.Sizeof ? NodeType.Sizeof : (NodeType)ZingNodeType.Choose);
                        uex.SourceContext = sctx;
                        this.GetNextToken();
                        SourceContext openParen = this.scanner.CurrentSourceContext;
                        this.Skip(Token.LeftParenthesis);

                        SourceContext startingContext = this.scanner.CurrentSourceContext;
                        Token tok = this.currentToken;
                        TypeNode t = this.ParseBaseTypeExpression(followers | Token.RightParenthesis, false, true);
                        if (t != null)
                        {
                            uex.Operand = new MemberBinding(null, t);
                            if (t != null) uex.Operand.SourceContext = t.SourceContext;
                        }
                        else
                        {
                            //Restore prior state and reparse as expression
                            this.scanner.endPos = startingContext.StartPos;
                            this.currentToken = Token.None;
                            this.GetNextToken();
                            uex.Operand = this.ParseExpression(followers | Token.RightParenthesis);
                        }
                        uex.SourceContext.EndPos = this.scanner.endPos;
                        this.ParseBracket(openParen, Token.RightParenthesis, followers, Error.ExpectedRightParenthesis);
                        expression = uex;
                    }
                    break;

                case Token.LeftParenthesis:
                    expression = this.ParseParenthesizedExpression(followers | Token.Dot | Token.LeftBracket);  // TODO: bracket?
                    break;

                default:
                    if (this.currentToken == Token.Identifier) goto case Token.Identifier;
                    expression = new Literal(null, SystemTypes.Object, this.scanner.CurrentSourceContext);
                    if (Parser.InfixOperators[this.currentToken])
                    {
                        this.HandleError(Error.InvalidExprTerm, this.scanner.GetTokenSource());
                        this.GetNextToken();
                    }
                    else
                        this.SkipTo(followers | Parser.PrimaryStart, Error.InvalidExprTerm, this.scanner.GetTokenSource());
                    if (Parser.PrimaryStart[this.currentToken]) return this.ParsePrimaryExpression(followers);
                    return expression;
            }
            expression = this.ParseIndexerCallOrSelector(expression, followers);
            this.SkipTo(followers);
            return expression;
        }

        private Expression ParseIndexerCallOrSelector(Expression expression, TokenSet followers)
        {
            TokenSet followersOrContinuers = followers | Token.LeftBracket | Token.LeftParenthesis | Token.Dot;
            for (; ; )
            {
                switch (this.currentToken)
                {
                    case Token.LeftBracket:
                        if (this.sink != null)
                            this.sink.StartParameters(this.scanner.CurrentSourceContext);
                        this.GetNextToken();
                        int endCol;
                        ExpressionList indices = this.ParseIndexList(followersOrContinuers, out endCol);
                        Indexer indexer = new Indexer(expression, indices);
                        indexer.SourceContext = expression.SourceContext;
                        indexer.SourceContext.EndPos = endCol;
                        expression = indexer;
                        break;

                    case Token.LeftParenthesis:
                        if (this.sink != null)
                            this.sink.StartParameters(this.scanner.CurrentSourceContext);
                        SourceContext parenStart = this.scanner.CurrentSourceContext;
                        this.GetNextToken();
                        ExpressionList arguments = this.ParseArgumentList(followersOrContinuers, out endCol);
                        if (expression == null) return null;
                        MethodCall mcall = new MethodCall(expression, arguments);
                        mcall.GiveErrorIfSpecialNameMethod = true;
                        mcall.SourceContext = expression.SourceContext;
                        mcall.SourceContext.EndPos = endCol;
                        SourceContext parenEnd = this.scanner.CurrentSourceContext;
                        parenEnd.StartPos = endCol - 1;
                        parenEnd.EndPos = endCol;
                        if (this.sink != null)
                            this.sink.MatchPair(parenStart, parenEnd);
                        expression = mcall;
                        break;

                    case Token.Dot:
                        expression = this.ParseQualifiedIdentifier(expression, followersOrContinuers);
                        break;

                    default:
                        return expression;
                }
            }
        }

        // TODO: what to do about multi-dimensional arrays?
        private ExpressionList ParseIndexList(TokenSet followers, out int endCol)
        {
            TokenSet followersOrCommaOrRightBracket = followers | Token.Comma | Token.RightBracket;
            ExpressionList result = new ExpressionList();
            if (this.currentToken != Token.RightBracket)
            {
                result.Add(this.ParseExpression(followersOrCommaOrRightBracket));
                while (this.currentToken == Token.Comma)
                {
                    if (this.sink != null)
                        this.sink.NextParameter(this.scanner.CurrentSourceContext);
                    this.GetNextToken();
                    result.Add(this.ParseExpression(followersOrCommaOrRightBracket));
                }
            }
            endCol = this.scanner.endPos;
            if ((this.currentToken == Token.RightBracket) && (this.sink != null))
                this.sink.EndParameters(this.scanner.CurrentSourceContext);
            this.Skip(Token.RightBracket);
            this.SkipTo(followers);
            return result;
        }

        private ExpressionList ParseArgumentList(TokenSet followers, out int endCol)
        {
            TokenSet followersOrCommaOrRightParenthesis = followers | Token.Comma | Token.RightParenthesis;
            ExpressionList result = new ExpressionList();
            if (this.currentToken != Token.RightParenthesis)
            {
                result.Add(this.ParseArgument(followersOrCommaOrRightParenthesis));
                while (this.currentToken == Token.Comma)
                {
                    if (this.sink != null)
                        this.sink.NextParameter(this.scanner.CurrentSourceContext);
                    this.GetNextToken();
                    result.Add(this.ParseArgument(followersOrCommaOrRightParenthesis));
                }
            }
            endCol = this.scanner.endPos;
            if (this.currentToken == Token.RightParenthesis && this.sink != null)
                this.sink.EndParameters(this.scanner.CurrentSourceContext);
            this.Skip(Token.RightParenthesis);
            this.SkipTo(followers);
            return result;
        }

        private Expression ParseArgument(TokenSet followers)
        {
            switch (this.currentToken)
            {
                case Token.Out:
                    SourceContext sctx = this.scanner.CurrentSourceContext;
                    this.GetNextToken();
                    Expression expr = this.ParseExpression(followers);
                    sctx.EndPos = expr.SourceContext.EndPos;
                    return new UnaryExpression(expr, NodeType.OutAddress, sctx);

                default:
                    return this.ParseExpression(followers);
            }
        }

        private Expression ParseQualifiedIdentifier(Expression qualifier, TokenSet followers)
        {
            return this.ParseQualifiedIdentifier(qualifier, followers, false);
        }

        private Expression ParseQualifiedIdentifier(Expression qualifier, TokenSet followers, bool returnNullIfError)
        {
            Debug.Assert(this.currentToken == Token.Dot);
            SourceContext dotContext = this.scanner.CurrentSourceContext;
            Expression result = null;
            Identifier id = null;
            this.GetNextToken();
            if (this.sink != null && this.currentToken == Token.Identifier)
                this.sink.QualifyName(dotContext, this.scanner.GetIdentifier());

            TypeNode tn = Parser.TypeNodeFor(this.currentToken);
            if (tn != null)
            {
                id = this.scanner.GetIdentifier();
                this.GetNextToken();
            }
            else
                id = ParsePrefixedIdentifier();

            result = new QualifiedIdentifier(qualifier, id);

            result.SourceContext = this.scanner.CurrentSourceContext;
            if (qualifier != null)
                result.SourceContext.StartPos = qualifier.SourceContext.StartPos;
            if (this.currentToken == Token.Dot) return this.ParseQualifiedIdentifier(result, followers, returnNullIfError);
            if (returnNullIfError && !followers[this.currentToken]) return null;
            this.SkipTo(followers);
            return result;
        }

        private Identifier ParsePrefixedIdentifier()
        {
            Identifier id = this.scanner.GetIdentifier();
            if (this.currentToken == Token.Identifier && this.scanner.ScanNamespaceSeparator())
            {
                id = ParseNamePart(id);
            }
            this.Skip(Token.Identifier);
            return id;
        }

        private Identifier ParseNamePart(Identifier prefix)
        {
            this.GetNextToken();
            Identifier qid = this.scanner.GetIdentifier();
            qid.Prefix = prefix;
            qid.SourceContext.StartPos = prefix.SourceContext.StartPos;
            return qid;
        }

        private void SetFieldFlags(TokenList modifierTokens, SourceContextList modifierContexts, Field f)
        {
            f.Flags = FieldFlags.Public;

            for (int i = 0, n = modifierTokens.Length; i < n; i++)
            {
                switch (modifierTokens[i])
                {
                    case Token.Static:
                        if ((f.Flags & FieldFlags.Static) != 0)
                            this.HandleError(modifierContexts[i], Error.DuplicateModifier, "static");
                        f.Flags |= FieldFlags.Static;
                        break;

                    default:
                        this.HandleError(f.Name.SourceContext, Error.InvalidModifier, modifierContexts[i].SourceText);
                        break;
                }
            }
        }

        private MethodFlags GetMethodFlags(TokenList modifierTokens, SourceContextList modifierContexts, TypeNode type, ZMethod method)
        {
            MethodFlags result = 0;
            for (int i = 0, n = modifierTokens.Length; i < n; i++)
            {
                switch (modifierTokens[i])
                {
                    case Token.Static:
                        if ((result & MethodFlags.Static) != 0)
                            this.HandleError(modifierContexts[i], Error.DuplicateModifier, "static");
                        result |= MethodFlags.Static;
                        break;

                    case Token.Activate:
                        if (method.Activated)
                            this.HandleError(modifierContexts[i], Error.DuplicateModifier, "activate");
                        method.Activated = true;
                        break;

                    case Token.Atomic:
                        if (method.Atomic)
                            this.HandleError(modifierContexts[i], Error.DuplicateModifier, "atomic");
                        method.Atomic = true;
                        break;

                    default:
                        this.HandleError(method.Name.SourceContext, Error.InvalidModifier, modifierContexts[i].SourceText);
                        break;
                }
            }
            return result | MethodFlags.Public;
        }

        private static TypeNode TypeNodeFor(Token tok)
        {
            switch (tok)
            {
                case Token.Bool: return SystemTypes.Boolean;
                case Token.Byte: return SystemTypes.UInt8;
                case Token.SByte: return SystemTypes.Int8;
                case Token.Short: return SystemTypes.Int16;
                case Token.UShort: return SystemTypes.UInt16;
                case Token.Int: return SystemTypes.Int32;
                case Token.UInt: return SystemTypes.UInt32;
                case Token.Long: return SystemTypes.Int64;
                case Token.ULong: return SystemTypes.UInt64;
                case Token.Float: return SystemTypes.Single;
                case Token.Double: return SystemTypes.Double;
                case Token.Decimal: return SystemTypes.Decimal;
                case Token.Object: return SystemTypes.Object;
                case Token.Void: return SystemTypes.Void;
                case Token.String: return SystemTypes.String;
                default: return null;
            }
        }

        private void HandleError(Error error, params string[] messageParameters)
        {
            ErrorNode enode = new ZingErrorNode(error, messageParameters);
            enode.SourceContext = this.scanner.CurrentSourceContext;
            if (error == Error.ExpectedSemicolon && this.scanner.TokenIsFirstOnLine)
            {
                int i = this.scanner.eolPos;
                if (i > 1)
                    enode.SourceContext.StartPos = i - 2; //Try to have a place for the cursor to hover
                else
                    enode.SourceContext.StartPos = 0;
                enode.SourceContext.EndPos = i;
            }
            this.errors.Add(enode);
        }

        private void HandleError(SourceContext ctx, Error error, params string[] messageParameters)
        {
            if (ctx.Document == null) return;
            ErrorNode enode = new ZingErrorNode(error, messageParameters);
            enode.SourceContext = ctx;
            this.errors.Add(enode);
        }

        private void Skip(Token token)
        {
            if (this.currentToken == token)
                this.GetNextToken();
            else
            {
                switch (token)
                {
                    case Token.Identifier: this.HandleError(Error.ExpectedIdentifier); break;
                    case Token.LeftBrace: this.HandleError(Error.ExpectedLeftBrace); break;
                    case Token.LeftBracket: this.HandleError(Error.ExpectedLeftBracket); break;
                    case Token.LeftParenthesis: this.HandleError(Error.SyntaxError, "("); break;
                    case Token.RightBrace: this.HandleError(Error.ExpectedRightBrace); break;
                    case Token.RightBracket: this.HandleError(Error.ExpectedRightBracket); break;
                    case Token.RightParenthesis: this.HandleError(Error.ExpectedRightParenthesis); break;
                    case Token.Semicolon: this.HandleError(Error.ExpectedSemicolon); break;
                    case Token.Dot: this.HandleError(Error.ExpectedPeriod); break;
                    case Token.StringLiteral: this.HandleError(Error.ExpectedStringLiteral); break;
                    default: this.HandleError(Error.UnexpectedToken, this.scanner.GetTokenSource()); break;
                }
            }
        }

        private void SkipTo(TokenSet followers)
        {
            if (followers[this.currentToken]) return;
            this.HandleError(Error.UnexpectedToken, this.scanner.GetTokenSource());
            do
            {
                this.GetNextToken();
            } while (!followers[this.currentToken]);
        }

        private void SkipTo(TokenSet followers, Error error, params string[] messages)
        {
            if (error != Error.None)
                this.HandleError(error, messages);
            while (!followers[this.currentToken])
                this.GetNextToken();
        }

        private void SkipSemiColon(TokenSet followers)
        {
            if (this.currentToken == Token.Semicolon)
            {
                this.GetNextToken();
            }
            else
            {
                this.Skip(Token.Semicolon);
                while (!this.scanner.TokenIsFirstOnLine && this.currentToken != Token.Semicolon && (this.currentToken != Token.LeftBrace || !followers[Token.LeftBrace]))
                    this.GetNextToken();
                if (this.currentToken == Token.Semicolon)
                    this.GetNextToken();
            }
        }

        /// <summary>
        /// returns true if opnd1 operator1 opnd2 operator2 opnd3 implicitly brackets as opnd1 operator1 (opnd2 operator2 opnd3)
        /// </summary>
        private static bool LowerPriority(Token operator1, Token operator2)
        {
            switch (operator1)
            {
                case Token.Divide:
                case Token.Multiply:
                case Token.Remainder:
                    return false;

                case Token.Plus:
                case Token.Subtract:
                    switch (operator2)
                    {
                        case Token.Divide:
                        case Token.Multiply:
                        case Token.Remainder:
                            return true;

                        default:
                            return false;
                    }
                case Token.LeftShift:
                case Token.RightShift:
                    switch (operator2)
                    {
                        case Token.Divide:
                        case Token.Multiply:
                        case Token.Remainder:
                        case Token.Plus:
                        case Token.Subtract:
                            return true;

                        default:
                            return false;
                    }
                case Token.GreaterThan:
                case Token.GreaterThanOrEqual:
                case Token.LessThan:
                case Token.LessThanOrEqual:
                    switch (operator2)
                    {
                        case Token.Divide:
                        case Token.Multiply:
                        case Token.Remainder:
                        case Token.Plus:
                        case Token.Subtract:
                        case Token.LeftShift:
                        case Token.RightShift:
                            return true;

                        default:
                            return false;
                    }
                case Token.Equal:
                case Token.NotEqual:
                    switch (operator2)
                    {
                        case Token.Divide:
                        case Token.Multiply:
                        case Token.Remainder:
                        case Token.Plus:
                        case Token.Subtract:
                        case Token.LeftShift:
                        case Token.RightShift:
                        case Token.GreaterThan:
                        case Token.GreaterThanOrEqual:
                        case Token.LessThan:
                        case Token.LessThanOrEqual:
                            return true;

                        default:
                            return false;
                    }
                case Token.BitwiseAnd:
                    switch (operator2)
                    {
                        case Token.Divide:
                        case Token.Multiply:
                        case Token.Remainder:
                        case Token.Plus:
                        case Token.Subtract:
                        case Token.LeftShift:
                        case Token.RightShift:
                        case Token.GreaterThan:
                        case Token.GreaterThanOrEqual:
                        case Token.LessThan:
                        case Token.LessThanOrEqual:
                        case Token.Equal:
                        case Token.NotEqual:
                            return true;

                        default:
                            return false;
                    }
                case Token.BitwiseXor:
                    switch (operator2)
                    {
                        case Token.Divide:
                        case Token.Multiply:
                        case Token.Remainder:
                        case Token.Plus:
                        case Token.Subtract:
                        case Token.LeftShift:
                        case Token.RightShift:
                        case Token.GreaterThan:
                        case Token.GreaterThanOrEqual:
                        case Token.LessThan:
                        case Token.LessThanOrEqual:
                        case Token.Equal:
                        case Token.NotEqual:
                        case Token.BitwiseAnd:
                            return true;

                        default:
                            return false;
                    }
                case Token.BitwiseOr:
                    switch (operator2)
                    {
                        case Token.Divide:
                        case Token.Multiply:
                        case Token.Remainder:
                        case Token.Plus:
                        case Token.Subtract:
                        case Token.LeftShift:
                        case Token.RightShift:
                        case Token.GreaterThan:
                        case Token.GreaterThanOrEqual:
                        case Token.LessThan:
                        case Token.LessThanOrEqual:
                        case Token.Equal:
                        case Token.NotEqual:
                        case Token.BitwiseAnd:
                        case Token.BitwiseXor:
                            return true;

                        default:
                            return false;
                    }
                case Token.LogicalAnd:
                    switch (operator2)
                    {
                        case Token.Divide:
                        case Token.Multiply:
                        case Token.Remainder:
                        case Token.Plus:
                        case Token.Subtract:
                        case Token.LeftShift:
                        case Token.RightShift:
                        case Token.GreaterThan:
                        case Token.GreaterThanOrEqual:
                        case Token.LessThan:
                        case Token.LessThanOrEqual:
                        case Token.Equal:
                        case Token.NotEqual:
                        case Token.BitwiseAnd:
                        case Token.BitwiseXor:
                        case Token.BitwiseOr:
                            return true;

                        default:
                            return false;
                    }
                case Token.LogicalOr:
                    switch (operator2)
                    {
                        case Token.Divide:
                        case Token.Multiply:
                        case Token.Remainder:
                        case Token.Plus:
                        case Token.Subtract:
                        case Token.LeftShift:
                        case Token.RightShift:
                        case Token.GreaterThan:
                        case Token.GreaterThanOrEqual:
                        case Token.LessThan:
                        case Token.LessThanOrEqual:
                        case Token.Equal:
                        case Token.NotEqual:
                        case Token.BitwiseAnd:
                        case Token.BitwiseXor:
                        case Token.BitwiseOr:
                        case Token.LogicalAnd:
                            return true;

                        default:
                            return false;
                    }
            }
            Debug.Assert(false);
            return false;
        }

        private static NodeType ConvertToUnaryNodeType(Token op)
        {
            switch (op)
            {
                case Token.Plus: return NodeType.UnaryPlus;
                case Token.Subtract: return NodeType.Neg;
                case Token.LogicalNot: return NodeType.LogicalNot;
                case Token.BitwiseNot: return NodeType.Not;
                default: return NodeType.Nop;
            }
        }

        private static NodeType ConvertToBinaryNodeType(Token op)
        {
            switch (op)
            {
                case Token.Plus: return NodeType.Add;
                case Token.BitwiseAnd: return NodeType.And;
                case Token.BitwiseOr: return NodeType.Or;
                case Token.BitwiseXor: return NodeType.Xor;
                case Token.Divide: return NodeType.Div;
                case Token.Equal: return NodeType.Eq;
                case Token.GreaterThan: return NodeType.Gt;
                case Token.GreaterThanOrEqual: return NodeType.Ge;
                case Token.LeftShift: return NodeType.Shl;
                case Token.LessThan: return NodeType.Lt;
                case Token.LessThanOrEqual: return NodeType.Le;
                case Token.LogicalAnd: return NodeType.LogicalAnd;
                case Token.LogicalOr: return NodeType.LogicalOr;
                case Token.Multiply: return NodeType.Mul;
                case Token.NotEqual: return NodeType.Ne;
                case Token.Remainder: return NodeType.Rem;
                case Token.RightShift: return NodeType.Shr;
                case Token.Subtract: return NodeType.Sub;
                case Token.In: return (NodeType)ZingNodeType.In;
                default: return NodeType.Nop;
            }
        }

        private static readonly TokenSet EndOfFile;
        private static readonly TokenSet TypeDeclarationStart;
        private static readonly TokenSet InterfaceMemberDeclarationStart;
        private static readonly TokenSet StructMemberDeclarationStart;
        private static readonly TokenSet ClassMemberDeclarationStart;
        private static readonly TokenSet PrimaryStart;
        private static readonly TokenSet StatementStart;
        private static readonly TokenSet InfixOperators;
        private static readonly TokenSet JoinPatternStart;

        static Parser()
        {
            EndOfFile = new TokenSet();
            EndOfFile |= Token.EndOfFile;

            TypeDeclarationStart = new TokenSet();
            TypeDeclarationStart |= Token.Interface;
            TypeDeclarationStart |= Token.Class;
            TypeDeclarationStart |= Token.Enum;
            TypeDeclarationStart |= Token.Struct;
            TypeDeclarationStart |= Token.Set;
            TypeDeclarationStart |= Token.Range;
            TypeDeclarationStart |= Token.Chan;
            TypeDeclarationStart |= Token.Array;

            StructMemberDeclarationStart = new TokenSet();
            StructMemberDeclarationStart |= Token.Identifier;
            StructMemberDeclarationStart |= Token.Bool;
            StructMemberDeclarationStart |= Token.Byte;
            StructMemberDeclarationStart |= Token.SByte;
            StructMemberDeclarationStart |= Token.Short;
            StructMemberDeclarationStart |= Token.UShort;
            StructMemberDeclarationStart |= Token.Int;
            StructMemberDeclarationStart |= Token.UInt;
            StructMemberDeclarationStart |= Token.Long;
            StructMemberDeclarationStart |= Token.ULong;
            StructMemberDeclarationStart |= Token.Float;
            StructMemberDeclarationStart |= Token.Double;
            StructMemberDeclarationStart |= Token.Decimal;
            StructMemberDeclarationStart |= Token.Object;
            StructMemberDeclarationStart |= Token.String;

            ClassMemberDeclarationStart = StructMemberDeclarationStart;
            ClassMemberDeclarationStart |= Token.Activate;
            ClassMemberDeclarationStart |= Token.Atomic;
            ClassMemberDeclarationStart |= Token.Static;
            ClassMemberDeclarationStart |= Token.Void;

            InterfaceMemberDeclarationStart = StructMemberDeclarationStart;
            InterfaceMemberDeclarationStart |= Token.Void;

            PrimaryStart = new TokenSet();
            PrimaryStart |= Token.Identifier;
            PrimaryStart |= Token.This;
            PrimaryStart |= Token.New;
            PrimaryStart |= Token.Sizeof;
            PrimaryStart |= Token.Choose;
            PrimaryStart |= Token.Typeof;           // permitted in attributes only
            PrimaryStart |= Token.HexLiteral;
            PrimaryStart |= Token.IntegerLiteral;
            PrimaryStart |= Token.StringLiteral;
            PrimaryStart |= Token.Null;
            PrimaryStart |= Token.False;
            PrimaryStart |= Token.True;
            PrimaryStart |= Token.LeftParenthesis;
            PrimaryStart |= Token.Self;

            StatementStart = new TokenSet();
            StatementStart |= Parser.PrimaryStart;
            StatementStart |= Token.Bool;
            StatementStart |= Token.Byte;
            StatementStart |= Token.SByte;
            StatementStart |= Token.Short;
            StatementStart |= Token.UShort;
            StatementStart |= Token.Int;
            StatementStart |= Token.UInt;
            StatementStart |= Token.Long;
            StatementStart |= Token.ULong;
            StatementStart |= Token.Float;
            StatementStart |= Token.Double;
            StatementStart |= Token.Decimal;
            StatementStart |= Token.Object;
            StatementStart |= Token.String;
            StatementStart |= Token.LeftBrace;
            StatementStart |= Token.Semicolon;
            StatementStart |= Token.If;
            StatementStart |= Token.While;
            StatementStart |= Token.Foreach;
            StatementStart |= Token.Goto;
            StatementStart |= Token.Return;
            StatementStart |= Token.Raise;
            StatementStart |= Token.Try;
            //StatementStart |= Token.With;   //Not really, but helps error recovery
            StatementStart |= Token.Accept;
            StatementStart |= Token.Assert;
            StatementStart |= Token.Assume;
            StatementStart |= Token.Async;
            StatementStart |= Token.Atomic;
            StatementStart |= Token.Select;
            StatementStart |= Token.Send;
            StatementStart |= Token.Trace;
            StatementStart |= Token.Event;
            StatementStart |= Token.LeftBracket;
            StatementStart |= Token.Yield;
            StatementStart |= Token.InvokePlugin;
            StatementStart |= Token.InvokeShed;

            JoinPatternStart = new TokenSet();
            JoinPatternStart |= Token.Timeout;
            JoinPatternStart |= Token.Wait;
            JoinPatternStart |= Token.Receive;
            JoinPatternStart |= Token.Event;
            JoinPatternStart |= Token.LeftBracket;

            InfixOperators = new TokenSet();
            InfixOperators |= Token.Assign;
            InfixOperators |= Token.BitwiseAnd;
            InfixOperators |= Token.BitwiseOr;
            InfixOperators |= Token.BitwiseXor;
            InfixOperators |= Token.BitwiseNot;
            InfixOperators |= Token.Divide;
            InfixOperators |= Token.Equal;
            InfixOperators |= Token.GreaterThan;
            InfixOperators |= Token.GreaterThanOrEqual;
            InfixOperators |= Token.LeftShift;
            InfixOperators |= Token.LessThan;
            InfixOperators |= Token.LessThanOrEqual;
            InfixOperators |= Token.LogicalAnd;
            InfixOperators |= Token.LogicalNot;
            InfixOperators |= Token.LogicalOr;
            InfixOperators |= Token.Multiply;
            InfixOperators |= Token.NotEqual;
            InfixOperators |= Token.Plus;
            InfixOperators |= Token.Remainder;
            InfixOperators |= Token.RightShift;
            InfixOperators |= Token.Subtract;
            InfixOperators |= Token.Arrow;
            InfixOperators |= Token.In;
        }

        private struct TokenSet
        {
            private ulong bits0, bits1;

            public static TokenSet operator |(TokenSet ts, Token t)
            {
                TokenSet result = new TokenSet();
                int i = (int)t;
                if (i < 64)
                {
                    result.bits0 = ts.bits0 | (1ul << i);
                    result.bits1 = ts.bits1;
                }
                else
                {
                    result.bits0 = ts.bits0;
                    result.bits1 = ts.bits1 | (1ul << (i - 64));
                }
                return result;
            }

            public static TokenSet operator |(TokenSet ts1, TokenSet ts2)
            {
                TokenSet result = new TokenSet();
                result.bits0 = ts1.bits0 | ts2.bits0;
                result.bits1 = ts1.bits1 | ts2.bits1;
                return result;
            }

            internal bool this[Token t]
            {
                get
                {
                    int i = (int)t;
                    if (i < 64)
                        return (this.bits0 & (1ul << i)) != 0;
                    else
                        return (this.bits1 & (1ul << (i - 64))) != 0;
                }
                set
                {
                    int i = (int)t;
                    if (i < 64)
                    {
                        if (value)
                            this.bits0 |= (1ul << i);
                        else
                            this.bits0 &= ~(1ul << i);
                    }
                    else
                    {
                        if (value)
                            this.bits1 |= (1ul << (i - 64));
                        else
                            this.bits1 &= ~(1ul << (i - 64));
                    }
                }
            }

            static TokenSet()
            {
                int i = (int)Token.EndOfFile;
                Debug.Assert(0 <= i && i <= 127);
            }
        }
    }

    internal sealed class TokenList
    {
        private Token[] elements;
        private int length = 0;

        internal TokenList()
        {
            this.elements = new Token[4];
        }

        internal TokenList(int capacity)
        {
            this.elements = new Token[capacity];
        }

        internal void Add(Token element)
        {
            int n = this.elements.Length;
            int i = this.length++;
            if (i == n)
            {
                int m = n * 2; if (m < 4) m = 4;
                Token[] newElements = new Token[m];
                for (int j = 0; j < n; j++) newElements[j] = elements[j];
                this.elements = newElements;
            }
            this.elements[i] = element;
        }

        internal int Length
        {
            get { return this.length; }
        }

        internal Token this[int index]
        {
            get
            {
                return this.elements[index];
            }
            set
            {
                this.elements[index] = value;
            }
        }
    }

    internal sealed class SourceContextList
    {
        private SourceContext[] elements;
        private int length = 0;

        internal SourceContextList()
        {
            this.elements = new SourceContext[4];
        }

        internal SourceContextList(int capacity)
        {
            this.elements = new SourceContext[capacity];
        }

        internal void Add(SourceContext element)
        {
            int n = this.elements.Length;
            int i = this.length++;
            if (i == n)
            {
                int m = n * 2; if (m < 4) m = 4;
                SourceContext[] newElements = new SourceContext[m];
                for (int j = 0; j < n; j++) newElements[j] = elements[j];
                this.elements = newElements;
            }
            this.elements[i] = element;
        }

        internal int Length
        {
            get { return this.length; }
        }

        internal SourceContext this[int index]
        {
            get
            {
                return this.elements[index];
            }
            set
            {
                this.elements[index] = value;
            }
        }
    }
}