#if !ReducedFootprint

using System;
using System.Compiler;
using System.Diagnostics;
using System.Globalization;
using System.IO;

//using CS=Microsoft.Comega; LJW
//using CSOPTIONS=Microsoft.Comega.ComegaCompilerOptions; LJW

namespace Microsoft.Zing
{
    /// <summary>
    /// Summary description for ZTemplateParser.
    /// </summary>
    public class ZTemplateParser
    {
        // Static methods only - private construct prevents class activation
        public ZTemplateParser(Module module)
        {
            this.module = module;
        }

        private Module module;

        // private static CompilationUnit cuExpressions;

        private CompilationUnit cuStatements;
        private CompilationUnit cuExprs;

        public CompilationUnit ParseBaseTemplate(string templateName, System.Reflection.Assembly assembly)
        {
            //System.IO.Stream templateStream = System.Reflection.Assembly.GetCallingAssembly().GetManifestResourceStream(templateName);
            System.IO.Stream templateStream = assembly.GetManifestResourceStream(templateName);
            System.IO.StreamReader reader = new StreamReader(templateStream, true);
            string source = reader.ReadToEnd();

            Compiler zingCompiler = new Microsoft.Zing.Compiler();
            CompilerOptions options = new Microsoft.Zing.ZingCompilerOptions();
            ErrorNodeList errors = new ErrorNodeList(0);
            Module m = zingCompiler.CreateModule(options, errors);
            Parser parser = new Parser(m);

            CompilationUnit cu = parser.ParseCompilationUnit(source, templateName, options, errors, null, true);
            if (errors.Count > 0)
            {
                Debug.Assert(false, "ZTemplateParser Error: " +
                    errors[0].GetMessage(CultureInfo.CurrentUICulture));
                return null;
            }
            else
            {
                Restorer restorer = new Restorer(this.module);
                restorer.VisitCompilationUnit(cu);
                return cu;
            }
        }

        public bool ParseStatementTemplate(string templateName, System.Reflection.Assembly assembly)
        {
            this.cuStatements = ParseBaseTemplate(templateName, assembly);
            return (this.cuStatements != null);
        }

        public bool ParseExpressionTemplate(string templateName, System.Reflection.Assembly assembly)
        {
            this.cuExprs = ParseBaseTemplate(templateName, assembly);
            return (this.cuExprs != null);
        }

        public Expression GetExpressionTemplate(string name)
        {
            TypeNode exprClass = ((Namespace)cuExprs.Nodes[0]).Types[0];
            for (int i = 0; i < exprClass.Members.Count; i++)
            {
                Field f = exprClass.Members[i] as Field;
                if (f != null && f.Name.Name == name)
                {
                    Duplicator duplicator = new Duplicator(module, null);
                    duplicator.SkipBodies = false;
                    return duplicator.VisitExpression(f.Initializer);
                }
            }
            throw new ArgumentException(string.Format(CultureInfo.CurrentUICulture,
                "Expression template '{0}' not found", name));
        }

        public Method GetMethodTemplate(string name)
        {
            for (int i = 0, n = ((Namespace)cuStatements.Nodes[0]).Types[0].Members.Count; i < n; i++)
            {
                Method m = ((Namespace)cuStatements.Nodes[0]).Types[0].Members[i] as Method;

                if (m != null && m.Name.Name == name)
                {
                    Duplicator duplicator = new Duplicator(module, null);
                    duplicator.SkipBodies = false;
                    return (Method)duplicator.Visit(m);
                }
            }
            throw new ArgumentException(string.Format(CultureInfo.CurrentUICulture,
                "Method template '{0}' not found", name));
        }

        public Statement GetStatementTemplate(string name)
        {
            for (int i = 0, n = ((Namespace)cuStatements.Nodes[0]).Types[0].Members.Count; i < n; i++)
            {
                Method m = ((Namespace)cuStatements.Nodes[0]).Types[0].Members[i] as Method;

                if (m != null && m.Name.Name == name)
                {
                    Duplicator duplicator = new Duplicator(module, null);
                    duplicator.SkipBodies = false;
                    StatementList stmtList = duplicator.VisitStatementList(m.Body.Statements);
                    return stmtList[0];
                }
            }
            throw new ArgumentException(string.Format(CultureInfo.CurrentUICulture,
                "Statement template '{0}' not found", name));
        }

        public StatementList GetStatementsTemplate(string name)
        {
            for (int i = 0, n = ((Namespace)cuStatements.Nodes[0]).Types[0].Members.Count; i < n; i++)
            {
                Method m = ((Namespace)cuStatements.Nodes[0]).Types[0].Members[i] as Method;

                if (m != null && m.Name.Name == name)
                {
                    Duplicator duplicator = new Duplicator(module, null);
                    duplicator.SkipBodies = false;
                    return duplicator.VisitStatementList(m.Body.Statements);
                }
            }
            throw new ArgumentException(string.Format(CultureInfo.CurrentUICulture,
                "Statement template '{0}' not found", name));
        }

        public static TypeNode GetTypeTemplateByName(CompilationUnit template, string name)
        {
            for (int i = 0, n = ((Namespace)template.Nodes[0]).Types.Count; i < n; i++)
            {
                TypeNode tn = ((Namespace)template.Nodes[0]).Types[i];

                if (tn.Name.Name == name)
                    return tn;
            }
            throw new ArgumentException("Type node '" + name + "' not found");
        }

        /*
        public static Expression GetExpressionTemplate(string name)
        {
            for (int i=0, n = ((Namespace)cuExpressions.Nodes[0]).NestedNamespaces[0].Types[0].Members.Count; i < n ;i++)
            {
                Field f = ((Namespace)cuExpressions.Nodes[0]).NestedNamespaces[0].Types[0].Members[i] as Field;

                if (f != null && f.Name.Name == name)
                {
                    CS.Duplicator duplicator = new CS.Duplicator(module, null);
                    duplicator.SkipBodies = false;

                    Expression expr = duplicator.VisitExpression(f.Initializer);

                    return expr;
                }
            }
            throw new ArgumentException(string.Format("Expression template '{0}' not found", name));
        }

        public static TypeNode GetTypeTemplateByName(params object[] definitions)
        {
            CompilationUnit template;

            string name = (string)definitions[0];

            template = cuParts;

            for (int i=0, n = ((Namespace)template.Nodes[0]).NestedNamespaces[0].Types[0].Members.Count; i < n ;i++)
            {
                Member m = ((Namespace)template.Nodes[0]).NestedNamespaces[0].Types[0].Members[i];

                if (! (m is TypeNode))
                    continue;

                TypeNode tn = (TypeNode) m;

                if (tn.Name.Name == name)
                {
                    CS.Duplicator duplicator = new CS.Duplicator(module, null);
                    duplicator.SkipBodies = false;
                    TypeNode dupClass = duplicator.VisitTypeNode(tn);

                    return dupClass;
                }
            }
            Debug.Assert(false);
            throw new ApplicationException("Type node '" + name + "' not found");
        }

        public static Member GetMemberByName(MemberList members, string name)
        {
            for (int i=0, n = Members.Count; i < n ;i++)
            {
                if (members[i] != null && members[i].Name.Name == name)
                    return members[i];
            }

            throw new ApplicationException(string.Format("Member '{0}' not found", name));
        }

        public static int GetMemberIndexByName(MemberList members, string name)
        {
            for (int i=0, n = Members.Count; i < n ;i++)
            {
                if (members[i] != null && members[i].Name.Name == name)
                    return i;
            }

            throw new ApplicationException(string.Format("Member '{0}' not found", name));
        }
        */
    }
}

#endif