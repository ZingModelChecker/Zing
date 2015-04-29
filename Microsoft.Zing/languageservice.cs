#if !ReducedFootprint
#if CCINamespace
using Microsoft.Cci;
using CciAuthoringHelper = Microsoft.Cci.AuthoringHelper;
using CciAuthoringScope = Microsoft.Cci.AuthoringScope;
using CciDeclarations = Microsoft.Cci.Declarations;
using CciLanguageService = Microsoft.Cci.LanguageService;
using CciScanner = Microsoft.Cci.Scanner;
using CciSwitch = Microsoft.Cci.Switch;
#else

#endif
//using System.Compiler;

/* LJW
namespace Microsoft.Zing
{
    public sealed class LanguageService : CciLanguageService
    {
        private Scanner scanner;
        private TrivialHashtable scopeFor;

        public LanguageService()
            : base(new ErrorHandler(new ErrorNodeList(0)))
        {
            this.scanner = new Scanner();
        }

        public override CciAuthoringScope GetAuthoringScope()
        {
            return new AuthoringScope(this, new AuthoringHelper(this.errorHandler, this.culture));
        }
        public override System.CodeDom.Compiler.CompilerParameters GetDummyCompilerParameters()
        {
            return new ZingCompilerOptions();
        }
        public override CciScanner GetScanner()
        {
            return this.scanner;
        }
        public override Compilation GetDummyCompilationFor(string fileName)
        {
            string fContents = null;
            if (File.Exists(fileName))
            {
                StreamReader sr = new StreamReader(fileName);
                fContents = sr.ReadToEnd(); sr.Close();
            }
            Compilation compilation = new Compilation();
            //compilation.IsDummy = true;
            compilation.CompilerParameters = this.GetDummyCompilerParameters();
            compilation.TargetModule = new Module();
            DocumentText docText = new DocumentText(new StringSourceText(fContents, true));
            SourceContext sctx = new SourceContext(Compiler.CreateZingDocument(fileName, 0, docText));
            compilation.CompilationUnits = new CompilationUnitList(new CompilationUnitSnippet(new Identifier(fileName), new ParserFactory(), sctx));
            compilation.CompilationUnits[0].Compilation = compilation;
            return compilation;
        }
        public override MemberList GetTypesNamespacesAndPrefixes(Scope scope)
        {
            MemberList result = new MemberList();
            while (scope != null && !(scope is TypeScope)) scope = scope.OuterScope;
            if (scope == null) return result;
            TypeNode currentType = ((TypeScope)scope).Type;
            if (currentType == null || currentType.DeclaringModule == null) return result;
            ErrorHandler errorHandler = new ErrorHandler(new ErrorNodeList(0));
            TrivialHashtable ambiguousTypes = new TrivialHashtable();
            TrivialHashtable referencedLabels = new TrivialHashtable();
            Hashtable exceptionNames = new Hashtable();
            TrivialHashtable symbolicNodes = new TrivialHashtable();
            Looker looker = new Looker(null, errorHandler, null, ambiguousTypes, referencedLabels, exceptionNames, symbolicNodes);
            looker.currentType = currentType;
            looker.currentModule = currentType.DeclaringModule;
            return looker.GetVisibleTypesNamespacesAndPrefixes(scope);
        }
        public override MemberList GetNestedNamespacesAndTypes(Identifier name, Scope scope)
        {
            MemberList result = new MemberList();
            ErrorHandler errorHandler = new ErrorHandler(new ErrorNodeList(0));
            TrivialHashtable scopeFor = new TrivialHashtable();
            TrivialHashtable factoryMap = new TrivialHashtable();
            TrivialHashtable ambiguousTypes = new TrivialHashtable();
            TrivialHashtable referencedLabels = new TrivialHashtable();
            Hashtable exceptionNames = new Hashtable();
            TrivialHashtable symbolicNodes = new TrivialHashtable();
            Looker looker = new Looker(null, errorHandler, null, ambiguousTypes, referencedLabels, exceptionNames, symbolicNodes);
            looker.currentModule = this.currentSymbolTable;
            return looker.GetNestedNamespacesAndTypes(name, scope);
        }
        public override void ParseAndAnalyzeCompilationUnit(string fname, string text, ErrorNodeList errors, Compilation compilation, AuthoringSink asink)
        {
            if (fname == null || text == null || errors == null || compilation == null){Debug.Assert(false); return;}
            CompilationUnitList compilationUnitSnippets = compilation.CompilationUnits;
            if (compilationUnitSnippets == null){Debug.Assert(false); return;}
            //Fix up the CompilationUnitSnippet corresponding to fname with the new source text
            CompilationUnitSnippet cuSnippet = this.GetCompilationUnitSnippet(compilation, fname);
            if (cuSnippet == null) return;
            Compiler compiler = new Compiler();
            cuSnippet.SourceContext.Document = compiler.CreateDocument(fname, 1, new DocumentText(text));
            cuSnippet.SourceContext.EndPos = text.Length;
            //Parse all of the compilation unit snippets
            Module symbolTable = compiler.CreateModule(compilation.CompilerParameters, errors);
            int n = compilationUnitSnippets.Length;
            for (int i = 0; i < n; i++)
            {
                CompilationUnitSnippet compilationUnitSnippet = compilationUnitSnippets[i] as CompilationUnitSnippet;
                if (compilationUnitSnippet == null){Debug.Assert(false); continue;}
                Document doc = compilationUnitSnippet.SourceContext.Document;
                if (doc == null || doc.Text == null){Debug.Assert(false); continue;}
                IParserFactory factory = compilationUnitSnippet.ParserFactory;
                if (factory == null) continue;
                compilationUnitSnippet.Nodes = null;
                compilationUnitSnippet.PreprocessorDefinedSymbols = null;
                IParser p = factory.CreateParser(doc.Name, doc.LineNumber, doc.Text, symbolTable, compilationUnitSnippet == cuSnippet ? errors : new ErrorNodeList(), compilation.CompilerParameters);
                if (p == null){Debug.Assert(false); continue;}
                if (p is ResgenCompilerStub) continue;
                Parser zingParser = p as Parser;
                if (zingParser == null)
                    p.ParseCompilationUnit(compilationUnitSnippet);
                else
                    zingParser.ParseCompilationUnit(compilationUnitSnippet, compilationUnitSnippet != cuSnippet, true, asink);
                StringSourceText stringSourceText = doc.Text.TextProvider as StringSourceText;
                if (stringSourceText != null && stringSourceText.IsSameAsFileContents)
                    doc.Text.TextProvider = new CollectibleSourceText(doc.Name, doc.Text.Length);
            }
            //Construct symbol table for entire project
            ErrorHandler errorHandler = new ErrorHandler(errors);
            TrivialHashtable ambiguousTypes = new TrivialHashtable();
            TrivialHashtable referencedLabels = new TrivialHashtable();
            TrivialHashtable symbolicNodes = new TrivialHashtable();
            Hashtable exceptionNames = new Hashtable();
            TrivialHashtable scopeFor = this.scopeFor = new TrivialHashtable();
            Scoper scoper = new Scoper(scopeFor);
            scoper.currentModule = symbolTable;
            Looker symLooker = new Looker(null, new ErrorHandler(new ErrorNodeList(0)), scopeFor, ambiguousTypes, referencedLabels, exceptionNames, symbolicNodes);
            symLooker.currentAssembly = (symLooker.currentModule = symbolTable) as AssemblyNode;
            Looker looker = new Looker(null, errorHandler, scopeFor, ambiguousTypes, referencedLabels, exceptionNames, symbolicNodes);
            looker.currentAssembly = (looker.currentModule = symbolTable) as AssemblyNode;
            looker.identifierInfos = this.identifierInfos = new NodeList();
            looker.identifierPositions = this.identifierPositions = new Int32List();
            looker.identifierLengths = this.identifierLengths = new Int32List();
            looker.identifierScopes = this.identifierScopes = new ScopeList();
            //      if (compilation.IsDummy){
            //        //This happens when there is no project. In this case, semantic errors should be ignored since the references and options are unknown.
            //        //But proceed with the full analysis anyway so that some measure of Intellisense can still be provided.
            //        errorHandler.Errors = new ErrorNodeList(0);
            //      }
            for (int i = 0; i < n; i++)
            {
                CompilationUnit cUnit = compilationUnitSnippets[i];
                scoper.VisitCompilationUnit(cUnit);
            }
            for (int i = 0; i < n; i++)
            {
                CompilationUnit cUnit = compilationUnitSnippets[i];
                if (cUnit == cuSnippet)
                    looker.VisitCompilationUnit(cUnit); //Uses real error message list and populate the identifier info lists
                else
                    symLooker.VisitCompilationUnit(cUnit); //Errors are discarded
            }
            //Now analyze the given file for errors
            TypeSystem typeSystem = new TypeSystem(errorHandler);
            Resolver resolver = new Resolver(errorHandler, typeSystem, symbolicNodes);
            resolver.currentAssembly = (resolver.currentModule = symbolTable) as AssemblyNode;
            resolver.VisitCompilationUnit(cuSnippet);
            Partitioner partitioner = new Partitioner();
            partitioner.VisitCompilationUnit(cuSnippet);
            Checker checker = new Checker(errorHandler, typeSystem, ambiguousTypes, referencedLabels, symbolicNodes);
            checker.currentAssembly = (checker.currentModule = symbolTable) as AssemblyNode;
            checker.VisitCompilationUnit(cuSnippet);
            compilation.TargetModule = this.currentSymbolTable = symbolTable;
        }
        public override CompilationUnit ParseCompilationUnit(string fname, string source, ErrorNodeList errors, Compilation compilation, AuthoringSink sink)
        {
            Parser p = new Parser(compilation.TargetModule); //This is reallocated by the caller for every call
            CompilationUnit cu = p.ParseCompilationUnit(source, fname, compilation.CompilerParameters, errors, sink, true);
            if (cu != null) cu.Compilation = compilation;
            return cu;
        }
        public override void Resolve(Member unresolvedMember, Member resolvedMember)
        {
            if (unresolvedMember == null || resolvedMember == null) return;
            ErrorHandler errorHandler = new ErrorHandler(new ErrorNodeList(0));
            TrivialHashtable ambiguousTypes = new TrivialHashtable();
            TrivialHashtable referencedLabels = new TrivialHashtable();
            Hashtable exceptionNames = new Hashtable();
            TrivialHashtable symbolicNodes = new TrivialHashtable();
            Looker looker = new Looker(null, errorHandler, this.scopeFor, ambiguousTypes, referencedLabels, exceptionNames, symbolicNodes);
            looker.currentAssembly = (looker.currentModule = this.currentSymbolTable) as AssemblyNode;
            TypeNode currentType = resolvedMember.DeclaringType;
            if (resolvedMember is TypeNode && unresolvedMember.DeclaringType != null &&
                ((TypeNode)resolvedMember).FullName == unresolvedMember.DeclaringType.FullName)
            {
                unresolvedMember.DeclaringType = (TypeNode)resolvedMember;
                currentType = (TypeNode)resolvedMember;
                looker.scope = this.scopeFor[resolvedMember.UniqueKey] as Scope;
            }
            else if (resolvedMember.DeclaringType != null)
            {
                unresolvedMember.DeclaringType = resolvedMember.DeclaringType;
                looker.scope = this.scopeFor[resolvedMember.DeclaringType.UniqueKey] as Scope;
            }
            else if (resolvedMember.DeclaringNamespace != null)
            {
                unresolvedMember.DeclaringNamespace = resolvedMember.DeclaringNamespace;
                looker.scope = this.scopeFor[resolvedMember.DeclaringNamespace.UniqueKey] as Scope;
            }
            if (looker.scope == null) return;
            looker.currentType = currentType;
            looker.identifierInfos = this.identifierInfos = new NodeList();
            looker.identifierPositions = this.identifierPositions = new Int32List();
            looker.identifierLengths = this.identifierLengths = new Int32List();
            looker.identifierScopes = this.identifierScopes = new ScopeList();
            looker.Visit(unresolvedMember);
            //Walk IR inferring types and resolving overloads
            Resolver resolver = new Resolver(errorHandler, new TypeSystem(errorHandler), new TrivialHashtable());
            resolver.currentAssembly = (resolver.currentModule = this.currentSymbolTable) as AssemblyNode;
            resolver.currentType = currentType;
            resolver.Visit(unresolvedMember);
        }
        public override void Lookup(CompilationUnit partialCompilationUnit)
        {
            if (partialCompilationUnit == null){Debug.Assert(false); return;}
            TrivialHashtable scopeFor = new TrivialHashtable();
            Scoper scoper = new Scoper(scopeFor);
            scoper.currentModule = this.currentSymbolTable;
            scoper.VisitCompilationUnit(partialCompilationUnit);

            ErrorHandler errorHandler = new ErrorHandler(new ErrorNodeList(0));
            TrivialHashtable ambiguousTypes = new TrivialHashtable();
            TrivialHashtable referencedLabels = new TrivialHashtable();
            Hashtable exceptionNames = new Hashtable();
            TrivialHashtable symbolicNodes = new TrivialHashtable();
            Looker looker = new Looker(null, errorHandler, scopeFor, ambiguousTypes, referencedLabels, exceptionNames, symbolicNodes);
            looker.currentAssembly = (looker.currentModule = this.currentSymbolTable) as AssemblyNode;
            looker.identifierInfos = this.identifierInfos = new NodeList();
            looker.identifierPositions = this.identifierPositions = new Int32List();
            looker.identifierLengths = this.identifierLengths = new Int32List();
            looker.identifierScopes = this.identifierScopes = new ScopeList();
            looker.VisitCompilationUnit(partialCompilationUnit);
        }
    }

    internal class AuthoringScope : System.Compiler.AuthoringScope
    {
        public AuthoringScope(LanguageService languageService, AuthoringHelper helper)
            : base(languageService, helper)
        {
        }

        public override MemberList GetMembers(int line, int col, Node node, Scope scope)
        {
            return base.GetMembers(line, col, node, scope);
        }

        //
        // This method works exactly like base.GetMembers except that we don't recurse
        // on the base class of "type". This eliminates some noise in VS code-sense.
        //
        public override void GetMembers(TypeNode type, MemberList members, bool staticMembersWanted, bool showPrivate, bool showFamily, bool showInternal)
        {
            if (type == null || members == null) return;
            TypeUnion tu = type as TypeUnion;
            if (tu != null)
            {
                TypeNodeList tlist = tu.Types;
                for (int i = 0, n = (tlist == null ? 0 : tlist.Length); i < n; i++)
                {
                    TypeNode t = tlist[i] as TypeNode;
                    if (t == null) continue;
                    this.GetMembers(t, members, staticMembersWanted, showPrivate, showFamily, showInternal);
                }
                return;
            }
            TypeAlias ta = type as TypeAlias;
            if (ta != null)
            {
                this.GetMembers(ta.AliasedType, members, staticMembersWanted, showPrivate, showFamily, showInternal);
                return;
            }
            MemberList typeMembers = type.Members;
            for (int i = 0, k = typeMembers == null ? 0 : typeMembers.Count; i < k; i++)
            {
                Member mem = typeMembers[i];
                if (mem == null) continue;
                if (staticMembersWanted)
                {
                    if (!mem.IsStatic) continue;
                }
                else
                {
                    if (mem.IsStatic) continue;
                }
                if (mem.IsCompilerControlled) continue;
                if (mem.IsPrivate && !showPrivate) continue;
                if ((mem.IsFamily || mem.IsFamilyAndAssembly) && !showFamily) continue;
                if ((mem.IsAssembly || mem.IsFamilyOrAssembly) && !showInternal) continue;
                if (mem.IsSpecialName && !(mem is InstanceInitializer)) continue;
                if (mem.IsAnonymous)
                {
                    TypeNode t = ((Field)mem).Type;
                    this.GetMembers(t, members, staticMembersWanted, showPrivate, showFamily, showInternal);
                }
                else
                {
                    members.Add(mem);
                }
            }
        }
    }

#if UNUSED
    internal sealed class Declarations : System.Compiler.Declarations
    {
        public Declarations(MemberList memberList, System.Compiler.AuthoringHelper helper) :
            base(memberList, helper)
        {
        }
        public override bool IsCommitChar(string textSoFar, char ch)
        {
            return ! (Char.IsLetterOrDigit(ch) || ch == '_');
        }
    }
#endif

    internal sealed class AuthoringHelper : System.Compiler.AuthoringHelper
    {
        public AuthoringHelper(ErrorHandler errorHandler, CultureInfo culture)
            : base(errorHandler, culture)
        {
        }

        public override String GetDescription(Member member, int overloads)
        {
            Field f = member as Field;
            if (f != null && f.DeclaringType == null && f.IsCompilerControlled) return "";
            return base.GetDescription(member, overloads);
        }
    }
}

*/

#endif