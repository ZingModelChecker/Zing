using System;
using System.Diagnostics;
using System.CodeDom.Compiler;
using System.Collections;
using System.Compiler;
using System.Diagnostics.SymbolStore;
using System.IO;
using System.Text;
using Z=Microsoft.Zing;

class main
{
    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    [MTAThread]
    static int Main(string[] args)
    {
        int rc = 0;
        int n = (args == null) ? 0 : args.Length;
        bool testsuite = false;
        for (int i = 0; i < n; i++)
        {
            string arg = args[i];
            char ch = arg[0];
            if (ch != '@')
            {
                if (Path.GetExtension(arg) == ".suite") 
                    testsuite = true;
            }
        }
        CompilerOptions options = new Microsoft.Zing.ZingCompilerOptions();
        options.TempFiles = new TempFileCollection(Directory.GetCurrentDirectory(), true);
        options.GenerateExecutable = false;
        options.CompileAndExecute = testsuite;
        ErrorNodeList errors = new ErrorNodeList(0);
        Compiler compiler = new Microsoft.Zing.Compiler();
        string[] fileNames = compiler.ParseCompilerParameters(options, args, errors);
        if (options.DisplayCommandLineHelp)
        {
            Console.WriteLine("\nZing compiler options:");
            Console.WriteLine(options.GetOptionHelp());
            return 0;
        }

        if (testsuite)
        {
            // fileNames has expanded wildcards.
            foreach (string file in fileNames) 
            {
                main.RunSuite(file);
            }
            return 0;
        }    

        string fatalErrorString = null;
        n = errors.Count;

        
        for (int i = 0; i < n; i++)
        {
            ErrorNode e = errors[i];
            if (e == null) continue;
            rc++;
            if (fatalErrorString == null) fatalErrorString = compiler.GetFatalErrorString();
            Console.Write(fatalErrorString, e.Code.ToString("0000"));
            Console.WriteLine(e.GetMessage());
        }
        if (rc > 0) return 1;

        CompilerResults results;
        if (fileNames.Length == 1)
            results = compiler.CompileAssemblyFromFile(options, fileNames[0]);
        else
            results = compiler.CompileAssemblyFromFileBatch(options, fileNames);

        string errorString = null;
        string warningString = null;
        
        Console.WriteLine();

        foreach (CompilerError e in results.Errors)
        {
            if(e.IsWarning)
            {
                Console.ForegroundColor = ConsoleColor.Gray;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
            }

            if (e.FileName != null && e.FileName.Length > 0)
            {
                Console.Write(e.FileName);
                Console.Write('(');
                Console.Write(e.Line);
                Console.Write(',');
                Console.Write(e.Column);
                Console.Write("): ");
            }
            if (e.IsWarning)
            {
                
                if (e.ErrorNumber.Length == 4)
                {
                    if (warningString == null) warningString = compiler.GetWarningString();
                    Console.Write(warningString, e.ErrorNumber);
                }
                
            }
            else
            {
                if (e.ErrorNumber.Length == 4)
                {
                    rc++; //REVIEW: include related location errors?
                    if (errorString == null) errorString = compiler.GetErrorString();
                    Console.Write(errorString, e.ErrorNumber);
                }
            }
            Console.WriteLine(e.ErrorText);
        }
        Console.ResetColor();

        if (rc > 0) return 1;
        if ((rc = results.NativeCompilerReturnValue) == 0 && options.CompileAndExecute && 
            results.CompiledAssembly != null && results.CompiledAssembly.EntryPoint != null)
        {
            if (results.CompiledAssembly.EntryPoint.GetParameters().Length == 0)
                results.CompiledAssembly.EntryPoint.Invoke(null, null);
            else
                results.CompiledAssembly.EntryPoint.Invoke(null, new object[]{new string[0]});
        }      
        if (rc > 0) return 1;
        return 0;
    }

    private static void RunSuite(string suiteName)
    {
        StringBuilder source = null;
        StringBuilder expectedOutput = null;
        StringBuilder actualOutput = null;
        ArrayList compilerParameters = null;
        ArrayList testCaseParameters = null;
        int errors = 0;
        main.assemblyNameCounter = 0;
        try
        {
            StreamReader instream = File.OpenText(suiteName);
            int ch = instream.Read();
            int line = 1;
            while (ch >= 0)
            {
                if (ch == '`')
                {
                    ch = instream.Read();
                    while (ch == '/')
                    {
                        //compiler parameters
                        StringBuilder cParam = new StringBuilder();
                        do
                        {
                            cParam.Append((char)ch);
                            ch = instream.Read();
                        }while(ch != '/' && ch != 0 && ch != 10 && ch != 13);
                        for (int i = cParam.Length-1; i >= 0; i--)
                        {
                            if (cParam[i] != ' ') break;
                            cParam.Length = i;
                        }
                        if (compilerParameters == null) compilerParameters = new ArrayList();
                        compilerParameters.Add(cParam.ToString());
                    }
                    if (ch == 13) 
                    {
                        line++;
                        ch = instream.Read();
                        if (ch == 10) ch = instream.Read();
                    }         
                }
                if (ch == ':')
                {
                    ch = instream.Read();
                    while (ch == '=')
                    {
                        //test case parameters
                        StringBuilder tcParam = new StringBuilder();
                        ch = instream.Read(); //discard =
                        while(ch != '=' && ch != 0 && ch != 10 && ch != 13)
                        {
                            tcParam.Append((char)ch);
                            ch = instream.Read();
                        }
                        for (int i = tcParam.Length-1; i >= 0; i--)
                        {
                            if (tcParam[i] != ' ') break;
                            tcParam.Length = i;
                        }
                        if (testCaseParameters == null) testCaseParameters = new ArrayList();
                        testCaseParameters.Add(tcParam.ToString());
                    }
                    if (ch == 13) 
                    {
                        ch = instream.Read();
                        line++;
                        if (ch == 10) ch = instream.Read();          
                    }
                }
                source = new StringBuilder();
                while (ch >= 0 && ch != '`')
                {
                    source.Append((char)ch);
                    ch = instream.Read();
                    if (ch == 13) line++;
                }
                if (ch < 0) break;
                ch = instream.Read();
                if (ch == 13) 
                {
                    line++;
                    ch = instream.Read();
                    if (ch == 10) ch = instream.Read();
                }        
                int errLine = line;
                expectedOutput = new StringBuilder();
                while (ch >= 0 && ch != '`')
                {
                    expectedOutput.Append((char)ch);
                    ch = instream.Read();
                    if (ch == 13) line++;
                }
                ch = instream.Read();
                if (ch == 13) 
                { 
                    ch = instream.Read(); 
                    line++; 
                    if (ch == 10) ch = instream.Read();
                }                
                actualOutput = new StringBuilder();
                TextWriter savedOut = Console.Out;
                try
                {
                    int returnCode = RunTest(source.ToString(), actualOutput, compilerParameters, testCaseParameters);
                    if (returnCode != 0)
                        actualOutput.Append("Non zero return code: "+returnCode);
                }
                catch(Exception e)
                {
                    actualOutput.Append(e.Message);
                    actualOutput.Append((char)13);
                    actualOutput.Append((char)10);
                }
                compilerParameters = null;
                testCaseParameters = null;
                Console.SetOut(savedOut);
                if (!expectedOutput.ToString().Equals(actualOutput.ToString()))
                {
                    if (errors++ == 0) Console.WriteLine(suiteName+" failed");
                    Console.WriteLine("source({0}):", errLine);
                    if (source != null)
                        Console.WriteLine(source);
                    Console.WriteLine("actual output:");
                    Console.WriteLine(actualOutput);
                    Console.WriteLine("expected output:");
                    if (expectedOutput != null)
                        Console.WriteLine(expectedOutput);
                }
            }
            instream.Close();
            if (errors == 0)
                Console.WriteLine(suiteName+" passed");
            else
                Console.WriteLine(suiteName+" had "+errors+ (errors > 1 ? " failures" : " failure"));
        }
        catch
        {
            Console.WriteLine(suiteName+" failed");
            Console.WriteLine("source:");
            if (source != null)
                Console.WriteLine(source);
            Console.WriteLine("actual output:");
            Console.WriteLine(actualOutput);
            Console.WriteLine("expected output:");
            if (expectedOutput != null)
                Console.WriteLine(expectedOutput);
        }
    }
  
    static int assemblyNameCounter = 0;
    private static int RunTest(string test, StringBuilder actualOutput, ArrayList compilerParameters, ArrayList testCaseParameters)
    {
        Console.SetOut(new StringWriter(actualOutput));
        Compiler compiler = new Microsoft.Zing.Compiler();
        CompilerParameters options = new Microsoft.Zing.ZingCompilerOptions();
        if (compilerParameters != null)
        {
            ErrorNodeList compilerParameterErrors = new ErrorNodeList(0);
            compiler.ParseCompilerParameters(options, (string[])compilerParameters.ToArray(typeof(string)), compilerParameterErrors, false);
            for (int i = 0, n = compilerParameterErrors.Count; i < n; i++)
            {
                ErrorNode err = compilerParameterErrors[i];
                Console.WriteLine(err.GetMessage());
            }
        }
        options.OutputAssembly = "assembly for test case "+main.assemblyNameCounter++ + ".dll";
        options.GenerateExecutable = false;
        options.GenerateInMemory = true;
        CompilerResults results = compiler.CompileAssemblyFromSource(options, test);
        foreach (CompilerError e in results.Errors)
        {
            Console.Write('(');
            Console.Write(e.Line);
            Console.Write(',');
            Console.Write(e.Column);
            Console.Write("): ");
            string warningString = null;
            string errorString = null;
            if (e.IsWarning)
            {
                if (e.ErrorNumber.Length == 4)
                {
                    if (warningString == null) warningString = compiler.GetWarningString();
                    Console.Write(warningString, e.ErrorNumber);
                }
            }
            else
            {
                if (e.ErrorNumber.Length == 4)
                {
                    if (errorString == null) errorString = compiler.GetErrorString();
                    Console.Write(errorString, e.ErrorNumber);
                }
            }
            Console.WriteLine(e.ErrorText);
        }
        if (results.NativeCompilerReturnValue != 0) return 0;
        object returnVal = null;
        try {
            //
            // We implement a "standard" execution environment for
            // test cases. This is simply a random execution in which
            // "trace" events are written to the standard output.
            //
            Z.State s = Z.State.Load(results.CompiledAssembly);

            while (!s.IsTerminal) 
            {
                foreach (Z.ZingEvent e in s.GetEvents())
                {
                    if (e is Z.TraceEvent)
                        Console.WriteLine(e.ToString());
                }

                s = s.RunRandom();
            }

            if (s.Type == Z.StateType.Error) {
                Console.WriteLine("erroneous node");
                Console.WriteLine(s.ToString());
            }

            // we remain silent for failedassumptions and normal endings
        }
        catch(System.Reflection.TargetInvocationException e)
        {
            throw e.InnerException;
        }
        if (returnVal is int) return (int)returnVal;
        return 0;
    }
}
