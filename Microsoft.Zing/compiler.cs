using System;
using System.Collections;
using System.Collections.Specialized;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.SymbolStore;
using System.IO;
using System.Threading;
using R=System.Reflection;
using CS=Microsoft.Comega;

namespace Microsoft.Zing
{
    /// <summary>
    /// This class is used by CodeDom clients to generate assemblies from CodeDom trees with
    /// HScript snippets. ASP .NET with script blocks written in HScript is an example of such
    /// client. Note that this works even though there is no HScript source representation for
    /// most CodeDom constructs. The parts that are not HScript snippets are never translated
    /// to source code if the ICodeCompiler interface is used, as is the case for ASP .NET.
    /// </summary>
    [System.ComponentModel.DesignerCategory("code")]
    public class ZingCodeProvider: CodeDomProvider
    {

        public ZingCodeProvider()
        {
        }

        [SuppressMessage("Microsoft.Security", "CA2123:OverrideLinkDemandsShouldBeIdenticalToBase")]
        [Obsolete("TODO")]
        public override ICodeGenerator CreateGenerator()
        {
            return new Compiler();
        }
        [SuppressMessage("Microsoft.Security", "CA2123:OverrideLinkDemandsShouldBeIdenticalToBase")]
        [Obsolete("TODO")]
        public override ICodeCompiler CreateCompiler()
        {
            return new Compiler();
        }
        [SuppressMessage("Microsoft.Security", "CA2123:OverrideLinkDemandsShouldBeIdenticalToBase")]
        public override string FileExtension
        {
            get {return "zing";}
        }
        [SuppressMessage("Microsoft.Security", "CA2123:OverrideLinkDemandsShouldBeIdenticalToBase")]
        [Obsolete("TODO")]
        public override ICodeParser CreateParser()
        {
            throw new NotImplementedException();
        }
        [SuppressMessage("Microsoft.Security", "CA2123:OverrideLinkDemandsShouldBeIdenticalToBase")]
        public override LanguageOptions LanguageOptions
        {
            get {return LanguageOptions.None;}
        }
    }

    /// <summary>
    /// This class orchestrates compilation for the the command line compiler as well as any
    /// CodeDom client that uses the ICodeCompiler interface. Most of the work is done in the
    /// base class provided by the CCI code generation framework. The methods provided by this
    /// class serve only to specialize the behavior of the base class, mainly by means of factory
    /// methods that instantiate language specific extensions of various base classes in the framework.
    /// </summary>
    [SuppressMessage("Microsoft.Naming", "CA1724:TypeNamesShouldNotMatchNamespaces")]
    sealed public class Compiler : System.Compiler.Compiler, ICodeGenerator
    {
        public Compiler()
        {
        }

        private static Type[] RequiredTypes = 
        {
            //typeof(Microsoft.Zing.StateImpl),
            typeof(System.Diagnostics.Debug),
        };

        private CompilerParameters Options; // LJW: added this. It had been in previous versions of CCI but was then removed
        private Module targetModule;

        #region Framework Overrides
        [SuppressMessage("Microsoft.Security", "CA2122:DoNotIndirectlyExposeMethodsWithLinkDemands")]
        public override void SetOutputFileName(CompilerParameters options, string fileName)
        {
            if (options == null){Debug.Assert(false); return;}
            CompilerOptions coptions = options as CompilerOptions;
            if (coptions != null && (coptions.OutputPath == null || coptions.OutputPath.Length == 0) && options.TempFiles != null && (options.TempFiles.TempDir != Directory.GetCurrentDirectory() || !options.TempFiles.KeepFiles))
            {
                //Get here when invoked from typical CodeDom client, such as ASP .Net
                fileName = options.TempFiles.AddExtension(this.GetTargetExtension(options).Substring(1));
            }
            else
            {
                //Get here when invoked from a command line compiler, or a badly behaved CodeDom client
                if (fileName == null) fileName = "noname";
                string ext = Path.GetExtension(fileName);
                if (string.Compare(ext, this.GetTargetExtension(options), true, System.Globalization.CultureInfo.InvariantCulture) != 0)
                    fileName = Path.GetDirectoryName(fileName) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(fileName) + this.GetTargetExtension(options);
            }
            if (options.OutputAssembly == null || options.OutputAssembly.Length == 0)
                options.OutputAssembly = Path.GetFileNameWithoutExtension(fileName);
            if (coptions != null && (coptions.OutputPath == null || coptions.OutputPath.Length == 0) )
                coptions.OutputPath = Path.GetDirectoryName(fileName);
        }

        [SuppressMessage("Microsoft.Security", "CA2122:DoNotIndirectlyExposeMethodsWithLinkDemands")]
        public override CompilerResults CompileAssemblyFromDomBatch(CompilerParameters options, CodeCompileUnit[] compilationUnits, ErrorNodeList errorNodes)
        {
            if (options == null){Debug.Assert(false); return null;}
            this.Options = options;
            int n = compilationUnits == null ? 0 : compilationUnits.Length;
            if (options.OutputAssembly == null || options.OutputAssembly.Length == 0)
            {
                for (int i = 0; i < n; i++)
                {
                    CodeSnippetCompileUnit csu = compilationUnits[i] as CodeSnippetCompileUnit;
                    if (csu == null || csu.LinePragma == null || csu.LinePragma.FileName == null) continue;
                    this.SetOutputFileName(options, csu.LinePragma.FileName);
                    break;
                }
            }
            CompilerResults results = new CompilerResults(options.TempFiles);
            AssemblyNode assem = this.CreateAssembly(options, errorNodes);
            Compilation compilation = new Compilation(assem, new CompilationUnitList(n), options, this.GetGlobalScope(assem));
            CodeDomTranslator cdt = new CodeDomTranslator();
            SnippetParser sp = new SnippetParser(this, assem, errorNodes, options);
            for (int i = 0; i < n; i++)
            {
                CompilationUnit cu = cdt.Translate(this, compilationUnits[i], assem, errorNodes);
                sp.Visit(cu);
                compilation.CompilationUnits.Add(cu);
                cu.Compilation = compilation;
            }
            this.CompileParseTree(compilation, errorNodes);
            this.ProcessErrors(options, results, errorNodes);
            if (results.NativeCompilerReturnValue == 0)
                this.SetEntryPoint(compilation, results);
            // The following line is different from the base class method...
            this.SaveOrLoadAssembly(this.targetModule as AssemblyNode, options, results, errorNodes);
            return results;
        }

        [SuppressMessage("Microsoft.Security", "CA2122:DoNotIndirectlyExposeMethodsWithLinkDemands")]
        public override CompilerResults CompileAssemblyFromFileBatch(CompilerParameters options, string[] fileNames, ErrorNodeList errorNodes, bool canUseMemoryMap)
        {
            if (options == null){Debug.Assert(false); return null;}
            int n = fileNames.Length;
            if (options.OutputAssembly == null || options.OutputAssembly.Length == 0)
            {
                for (int i = 0; i < n; i++)
                {
                    if (fileNames[i] == null) continue;
                    this.SetOutputFileName(options, fileNames[i]);
                    break;
                }
            }
            CompilerResults results = new CompilerResults(options.TempFiles);
            AssemblyNode assem = this.CreateAssembly(options, errorNodes);
            Compilation compilation = new Compilation(assem, new CompilationUnitList(n), options, this.GetGlobalScope(assem));
            SnippetParser sp = new SnippetParser(this, assem, errorNodes, options);
            for (int i = 0; i < n; i++)
            {
                string fileName = fileNames[i];
                if (fileName == null) continue;
                DocumentText text = this.CreateDocumentText(fileName, results, options, errorNodes, canUseMemoryMap);
                CompilationUnitSnippet cu = this.CreateCompilationUnitSnippet(fileName, 1, text, compilation);
                sp.Visit(cu);
                compilation.CompilationUnits.Add(cu);
                cu.Compilation = compilation;
            }            
            
            this.CompileParseTree(compilation, errorNodes);

            this.ProcessErrors(options, results, errorNodes);
            if (results.NativeCompilerReturnValue == 0)
                this.SetEntryPoint(compilation, results);
            // The following line is different from the base class method...
            this.SaveOrLoadAssembly(this.targetModule as AssemblyNode, options, results, errorNodes);
            return results;
        }

        [SuppressMessage("Microsoft.Security", "CA2122:DoNotIndirectlyExposeMethodsWithLinkDemands")]
        public override void CompileParseTree(Compilation compilation, ErrorNodeList errorNodes)
        {
            TrivialHashtable ambiguousTypes = new TrivialHashtable();
			TrivialHashtable scopeFor = new TrivialHashtable();
            TrivialHashtable referencedLabels = new TrivialHashtable();
            Hashtable exceptionNames = new Hashtable();
            ErrorHandler errorHandler = new ErrorHandler(errorNodes);
            string target = "";
            ZingCompilerOptions zoptions = compilation.CompilerParameters as ZingCompilerOptions; 
            if (zoptions != null && zoptions.DumpSource) 
            {
                target = compilation.CompilerParameters.OutputAssembly;
                if (string.IsNullOrEmpty(target))
                {
                    target = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar;
                }
                string output = Path.GetDirectoryName(target);
                if (string.IsNullOrEmpty(output)) output = Directory.GetCurrentDirectory();
                target = Path.GetFileNameWithoutExtension(target);
            }

            if (this.Options == null)
                this.Options = compilation.CompilerParameters;

			//Attach scopes to namespaces and types so that forward references to base types can be looked up in the appropriate namespace scope
			Scoper scoper = new Scoper(scopeFor);
			scoper.VisitCompilation(compilation);

            //Walk IR looking up names
            TypeSystem typeSystem = new TypeSystem(errorHandler); 
            Looker looker = new Looker(compilation.GlobalScope, errorHandler, scopeFor, typeSystem, // LJW: added typeSystem
				ambiguousTypes, referencedLabels, exceptionNames);
            looker.VisitCompilation(compilation);

            //Walk IR inferring types and resolving overloads
            Resolver resolver = new Resolver(errorHandler, typeSystem);
            resolver.VisitCompilation(compilation);

            Checker checker = new Checker(errorHandler, typeSystem, scopeFor, ambiguousTypes, referencedLabels); // LJW: added scopeFor
            checker.VisitCompilation(compilation);

            // Walk the Zing IR splicing it into our code-gen templates
            Splicer splicer = new Splicer(this.Options, referencedLabels, exceptionNames);
            CompilationUnit cuMerged = splicer.CodeGen(compilation);

            //
            // If the 'dumpSource' option is given, then we decompile the IR to C# and write this
            // to a file.
            //
            if (zoptions != null && zoptions.DumpSource)
            {
                // Once the codegen is done and we have the IR for the generated code, we use the
                // decompiler to get C# back out, and hand this to the standard compiler. Later,
                // we'll replace this with the X# normalizer and avoid going in and out of source
                // code (except for compiler debugging).
                string tempSrc;
                string tempName = compilation.CompilerParameters.OutputAssembly + ".cs";
                Decompiler decompiler = new Decompiler();
                tempSrc = decompiler.Decompile(cuMerged);

                StreamWriter sw = File.CreateText(tempName);
                sw.Write(tempSrc);
                sw.Close();
            }

            if (zoptions != null && zoptions.DumpLabels)
            {
                string tempName = compilation.CompilerParameters.OutputAssembly + ".labels";
                StreamWriter sw = File.CreateText(tempName);
                sw.Write(splicer.LabelString);
                sw.Close();
            }

            for (int i = 0; i < RequiredTypes.Length ;i++)
            {
                R.Assembly asm = R.Assembly.GetAssembly(RequiredTypes[i]);
                if (!compilation.CompilerParameters.ReferencedAssemblies.Contains(asm.Location))
                    compilation.CompilerParameters.ReferencedAssemblies.Add(asm.Location);
            }

            // The if-statement added by Jiri Adamek
            // It loads a native ZOM assembly if used
            if (zoptions != null && zoptions.ZomAssemblyName != null)
            {
                compilation.CompilerParameters.ReferencedAssemblies.Add(zoptions.ZomAssemblyName);
            }

            CS.Compiler csCompiler = new CS.Compiler();

            // We have to create a new module to pull in references from the
            // assemblies we just added above.
            compilation.TargetModule = this.targetModule =
                csCompiler.CreateModule(compilation.CompilerParameters, errorNodes);

            // Our one top-level type must be added to the module's type list
            compilation.TargetModule.Types.Add(((Namespace)cuMerged.Nodes[0]).NestedNamespaces[0].Types[0]);

            // The restorer patches the DeclaringModule field of our types
            Restorer restorer = new Restorer(compilation.TargetModule);
            restorer.VisitCompilationUnit(cuMerged);

            // Replace the Zing compilation unit with our generated code before invoking
            // the Spec# back end.
            compilation.CompilationUnits = new CompilationUnitList(cuMerged);

            foreach (CompilationUnit cunit in compilation.CompilationUnits)
                cunit.Compilation = compilation;

            // For retail builds, disable the time-consuming definite-assignment checks
            // in the Spec# compiler.
            if (!this.Options.IncludeDebugInformation)
                compilation.CompilationUnits[0].PreprocessorDefinedSymbols["NODEFASSIGN"] = "true";

            // Let the Spec# back end process the IR from here
            csCompiler.CompileParseTree(compilation, errorNodes);
        }

        public override Document CreateDocument(string fileName, int lineNumber, DocumentText text)
        {
            return new Document(fileName, lineNumber, text, SymDocumentType.Text, typeof(DebuggerLanguage).GUID, SymLanguageVendor.Microsoft);
        }
        public static Document CreateZingDocument(string fileName, int lineNumber, DocumentText text)
        {
            return new Document(fileName, lineNumber, text, SymDocumentType.Text, typeof(DebuggerLanguage).GUID, SymLanguageVendor.Microsoft);
        }
        public override Document CreateDocument(string fileName, int lineNumber, string text)
        {
            return new Document(fileName, lineNumber, text, SymDocumentType.Text, typeof(DebuggerLanguage).GUID, SymLanguageVendor.Microsoft);
        }
        public override IParser CreateParser(string fileName, int lineNumber, DocumentText text, Module symbolTable, ErrorNodeList errorNodes, CompilerParameters options)
        {
            Document document = this.CreateDocument(fileName, lineNumber, text);
            return new Parser(document, errorNodes, symbolTable, options as ZingCompilerOptions);
        }
        public override CompilerOptions CreateCompilerOptions()
        {
            return new ZingCompilerOptions();
        }
        /// <summary>
        /// Parses all of the CompilationUnitSnippets in the given compilation, ignoring method bodies. Then resolves all type expressions.
        /// The resulting types can be retrieved from the module in compilation.TargetModule. The base types, interfaces and 
        /// member signatures will all be resolved and on an equal footing with imported, already compiled modules and assemblies.
        /// </summary>
        public override void ConstructSymbolTable(Compilation compilation, ErrorNodeList errors)
        {
            if (compilation == null){Debug.Assert(false); return;}
            Module symbolTable = compilation.TargetModule = this.CreateModule(compilation.CompilerParameters, errors, compilation);
            TrivialHashtable scopeFor = new TrivialHashtable();
            Scoper scoper = new Scoper(scopeFor);
            scoper.currentModule = symbolTable;
            ErrorHandler errorHandler = new ErrorHandler(errors);
            TypeSystem typeSystem = new TypeSystem(errorHandler); // LJW: added typeSystem
            Looker looker = new Looker(this.GetGlobalScope(symbolTable), errorHandler, scopeFor, typeSystem);
            looker.currentAssembly = (looker.currentModule = symbolTable) as AssemblyNode;
            looker.ignoreMethodBodies = true;
            compilation.GlobalScope = this.GetGlobalScope(symbolTable);

            CompilationUnitList sources = compilation.CompilationUnits;
            if (sources == null) {Debug.Assert(false); return;}
            int n = sources.Count;
            for (int i = 0; i < n; i++)
            {
                CompilationUnitSnippet compilationUnitSnippet = sources[i] as CompilationUnitSnippet;
                if (compilationUnitSnippet == null){Debug.Assert(false); continue;}
                compilationUnitSnippet.ChangedMethod = null;
                Document doc = compilationUnitSnippet.SourceContext.Document;
                if (doc == null || doc.Text == null){Debug.Assert(false); continue;}
                IParserFactory factory = compilationUnitSnippet.ParserFactory;
                if (factory == null){Debug.Assert(false); return;}
                IParser p = factory.CreateParser(doc.Name, doc.LineNumber, doc.Text, symbolTable, errors, compilation.CompilerParameters);
                if (p is ResgenCompilerStub) continue;
                if (p == null){Debug.Assert(false); continue;}
                Parser zingParser = p as Parser;
                if (zingParser == null)
                    p.ParseCompilationUnit(compilationUnitSnippet);
                else
                    zingParser.ParseCompilationUnit(compilationUnitSnippet, true, true, null);
                StringSourceText stringSourceText = doc.Text.TextProvider as StringSourceText;
                if (stringSourceText != null && stringSourceText.IsSameAsFileContents)
                    doc.Text.TextProvider = new CollectibleSourceText(doc.Name, doc.Text.Length);
                else if (doc.Text.TextProvider != null)
                    doc.Text.TextProvider.MakeCollectible();
            }
            CompilationUnitList compilationUnits = new CompilationUnitList();
            for (int i = 0; i < n; i++)
            {
                CompilationUnit cUnit = sources[i];
                compilationUnits.Add(scoper.VisitCompilationUnit(cUnit));
            }
            for (int i = 0; i < n; i++)
            {
                CompilationUnit cUnit = compilationUnits[i];
                if (cUnit == null) continue;
                looker.VisitCompilationUnit(cUnit);
            }
        }

        #endregion

        #region CodeDomCodeGenerator

        bool ICodeGenerator.Supports(GeneratorSupport supports) 
        {
            return false;
        }
        //These methods are used to translate CodeDom trees into target language source code
        //These are not implemented for Zing since there is no easy mapping from CodeDom to Zing
        void ICodeGenerator.GenerateCodeFromType(CodeTypeDeclaration e, TextWriter w, CodeGeneratorOptions o)
        {
            throw new NotImplementedException();
        }
        void ICodeGenerator.GenerateCodeFromExpression(CodeExpression e, TextWriter w, CodeGeneratorOptions o)
        {
            throw new NotImplementedException();
        }
        void ICodeGenerator.GenerateCodeFromCompileUnit(CodeCompileUnit e, TextWriter w, CodeGeneratorOptions o)
        {
            throw new NotImplementedException();
        }
        void ICodeGenerator.GenerateCodeFromNamespace(CodeNamespace e, TextWriter w, CodeGeneratorOptions o)
        {
            throw new NotImplementedException();
        }
        void ICodeGenerator.GenerateCodeFromStatement(CodeStatement e, TextWriter w, CodeGeneratorOptions o)
        {
            throw new NotImplementedException();
        }
        bool ICodeGenerator.IsValidIdentifier(string value)
        {
            throw new NotImplementedException();
        }
        void ICodeGenerator.ValidateIdentifier(string value)
        {
            throw new NotImplementedException();
        }
        string ICodeGenerator.CreateEscapedIdentifier(string value)
        {
            throw new NotImplementedException();
        }
        string ICodeGenerator.CreateValidIdentifier(string value)
        {
            throw new NotImplementedException();
        }
        string ICodeGenerator.GetTypeOutput(CodeTypeReference type)
        {
            throw new NotImplementedException();
        }

        #endregion
        
        public static void GenerateCodeFromIR(Node node, TextWriter w, CodeGeneratorOptions o)
        {
            ZingDecompiler decompiler = new ZingDecompiler(o);

            decompiler.Decompile(node, w);
        }

        [SuppressMessage("Microsoft.Security", "CA2122:DoNotIndirectlyExposeMethodsWithLinkDemands")]
        public static void GenerateCodeFileFromIR(Node node, string path,
            string indent, bool blankLines, string bracingStyle )
        {
            CodeGeneratorOptions o = new CodeGeneratorOptions();
            o.IndentString = indent;
            o.BlankLinesBetweenMembers = blankLines;
            o.BracingStyle = bracingStyle;
            StreamWriter sw = File.CreateText(path);
            GenerateCodeFromIR(node, sw, o);
            sw.Close();
        }

        [SuppressMessage("Microsoft.Security", "CA2122:DoNotIndirectlyExposeMethodsWithLinkDemands")]
        public void ResolveIR(Compilation compilation, ErrorNodeList errorNodes)
        {
            TrivialHashtable ambiguousTypes = new TrivialHashtable();
            TrivialHashtable scopeFor = new TrivialHashtable();
            TrivialHashtable referencedLabels = new TrivialHashtable();
            Hashtable exceptionNames = new Hashtable();
            ErrorHandler errorHandler = new ErrorHandler(errorNodes);
            string target = "";
            ZingCompilerOptions zoptions = compilation.CompilerParameters as ZingCompilerOptions; 
            if (zoptions != null && zoptions.DumpSource) 
            {
                target = compilation.CompilerParameters.OutputAssembly;
                if (string.IsNullOrEmpty(target))
                {
                    target = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar;
                }
                string output = Path.GetDirectoryName(target);
                if (string.IsNullOrEmpty(output)) output = Directory.GetCurrentDirectory();
                target = Path.GetFileNameWithoutExtension(target);
            }

            if (this.Options == null)
                this.Options = compilation.CompilerParameters;

            //Attach scopes to namespaces and types so that forward references to base types can be looked up in the appropriate namespace scope
            Scoper scoper = new Scoper(scopeFor);
            scoper.VisitCompilation(compilation);

            //Walk IR looking up names
            TypeSystem typeSystem = new TypeSystem(errorHandler);   
            Looker looker = new Looker(compilation.GlobalScope, errorHandler, scopeFor, typeSystem, // LJW: added typeSystem
				ambiguousTypes, referencedLabels, exceptionNames);
            looker.VisitCompilation(compilation);

            //Walk IR inferring types and resolving overloads
            Resolver resolver = new Resolver(errorHandler, typeSystem);
            resolver.VisitCompilation(compilation);

            Checker checker = new Checker(errorHandler, typeSystem, scopeFor, ambiguousTypes, referencedLabels); // LJW: added scopeFor
            checker.VisitCompilation(compilation);
        }

        #region CompilerParameters

        public override string[] ParseCompilerParameters(CompilerParameters options, string[] arguments, ErrorNodeList errors)
        {
            string[] result = base.ParseCompilerParameters(options, arguments, errors, true);
            //CompilerOptions coptions = options as CompilerOptions;
            return result;
        }
        public override bool ParseCompilerOption(CompilerParameters options, string arg, ErrorNodeList errors)
        {
            bool result = base.ParseCompilerOption(options, arg, errors);
            ZingCompilerOptions zoptions = options as ZingCompilerOptions;
            if (!result)
            {
                //See if Zing-specific option
                CompilerOptions coptions = options as CompilerOptions;
                if (coptions == null) return false;
                int n = arg.Length;
                if (n <= 1) return false;
                char ch = arg[0];
                if (ch != '/' && ch != '-') return false;
                ch = arg[1];
                switch(ch)
                {
                    case 'p':
                        if (this.ParseName(arg, "preemptive", "preemptive"))
                        {
                            ZingCompilerOptions.IsPreemtive = true;
                            return true;
                        }
                        break;
                    case 'n':
                        string warningnumber;
                        warningnumber = this.ParseNamedArgument(arg, "nowarning", "nowarning");
                        if (warningnumber != null)
                        {
                            zoptions.Warningnumber.Add(int.Parse(warningnumber));
                            return true;
                        }
                        break;
                    case 'd':
                        if (this.ParseName(arg, "dumpsource", "dumpsource"))
                        {
                            if (zoptions == null) break;
                            zoptions.DumpSource = true;
                            return true;
                        }
                        if (this.ParseName(arg, "dumplabels", "dumplabels"))
                        {
                            if (zoptions == null) break;
                            zoptions.DumpLabels = true;
                            return true;
                        }
                        break;
                    case 'u':
                        if (this.ParseName(arg, "unchecked", "unchecked"))
                        {
                            if (zoptions == null) break;
                            zoptions.CheckArithmetic = false;
                            return true;
                        }
                        break;
                    case 'z':
                        /* Added by Jiri Adamek;
                         * "zom" (optional) name argument defines the name of a .NET library containing 
                         * classes that are "linked" to the Zing runtime
                         */
                        string zomAssemblyName = this.ParseNamedArgument(arg, "zom", "zom");
                        if (zomAssemblyName != null)
                        {
                            if (zoptions == null) break;
                            if (zoptions.ZomAssemblyName == null)
                                zoptions.ZomAssemblyName = zomAssemblyName;
                            else 
                                System.Console.WriteLine("warning: Only single ZOM assembly is currently supported: '" + zomAssemblyName + "' is ignored");                          
                                // TODO: handle the warning using the standard CCI error handling infrastructure
                            return true;
                        }
                        break;
                }
                return false; //TODO: give an error message
            }
            return result;
        }

        #endregion CompilerParameters
    }

    

    
    public class ZingCompilerOptions : CompilerOptions
    {
     /*
     * Added for handling pre-emptive and non-preemptive compile time options
     */
        private static bool isPreemtive = false;
        public static bool IsPreemtive
        {
            get { return ZingCompilerOptions.isPreemtive; }
            set { ZingCompilerOptions.isPreemtive = value; }
        }

        private System.Collections.Generic.List<int> warningnumber;

        public System.Collections.Generic.List<int> Warningnumber
        {
            get { return warningnumber; }
            set { warningnumber = value; }
        }
        


        private bool dumpSource;
        public bool DumpSource
        {
            get { return this.dumpSource; }
            set { this.dumpSource = value; }
        }

        private bool dumpLabels;
        public bool DumpLabels
        {
            get { return this.dumpLabels; }
            set { this.dumpLabels = value; }
        }

        private bool checkArithmetic = true;
        public bool CheckArithmetic
        {
            get { return checkArithmetic; }
            set { checkArithmetic = value; }
        }

        /* Added by Jiri Adamek;
         * zomAssemblyName is the name of .NET assembly that is 
         * "linked" to the Zing runtime; from the point of view of a Zing code,
         * it defines a set of classes that can be instantiated and used as common Zing classes
         */
        private string zomAssemblyName = null;
        public string ZomAssemblyName
        {
            get { return zomAssemblyName; }
            set { zomAssemblyName = value; }
        }

		public ZingCompilerOptions()
            : base()
		{
            AddZingRuntime();
            this.Warningnumber = new System.Collections.Generic.List<int>();
		}

		public ZingCompilerOptions(CompilerOptions options)
			: base(options)
        {
            AddZingRuntime();

			ZingCompilerOptions zoptions = options as ZingCompilerOptions;
			if (zoptions == null) return;
			this.DumpSource = zoptions.DumpSource;
            this.Warningnumber = new System.Collections.Generic.List<int>();
        }

        public override string GetOptionHelp() 
        {
            System.Resources.ResourceManager rm = new System.Resources.ResourceManager("Microsoft.Zing.ErrorMessages", typeof(ZingErrorNode).Module.Assembly);
            return rm.GetString("Usage");
        }

        [SuppressMessage("Microsoft.Security", "CA2122:DoNotIndirectlyExposeMethodsWithLinkDemands")]
        private void AddZingRuntime()
        {
            // We always include a reference to the Zing runtime (for the attributes)
            R.Assembly asm = R.Assembly.GetAssembly(typeof(Microsoft.Zing.State));
            if (!this.ReferencedAssemblies.Contains(asm.Location))
                this.ReferencedAssemblies.Add(asm.Location);
        }
	}
}
