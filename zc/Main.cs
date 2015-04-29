using System;
using System.CodeDom.Compiler;
using System.Compiler;
using System.IO;

internal class main
{
    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    [MTAThread]
    private static int Main(string[] args)
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
            //nowarning
            var warningno = (options as Microsoft.Zing.ZingCompilerOptions).Warningnumber;
            if (e.IsWarning && warningno.Contains(int.Parse(e.ErrorNumber)))
                continue;

            if (e.IsWarning)
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
                results.CompiledAssembly.EntryPoint.Invoke(null, new object[] { new string[0] });
        }
        if (rc > 0) return 1;
        return 0;
    }
}