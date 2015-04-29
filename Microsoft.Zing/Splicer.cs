using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Compiler;
using System.Diagnostics;
using System.Diagnostics.SymbolStore;
using System.IO;
using CS = Microsoft.Comega;
using CSOPTIONS = Microsoft.Comega.ComegaCompilerOptions;

namespace Microsoft.Zing
{
    internal class Splicer
    {
        public Splicer(CompilerParameters options, TrivialHashtable referencedLabels, Hashtable exceptionNames)
        {
            this.options = options;
            this.referencedLabels = referencedLabels;
            this.exceptionNames = exceptionNames;
            choosableTypes = new Hashtable();
            sourceDocuments = new Hashtable();
            ZingCompilerOptions zOptions = (ZingCompilerOptions)options;
            if (zOptions.DumpLabels)
                labelString = new System.Text.StringBuilder();
        }

        private System.Text.StringBuilder labelString;

        public System.Text.StringBuilder LabelString
        {
            get { return labelString; }
        }

        private void AddToLabelString(string methodName)
        {
            labelString.Append(methodName + "\n");
            foreach (DictionaryEntry de in basicBlockToLabel)
            {
                BasicBlock block = (BasicBlock)de.Key;
                string label = (string)de.Value;
                labelString.Append(block.Name + " ");
                labelString.Append(label + "\n");
            }
            basicBlockToLabel = null;
        }

        private Hashtable basicBlockToLabel;

        public void AddBlockLabel(BasicBlock block, string label)
        {
            if (basicBlockToLabel != null && !basicBlockToLabel.ContainsKey(block))
            {
                // Debug.Assert(!basicBlockToLabel.ContainsKey(block));
                basicBlockToLabel[block] = label;
            }
        }

        private Hashtable exceptionNames;
        internal TrivialHashtable referencedLabels;

        internal ZMethod currentMethod;
        private CompilerParameters options;
        private Class appClass;
        private Compilation cZing;
        private CompilationUnit cuBase;
        private Hashtable sourceDocuments;
        private Hashtable choosableTypes;
        private uint nextTypeId = 1;
        private TypeExpression ZingPtrType = new TypeExpression(new QualifiedIdentifier(new Identifier("Z"), new Identifier("Pointer")));
        private TypeExpression ZingSymType = new TypeExpression(new QualifiedIdentifier(new Identifier("Y"), new Identifier("Variable")));

        private bool IsPredefinedType(TypeNode t)
        {
            return (t == SystemTypes.Int8) || (t == SystemTypes.UInt8) ||
                   (t == SystemTypes.Int16) || (t == SystemTypes.UInt16) ||
                   (t == SystemTypes.Int32) || (t == SystemTypes.UInt32) ||
                   (t == SystemTypes.Int64) || (t == SystemTypes.UInt64) ||
                   (t == SystemTypes.Single) || (t == SystemTypes.Double) ||
                   (t == SystemTypes.Boolean) || (t == SystemTypes.Decimal) ||
                   (t == SystemTypes.String);
        }

        public CompilationUnit CodeGen(Compilation compilation)
        {
            try
            {
                // Debugging message
                /*
                for (int c = 0, nc = compilation.CompilationUnits.Length; c < nc; c++)
                {
                    Namespace ns = (Namespace)compilation.CompilationUnits[c].Nodes[0];
                    Console.WriteLine("X" + ns.Name.ToString() + "X");

                    for (int i = 0, n = ns.Types.Length; i < n; i++)
                    {
                        TypeNode tn = (TypeNode)ns.Types[i];

                        Console.WriteLine("\t" + tn.Name.ToString());
                    }
                }
                 */
                // END

                this.cZing = compilation;
                Templates.module = compilation.TargetModule;
                Templates.InitializeTemplates();

                // Scan the Zing AST for interesting high-level information.
                MemberList globals = CollectGlobals();
                TypeNodeList classes = CollectClasses();

                // Compile the "base" template and get the interesting nodes from it.
                cuBase = Templates.GetApplicationTemplate(compilation.TargetModule, options, globals.Count > 0, HeapUsed());

                Debug.Assert(((Namespace)cuBase.Nodes[0]).Types.Count == 0);
                Debug.Assert(((Namespace)cuBase.Nodes[0]).NestedNamespaces.Count == 1);
                Debug.Assert(((Namespace)cuBase.Nodes[0]).NestedNamespaces[0].Types.Count == 1);

                appClass = (Class)((Namespace)cuBase.Nodes[0]).NestedNamespaces[0].Types[0];

                // Begin transforming the output

                SetSourceStrings(compilation);
                SetExceptionList();

                // for each Zing type definition, call a helper to splice it into the
                // generated compilation unit.

                for (int c = 0, nc = cZing.CompilationUnits.Count; c < nc; c++)
                {
                    for (int i = 0, n = ((Namespace)cZing.CompilationUnits[c].Nodes[0]).Types.Count; i < n; i++)
                    {
                        TypeNode tn = (TypeNode)((Namespace)cZing.CompilationUnits[c].Nodes[0]).Types[i];

                        if (tn is Interface)
                            GenerateInterface((Interface)tn);
                        else if (tn is Class)
                            GenerateClass((Class)tn);
                        else if (tn is EnumNode)
                            GenerateEnum((EnumNode)tn);
                        else if (tn is Range)
                            GenerateRange((Range)tn);
                        else if (tn is Set)
                            GenerateSet((Set)tn);
                        else if (tn is Chan)
                            GenerateChan((Chan)tn);
                        else if (tn is Struct)
                            GenerateStruct((Struct)tn);
                        else if (tn is ZArray)
                            GenerateArray((ZArray)tn);
                        else
                            throw new ApplicationException("Unknown Zing type: " + tn.GetType().ToString());
                    }
                }

                GenerateTypeChoiceHelper();

                // NOTE: this must be done after processing the classes because we turn off
                // the "static" flag on the globals during processing which confuses GenerateClass.
                ProcessGlobals(globals);

                return cuBase;
            }
            finally
            {
                Templates.module = null;
                Templates.ReleaseTemplates();
            }
        }

        #region Classes (+ methods & globals)

        private void GenerateInterface(Interface x)
        {
            TypeNode newClass = Templates.GetTypeTemplateByName("Interface");
            Replacer.Replace(newClass, newClass.Name, x.Name);
            TypeNode createMethods = (TypeNode)Templates.GetMemberByName(newClass.Members, "CreateMethods");

            for (int i = 0; i < x.Members.Count; i++)
            {
                ZMethod zMethod = x.Members[i] as ZMethod;
                Debug.Assert(zMethod != null);
                Class methodClass = GenerateInterfaceMethod(zMethod);
                newClass.Members.Add(methodClass);
                methodClass.DeclaringType = newClass;
                methodClass.Flags = (methodClass.Flags & ~TypeFlags.VisibilityMask) | TypeFlags.NestedFamORAssem;

                TypeNode tn = Templates.GetTypeTemplateByName("InterfaceExtras");
                Method member = (Method)Templates.GetMemberByName(tn.Members, "__CreateInterfaceMethod");
                member.Name = new Identifier("Create" + methodClass.Name.Name);
                Replacer.Replace(member, new Identifier("__InterfaceMethod"), methodClass.Name);
                member.DeclaringType = createMethods;
                createMethods.Members.Add(member);
            }

            // Add the emitted class to our Zing application class
            InstallType(newClass);
        }

        private void GenerateClass(Class c)
        {
            // The following code added by Jiri Adamek
            // Do not generate any code for native ZOM classes (they were manually written
            // and their code is placed in another assmbly)
            if (c is NativeZOM) return;
            // END of added code

            TypeNode newClass = Templates.GetTypeTemplateByName("Class");
            if (c.Interfaces != null)
            {
                for (int i = 0, n = c.Interfaces.Count; i < n; i++)
                {
                    string iname = c.Interfaces[i].Name.Name;
                    QualifiedIdentifier id = new QualifiedIdentifier(new Identifier(iname), new Identifier("CreateMethods"));
                    newClass.Interfaces.Add(new InterfaceExpression(id));
                }
            }

            Method writer = (Method)Templates.GetMemberByName(newClass.Members, "WriteString");
            Method traverser = (Method)Templates.GetMemberByName(newClass.Members, "TraverseFields");

            // Replace all references to the class name
            Replacer.Replace(newClass, newClass.Name, c.Name);
            SetTypeId(newClass);

            Block cloneFields = new Block();
            cloneFields.Statements = new StatementList();

            Method getValue = (Method)Templates.GetMemberByName(newClass.Members, "GetValue");
            Method setValue = (Method)Templates.GetMemberByName(newClass.Members, "SetValue");
            System.Compiler.Switch switchGetValue =
                (System.Compiler.Switch)Templates.GetStatementTemplate("GetFieldInfoSwitch");
            getValue.Body.Statements.Add(switchGetValue);
            System.Compiler.Switch switchSetValue =
                (System.Compiler.Switch)Templates.GetStatementTemplate("SetFieldInfoSwitch");
            setValue.Body.Statements.Add(switchSetValue);

            // Transfer non-static fields to the emitted class
            for (int i = 0, n = c.Members.Count; i < n; i++)
            {
                Field f = c.Members[i] as Field;

                if (f != null && f.Type != null && !f.IsStatic)
                {
                    // Clone the field since we might tinker with it
                    Field newField = (Field)f.Clone();
                    // change name of the field, so that the accessor can be named appropriately
                    newField.Name = new Identifier("priv_" + f.Name.Name);

                    if (GetTypeClassification(f.Type) == TypeClassification.Heap)
                        newField.Type = this.ZingPtrType;
                    else if (!IsPredefinedType(f.Type))
                        newField.Type = new TypeExpression(new QualifiedIdentifier(
                            new Identifier("Application"), f.Type.Name), f.Type.SourceContext);

                    if (newField.Initializer != null)
                    {
                        // Move the initialization to our constructor.
                        Expression initializer = newField.Initializer;
                        newField.Initializer = null;

                        Statement initStmt = Templates.GetStatementTemplate("InitComplexInstanceField");
                        Replacer.Replace(initStmt, "_FieldName", newField.Name);
                        Normalizer normalizer = new Normalizer(false);
                        Replacer.Replace(initStmt, "_expr", normalizer.VisitFieldInitializer(initializer));
                        Method ctor = (Method)newClass.Members[0];
                        Debug.Assert(ctor.Parameters.Count == 1);
                        ctor.Body.Statements.Add(initStmt);
                    }

                    Identifier idFieldName = new Identifier("id_" + f.Name.Name);
                    Field idf = new Field(newClass, null, FieldFlags.Public | FieldFlags.Static,
                        idFieldName,
                        SystemTypes.Int32, null);
                    idf.Initializer = new Literal(i, SystemTypes.Int32);
                    SwitchCase getCase = ((System.Compiler.Switch)Templates.GetStatementTemplate("GetFieldInfoCase")).Cases[0];
                    Replacer.Replace(getCase, "_fieldId", new Literal(i, SystemTypes.Int32));
                    Replacer.Replace(getCase, "_fieldName", new Identifier(newField.Name.Name));
                    switchGetValue.Cases.Add(getCase);

                    SwitchCase setCase = ((System.Compiler.Switch)Templates.GetStatementTemplate("SetFieldInfoCase")).Cases[0];
                    Replacer.Replace(setCase, "_fieldId", new Literal(i, SystemTypes.Int32));
                    Replacer.Replace(setCase, "_fieldName", new Identifier(newField.Name.Name));
                    TypeExpression tn = newField.Type as TypeExpression;
                    Replacer.Replace(setCase, "_fieldType", tn != null ? tn.Expression : new Identifier(newField.Type.Name.Name));
                    switchSetValue.Cases.Add(setCase);

                    newClass.Members.Add(newField);
                    newField.DeclaringType = newClass;
                    newClass.Members.Add(idf);
                    idf.DeclaringType = newClass;

                    // add property for the field
                    Property accessor = GetFieldAccessorProperty(f.Type, newField.Type, f.Name, newField.Name, idFieldName);
                    newClass.Members.Add(accessor);
                    accessor.DeclaringType = newClass;
                    if (accessor.Getter != null)
                    {
                        newClass.Members.Add(accessor.Getter);
                        accessor.Getter.DeclaringType = newClass;
                    }
                    if (accessor.Setter != null)
                    {
                        newClass.Members.Add(accessor.Setter);
                        accessor.Setter.DeclaringType = newClass;
                    }

                    writer.Body.Statements.Add(GetWriterStatement("this", f.Type, newField.Name));

                    traverser.Body.Statements.Add(GetTraverserStatement("this", f.Type, newField.Name));
                    /*if(GetTypeClassification(f.Type) == TypeClassification.Heap)
                    {
                        refTraverser.Body.Statements.Add(GetTraverserStatement("this", f.Type, newField.Name));
                    }
                    */

                    Statement cloneStmt = GetCloneStatement(newField.Name);
                    cloneFields.Statements.Add(cloneStmt);
                }

                ZMethod zMethod = c.Members[i] as ZMethod;
                if (zMethod != null)
                {
                    InterfaceList xs = FindMatchingInterfaces(c, zMethod);
                    Interface x = null;
                    if (xs.Count != 0)
                    {
                        // TODO: Handle the case when one method implements methods declared in multiple interfaces
                        Debug.Assert(xs.Count == 1);
                        x = xs[0];
                    }
                    Class methodClass = GenerateClassMethod(zMethod, x);
                    newClass.Members.Add(methodClass);
                    methodClass.DeclaringType = newClass;
                    methodClass.Flags = (methodClass.Flags & ~TypeFlags.VisibilityMask) | TypeFlags.NestedFamORAssem;

                    if (x != null)
                    {
                        TypeNode tn = Templates.GetTypeTemplateByName("ClassExtras");
                        Method member = (Method)Templates.GetMemberByName(tn.Members, "__CreateInterfaceMethod");
                        member.Name = new Identifier("Create" + methodClass.Name.Name);
                        member.DeclaringType = newClass;
                        Replacer.Replace(member,
                            new Identifier("__InterfaceMethod"),
                            new QualifiedIdentifier(x.Name, methodClass.Name));
                        Replacer.Replace(member,
                            new Identifier("__ClassMethod"),
                            new QualifiedIdentifier(c.Name, methodClass.Name));
                        newClass.Members.Add(member);
                    }
                }
            }

            // Splice the cloning assignment statements into the class's Clone method
            // at the appropriate place.
            Method cloner = (Method)Templates.GetMemberByName(newClass.Members, "Clone");
            Replacer.Replace(cloner.Body, "cloneFields", cloneFields);

            // Add the emitted class to our Zing application class
            InstallType(newClass);
        }

        private Property GetThisAccessorProperty(TypeNode type, Identifier name, Expression privExpr)
        {
            Property accessor = Templates.GetPropertyTemplate("thisAccessor");
            accessor.Type = type;
            accessor.Getter.ReturnType = type;
            accessor.Setter.Parameters[0].Type = type;
            accessor.Name = new Identifier(name.Name);
            Replacer.Replace(accessor.Getter, "_fieldName", privExpr);
            Replacer.Replace(accessor.Setter, "_fieldName", privExpr);

            accessor.Getter.Name = Identifier.For("get_" + name.Name);
            accessor.Setter.Name = Identifier.For("set_" + name.Name);

            return accessor;
        }

        //TODO: this code is temporary. Need to integrate this with GetAccessorProperty
        private Property GetStructAccessorProperty(string template, TypeNode type,
            Identifier name, Expression privExpr, Expression localInputOrOutput)
        {
            Property accessor = Templates.GetPropertyTemplate("structAccessor");
            accessor.Type = type;
            accessor.Getter.ReturnType = type;
            accessor.Setter.Parameters[0].Type = type;
            accessor.Name = new Identifier(name.Name);
            Replacer.Replace(accessor.Getter, "_fieldName", privExpr);
            Replacer.Replace(accessor.Setter, "_fieldName", privExpr);

            accessor.Getter.Name = Identifier.For("get_" + name.Name);
            accessor.Setter.Name = Identifier.For("set_" + name.Name);

            return accessor;
        }

        private Property GetAccessorProperty(string template, TypeNode type,
            Identifier name, Expression privExpr, Expression idExpr, Expression localInputOrOutput)
        {
            Property accessor = Templates.GetPropertyTemplate(template);
            accessor.Type = type;
            if (accessor.Getter != null)
            {
                accessor.Getter.ReturnType = type;
                accessor.Getter.Name = Identifier.For("get_" + name.Name);
            }
            if (accessor.Setter != null)
            {
                accessor.Setter.Parameters[0].Type = type;
                accessor.Setter.Name = Identifier.For("set_" + name.Name);
            }
            accessor.Name = new Identifier(name.Name);
            Replacer.Replace(accessor.Getter, "_fieldName", privExpr);
            Replacer.Replace(accessor.Getter, "_fieldId", idExpr);
            Replacer.Replace(accessor.Getter, "_localInputOrOutput", localInputOrOutput);
            Replacer.Replace(accessor.Setter, "_fieldName", privExpr);
            Replacer.Replace(accessor.Setter, "_fieldId", idExpr);
            Replacer.Replace(accessor.Setter, "_localInputOrOutput", localInputOrOutput);
            return accessor;
        }

        private Property GetFieldAccessorProperty(TypeNode oldType, TypeNode type, Identifier name, Expression privExpr,
                                              Expression idExpr)
        {
            Property accessor = null;
            if (type == this.ZingPtrType) // Zing pointer type
            {
                accessor = Templates.GetPropertyTemplate("ptrFieldAccessor");
                Replacer.Replace(accessor.Getter, "_fieldType", new Literal(oldType.Name.ToString(), SystemTypes.String));
            }
            else
            {
                accessor = Templates.GetPropertyTemplate("fieldAccessor");
            }

            accessor.Type = type;
            accessor.Getter.ReturnType = type;
            accessor.Setter.Parameters[0].Type = type;
            accessor.Name = new Identifier(name.Name);
            Replacer.Replace(accessor.Getter, "_fieldName", privExpr);
            Replacer.Replace(accessor.Getter, "_fieldId", idExpr);
            Replacer.Replace(accessor.Setter, "_fieldName", privExpr);
            Replacer.Replace(accessor.Setter, "_fieldId", idExpr);

            accessor.Getter.Name = Identifier.For("get_" + name.Name);
            accessor.Setter.Name = Identifier.For("set_" + name.Name);

            return accessor;
        }

        private Class GenerateInterfaceMethod(ZMethod zMethod)
        {
            this.currentMethod = zMethod;

            Class newClass = (Class)Templates.GetTypeTemplateByName("InterfaceMethod");

            Debug.Assert(!zMethod.IsStatic);
            GenerateThisParameter(newClass);

            Class inputsClass = (Class)Templates.GetMemberByName(newClass.Members, "InputVars");
            GenerateInputs(zMethod, inputsClass);

            Class outputsClass = (Class)Templates.GetMemberByName(newClass.Members, "OutputVars");
            GenerateOutputs(zMethod, outputsClass);

            Replacer.Replace(newClass, newClass.Name, zMethod.Name);

            // If this method doesn't return concrete bool, then remove the helper
            // property for accessing bool return values.
            if (zMethod.ReturnType == SystemTypes.Boolean)
            {
                Property boolRetValProp = (Property)Templates.GetTypeTemplateByName("BooleanReturnValueProperty").Members[0];
                newClass.Members.Add(boolRetValProp);
                boolRetValProp.DeclaringType = newClass;
                newClass.Members.Add(boolRetValProp.Getter);
                boolRetValProp.Getter.DeclaringType = newClass;
            }

            // Clear the "Activated" attribute if we aren't...
            if (!zMethod.Activated)
                newClass.Attributes = new AttributeList(0);

            this.currentMethod = null;

            return newClass;
        }

        private InterfaceList FindMatchingInterfaces(Class c, ZMethod zMethod)
        {
            InterfaceList matches = new InterfaceList(1);
            if (c.Interfaces != null && c.Interfaces.Count > 0)
            {
                for (int i = 0; i < c.Interfaces.Count; i++)
                {
                    Interface x = c.Interfaces[i];
                    if (x.GetMatchingMethod(zMethod) != null)
                        matches.Add(x);
                }
            }
            return matches;
        }

        private void GenerateThisParameter(Class newClass)
        {
            Class thisFieldClass = (Class)Templates.GetTypeTemplateByName("ThisField");
            Field thisField = (Field)thisFieldClass.Members[0];
            thisField.DeclaringType = newClass;
            newClass.Members.Add(thisField);
            Identifier thisAccessor = new Identifier("This");
            Property accessor =
                GetThisAccessorProperty(thisField.Type, thisAccessor, thisField.Name);
            newClass.Members.Add(accessor);
            accessor.DeclaringType = newClass;
            if (accessor.Getter != null)
            {
                newClass.Members.Add(accessor.Getter);
                accessor.Getter.DeclaringType = newClass;
            }
            if (accessor.Setter != null)
            {
                newClass.Members.Add(accessor.Setter);
                accessor.Setter.DeclaringType = newClass;
            }
        }

        private Class GenerateClassMethod(ZMethod zMethod, Interface x)
        {
            this.currentMethod = zMethod;
            Class newClass = (Class)Templates.GetTypeTemplateByName("ClassMethod");
            QualifiedIdentifier qi = (x == null)
                                     ? new QualifiedIdentifier(new Identifier("Z"), new Identifier("ZingMethod"))
                                     : new QualifiedIdentifier(x.Name, zMethod.Name);
            Replacer.Replace(newClass, new Identifier("__Method"), qi);

            if (x == null)
            {
                if (!zMethod.IsStatic)
                    GenerateThisParameter(newClass);
                Class interfaceClass = (Class)Templates.GetTypeTemplateByName("InterfaceMethod");
                for (int i = 0, n = interfaceClass.Members.Count; i < n; i++)
                {
                    newClass.Members.Add(interfaceClass.Members[i]);
                    interfaceClass.Members[i].DeclaringType = newClass;
                }

                Class inputsClass = (Class)Templates.GetMemberByName(newClass.Members, "InputVars");
                GenerateInputs(zMethod, inputsClass);

                Class outputsClass = (Class)Templates.GetMemberByName(newClass.Members, "OutputVars");
                GenerateOutputs(zMethod, outputsClass);
            }

            Class localsClass = (Class)Templates.GetMemberByName(newClass.Members, "LocalVars");
            GenerateLocals(zMethod, localsClass);

            Class extras = (Class)Templates.GetTypeTemplateByName(zMethod.IsStatic
                                         ? "StaticMethodExtras"
                                         : "InstanceMethodExtras");
            for (int i = 0, n = extras.Members.Count; i < n; i++)
            {
                newClass.Members.Add(extras.Members[i]);
                extras.Members[i].DeclaringType = newClass;
                Property p = extras.Members[i] as Property;

                if (p != null)
                {
                    if (p.Getter != null)
                        p.Getter.DeclaringType = newClass;
                    if (p.Setter != null)
                        p.Setter.DeclaringType = newClass;
                }
            }

            Replacer.Replace(newClass, newClass.Name, zMethod.Name);
            SetTypeId(newClass);

            ExtendMethodConstructor(newClass, zMethod);

            // If this method doesn't return concrete bool, then remove the helper
            // property for accessing bool return values.
            if (zMethod.ReturnType == SystemTypes.Boolean)
            {
                Property boolRetValProp = (Property)Templates.GetTypeTemplateByName("BooleanReturnValueProperty").Members[0];
                newClass.Members.Add(boolRetValProp);
                boolRetValProp.DeclaringType = newClass;
                newClass.Members.Add(boolRetValProp.Getter);
                boolRetValProp.Getter.DeclaringType = newClass;
            }

            // Clear the "Activated" attribute if we aren't...
            if (!zMethod.Activated)
                newClass.Attributes = new AttributeList(0);

            GenerateBasicBlocks(newClass, zMethod);
            this.currentMethod = null;

            return newClass;
        }

        private void GenerateInputs(ZMethod zMethod, Class inputsClass)
        {
            List<Field> inputs = new List<Field>(10);

            Method inputsGetValue = (Method)Templates.GetMemberByName(inputsClass.Members, "GetValue");
            Method inputsSetValue = (Method)Templates.GetMemberByName(inputsClass.Members, "SetValue");
            System.Compiler.Switch switchInputsGetValue =
                (System.Compiler.Switch)Templates.GetStatementTemplate("GetFieldInfoSwitch");
            inputsGetValue.Body.Statements.Add(switchInputsGetValue);
            System.Compiler.Switch switchInputsSetValue =
                (System.Compiler.Switch)Templates.GetStatementTemplate("SetFieldInfoSwitch");
            inputsSetValue.Body.Statements.Add(switchInputsSetValue);
            Method copier = (Method)Templates.GetMemberByName(inputsClass.Members, "CopyContents");

            // Create fields in Inputs or Outputs for the parameters
            for (int i = 0, n = zMethod.Parameters.Count; i < n; i++)
            {
                Parameter param = zMethod.Parameters[i];
                if (param == null || param.Type == null || (param.Flags & ParameterFlags.Out) != 0)
                    continue;

                TypeNode zingType = param.Type;

                Field f = new Field(inputsClass, null, FieldFlags.Public,
                    new Identifier("priv_" + param.Name.Name),
                    param.Type, null);

                if (f.Type is Reference)
                    f.Type = ((Reference)f.Type).ElementType;

                if (GetTypeClassification(f.Type) == TypeClassification.Heap)
                {
                    f.Type = this.ZingPtrType;
                }
                else if (!IsPredefinedType(f.Type))
                    f.Type = new TypeExpression(new QualifiedIdentifier(
                        new Identifier("Application"), zingType.Name), zingType.SourceContext);

                Identifier idFieldName = new Identifier("id_" + param.Name.Name);
                Field idf = new Field(inputsClass, null, FieldFlags.Public | FieldFlags.Static,
                    idFieldName,
                    SystemTypes.Int32, null);
                idf.Initializer = new Literal(i, SystemTypes.Int32);

                SwitchCase getCase = ((System.Compiler.Switch)Templates.GetStatementTemplate("GetFieldInfoCase")).Cases[0];
                Replacer.Replace(getCase, "_fieldId", new Literal(i, SystemTypes.Int32));
                Replacer.Replace(getCase, "_fieldName", new Identifier(f.Name.Name));
                switchInputsGetValue.Cases.Add(getCase);

                SwitchCase setCase = ((System.Compiler.Switch)Templates.GetStatementTemplate("SetFieldInfoCase")).Cases[0];
                Replacer.Replace(setCase, "_fieldId", new Literal(i, SystemTypes.Int32));
                Replacer.Replace(setCase, "_fieldName", new Identifier(f.Name.Name));
                TypeExpression tn = f.Type as TypeExpression;
                Replacer.Replace(setCase, "_fieldType", tn != null ? tn.Expression : new Identifier(f.Type.Name.Name));
                switchInputsSetValue.Cases.Add(setCase);

                QualifiedIdentifier localInputOrOutput = new QualifiedIdentifier(new Identifier("LocType"), new Identifier("Input"));

                Property accessor = GetAccessorProperty("inputAccessor", f.Type, param.Name, f.Name, idf.Name, localInputOrOutput);

                inputsClass.Members.Add(f);
                inputsClass.Members.Add(idf);
                inputsClass.Members.Add(accessor);
                f.DeclaringType = inputsClass;
                idf.DeclaringType = inputsClass;
                accessor.DeclaringType = inputsClass;
                if (accessor.Getter != null)
                {
                    inputsClass.Members.Add(accessor.Getter);
                    accessor.Getter.DeclaringType = inputsClass;
                }
                if (accessor.Setter != null)
                {
                    inputsClass.Members.Add(accessor.Setter);
                    accessor.Setter.DeclaringType = inputsClass;
                }

                if (zingType is Struct && !zingType.IsPrimitive && f.Type != SystemTypes.Decimal)
                    collectStructAccessors(false, (Struct)zingType, f.Name,
                        param.Name.Name, inputsClass);

                copier.Body.Statements.Add(GetCopyStatement(f.Name));
                inputs.Add(f);
            }

            Method writer = (Method)Templates.GetMemberByName(inputsClass.Members, "WriteString");
            Method traverser = (Method)Templates.GetMemberByName(inputsClass.Members, "TraverseFields");

            for (int i = 0, n = inputs.Count; i < n; i++)
            {
                Field f = (Field)inputs[i];
                writer.Body.Statements.Add(GetWriterStatement("this", f.Type, f.Name));
                traverser.Body.Statements.Add(GetTraverserStatement("this", f.Type, f.Name));
            }
        }

        private void GenerateOutputs(ZMethod zMethod, Class outputsClass)
        {
            List<Field> outputs = new List<Field>(10);

            Method outputsGetValue = (Method)Templates.GetMemberByName(outputsClass.Members, "GetValue");
            Method outputsSetValue = (Method)Templates.GetMemberByName(outputsClass.Members, "SetValue");
            System.Compiler.Switch switchOutputsGetValue =
                (System.Compiler.Switch)Templates.GetStatementTemplate("GetFieldInfoSwitch");
            outputsGetValue.Body.Statements.Add(switchOutputsGetValue);
            System.Compiler.Switch switchOutputsSetValue =
                (System.Compiler.Switch)Templates.GetStatementTemplate("SetFieldInfoSwitch");
            outputsSetValue.Body.Statements.Add(switchOutputsSetValue);
            Method copier = (Method)Templates.GetMemberByName(outputsClass.Members, "CopyContents");

            // Create fields in Inputs or Outputs for the parameters
            for (int i = 0, n = zMethod.Parameters.Count; i < n; i++)
            {
                Parameter param = zMethod.Parameters[i];
                if (param == null || param.Type == null || (param.Flags & ParameterFlags.Out) == 0)
                    continue;

                TypeNode zingType = param.Type;

                zingType = ((Reference)zingType).ElementType;

                Field f = new Field(outputsClass, null, FieldFlags.Public,
                    new Identifier("priv_" + param.Name.Name),
                    param.Type, null);

                if (f.Type is Reference)
                    f.Type = ((Reference)f.Type).ElementType;

                if (GetTypeClassification(f.Type) == TypeClassification.Heap)
                {
                    f.Type = this.ZingPtrType;
                }
                else if (!IsPredefinedType(f.Type))
                    f.Type = new TypeExpression(new QualifiedIdentifier(
                        new Identifier("Application"), zingType.Name), zingType.SourceContext);

                Identifier idFieldName = new Identifier("id_" + param.Name.Name);
                Field idf = new Field(outputsClass, null, FieldFlags.Public | FieldFlags.Static,
                    idFieldName,
                    SystemTypes.Int32, null);
                idf.Initializer = new Literal(i, SystemTypes.Int32);

                SwitchCase getCase = ((System.Compiler.Switch)Templates.GetStatementTemplate("GetFieldInfoCase")).Cases[0];
                Replacer.Replace(getCase, "_fieldId", new Literal(i, SystemTypes.Int32));
                Replacer.Replace(getCase, "_fieldName", new Identifier(f.Name.Name));
                switchOutputsGetValue.Cases.Add(getCase);

                SwitchCase setCase = ((System.Compiler.Switch)Templates.GetStatementTemplate("SetFieldInfoCase")).Cases[0];
                Replacer.Replace(setCase, "_fieldId", new Literal(i, SystemTypes.Int32));
                Replacer.Replace(setCase, "_fieldName", new Identifier(f.Name.Name));
                TypeExpression tn = f.Type as TypeExpression;
                Replacer.Replace(setCase, "_fieldType", tn != null ? tn.Expression : new Identifier(f.Type.Name.Name));
                switchOutputsSetValue.Cases.Add(setCase);

                QualifiedIdentifier localInputOrOutput = new QualifiedIdentifier(new Identifier("LocType"), new Identifier("Output"));

                Property accessor = GetAccessorProperty("outputAccessor", f.Type, param.Name, f.Name, idf.Name, localInputOrOutput);

                outputsClass.Members.Add(f);
                outputsClass.Members.Add(idf);
                outputsClass.Members.Add(accessor);
                f.DeclaringType = outputsClass;
                idf.DeclaringType = outputsClass;
                accessor.DeclaringType = outputsClass;
                if (accessor.Getter != null)
                {
                    outputsClass.Members.Add(accessor.Getter);
                    accessor.Getter.DeclaringType = outputsClass;
                }
                if (accessor.Setter != null)
                {
                    outputsClass.Members.Add(accessor.Setter);
                    accessor.Setter.DeclaringType = outputsClass;
                }

                //for outputs create an additional lastfunction accessor
                QualifiedIdentifier lfcOutput =
                    new QualifiedIdentifier(new Identifier("LocType"), new Identifier("LastFunctionOutput"));

                Identifier lfcid = new Identifier("_Lfc_" + param.Name.Name);

                Property lfcAccessor = GetAccessorProperty("lastFunctionOutputAccessor", f.Type,
                    lfcid, f.Name, idf.Name, lfcOutput);
                outputsClass.Members.Add(lfcAccessor);
                lfcAccessor.DeclaringType = outputsClass;
                if (lfcAccessor.Getter != null)
                {
                    outputsClass.Members.Add(lfcAccessor.Getter);
                    lfcAccessor.Getter.DeclaringType = outputsClass;
                }

                if (zingType is Struct && !zingType.IsPrimitive && f.Type != SystemTypes.Decimal)
                    collectStructAccessors(false, (Struct)zingType, f.Name,
                        param.Name.Name, outputsClass);

                copier.Body.Statements.Add(GetCopyStatement(f.Name));
                outputs.Add(f);
            }

            if (zMethod.ReturnType.TypeCode != TypeCode.Empty)
            {
                Field f = new Field(outputsClass, null, FieldFlags.Public,
                                    new Identifier("priv_ReturnValue"),
                                    zMethod.ReturnType, null);

                QualifiedIdentifier localInputOrOutput = new QualifiedIdentifier(new Identifier("LocType"), new Identifier("Output"));

                TypeNode zingType = zMethod.ReturnType;

                if (GetTypeClassification(f.Type) == TypeClassification.Heap)
                {
                    f.Type = this.ZingPtrType;
                }
                else if (!IsPredefinedType(f.Type))
                {
                    f.Type = new TypeExpression(new QualifiedIdentifier(
                        new Identifier("Application"), zingType.Name), zingType.SourceContext);
                }

                Identifier idFieldName = new Identifier("id_ReturnValue");
                Field idf = new Field(outputsClass, null, FieldFlags.Public | FieldFlags.Static,
                    idFieldName,
                    SystemTypes.Int32, null);
                idf.Initializer = new Literal(zMethod.Parameters.Count, SystemTypes.Int32);

                SwitchCase getCase = ((System.Compiler.Switch)Templates.GetStatementTemplate("GetFieldInfoCase")).Cases[0];
                Replacer.Replace(getCase, "_fieldId", new Literal(zMethod.Parameters.Count, SystemTypes.Int32));
                Replacer.Replace(getCase, "_fieldName", new Identifier("priv_ReturnValue"));
                switchOutputsGetValue.Cases.Add(getCase);

                SwitchCase setCase = ((System.Compiler.Switch)Templates.GetStatementTemplate("SetFieldInfoCase")).Cases[0];
                Replacer.Replace(setCase, "_fieldId", new Literal(zMethod.Parameters.Count, SystemTypes.Int32));
                Replacer.Replace(setCase, "_fieldName", new Identifier("priv_ReturnValue"));
                TypeExpression tn = f.Type as TypeExpression;
                Replacer.Replace(setCase, "_fieldType", tn != null ? tn.Expression : new Identifier(f.Type.Name.Name));
                switchOutputsSetValue.Cases.Add(setCase);

                Property accessor = GetAccessorProperty("outputAccessor", f.Type,
                                                        new Identifier("_ReturnValue"),
                                                        f.Name, idf.Name, localInputOrOutput);

                QualifiedIdentifier lfcOutput = new QualifiedIdentifier(new Identifier("LocType"), new Identifier("LastFunctionOutput"));

                Property lfcAccessor = GetAccessorProperty("lastFunctionOutputAccessor", f.Type,
                                                           new Identifier("_Lfc_ReturnValue"),
                                                           f.Name, idf.Name, lfcOutput);

                Identifier qualifier = new Identifier("outputs");

                outputsClass.Members.Add(f);
                outputsClass.Members.Add(idf);
                outputsClass.Members.Add(accessor);
                outputsClass.Members.Add(lfcAccessor);
                f.DeclaringType = outputsClass;
                idf.DeclaringType = outputsClass;
                accessor.DeclaringType = outputsClass;
                lfcAccessor.DeclaringType = outputsClass;

                if (accessor.Getter != null)
                {
                    outputsClass.Members.Add(accessor.Getter);
                    accessor.Getter.DeclaringType = outputsClass;
                }
                if (accessor.Setter != null)
                {
                    outputsClass.Members.Add(accessor.Setter);
                    accessor.Setter.DeclaringType = outputsClass;
                }
                if (lfcAccessor.Getter != null)
                {
                    outputsClass.Members.Add(lfcAccessor.Getter);
                    lfcAccessor.Getter.DeclaringType = outputsClass;
                }

                if (zingType is Struct && !zingType.IsPrimitive && f.Type != SystemTypes.Decimal)
                {
                    collectStructAccessors(false, (Struct)zingType, f.Name,
                        "_ReturnValue", outputsClass);
                    collectStructAccessors(false, (Struct)zingType, f.Name,
                        "_Lfc_ReturnValue", outputsClass);
                }

                copier.Body.Statements.Add(GetCopyStatement(f.Name));
                outputs.Add(f);
            }

            Method writer = (Method)Templates.GetMemberByName(outputsClass.Members, "WriteString");
            Method traverser = (Method)Templates.GetMemberByName(outputsClass.Members, "TraverseFields");

            for (int i = 0, n = outputs.Count; i < n; i++)
            {
                Field f = (Field)outputs[i];
                writer.Body.Statements.Add
                    (GetWriterStatement("this", f.Type, f.Name));
                traverser.Body.Statements.Add(GetTraverserStatement("this", f.Type, f.Name));
            }
        }

        private void GenerateLocals(ZMethod zMethod, Class localsClass)
        {
            List<Field> locals = new List<Field>(10);

            Method localsGetValue = (Method)Templates.GetMemberByName(localsClass.Members, "GetValue");
            Method localsSetValue = (Method)Templates.GetMemberByName(localsClass.Members, "SetValue");
            System.Compiler.Switch switchLocalsGetValue =
                (System.Compiler.Switch)Templates.GetStatementTemplate("GetFieldInfoSwitch");
            localsGetValue.Body.Statements.Add(switchLocalsGetValue);
            System.Compiler.Switch switchLocalsSetValue =
                (System.Compiler.Switch)Templates.GetStatementTemplate("SetFieldInfoSwitch");
            localsSetValue.Body.Statements.Add(switchLocalsSetValue);
            Method copier = (Method)Templates.GetMemberByName(localsClass.Members, "CopyContents");

            for (int i = 0, n = zMethod.LocalVars.Count; i < n; i++)
            {
                Field localVar = zMethod.LocalVars[i];
                TypeNode zingType = localVar.Type;
                if (localVar.Type == null)
                    continue;

                Field f = new Field(localsClass, null, FieldFlags.Public,
                                    new Identifier("priv_" + localVar.Name.Name),
                                    localVar.Type, null);

                if (GetTypeClassification(f.Type) == TypeClassification.Heap)
                    f.Type = this.ZingPtrType;
                else if (!IsPredefinedType(f.Type))
                    f.Type = new TypeExpression(new QualifiedIdentifier(
                        new Identifier("Application"), zingType.Name), zingType.SourceContext);

                Identifier idFieldName = new Identifier("id_" + localVar.Name.Name);
                Field idf = new Field(localsClass, null, FieldFlags.Public | FieldFlags.Static,
                    idFieldName,
                    SystemTypes.Int32, null);
                idf.Initializer = new Literal(i, SystemTypes.Int32);
                SwitchCase getCase = ((System.Compiler.Switch)Templates.GetStatementTemplate("GetFieldInfoCase")).Cases[0];
                Replacer.Replace(getCase, "_fieldId", new Literal(i, SystemTypes.Int32));
                Replacer.Replace(getCase, "_fieldName", new Identifier(f.Name.Name));
                switchLocalsGetValue.Cases.Add(getCase);

                SwitchCase setCase = ((System.Compiler.Switch)Templates.GetStatementTemplate("SetFieldInfoCase")).Cases[0];
                Replacer.Replace(setCase, "_fieldId", new Literal(i, SystemTypes.Int32));
                Replacer.Replace(setCase, "_fieldName", new Identifier(f.Name.Name));
                TypeExpression tn = f.Type as TypeExpression;
                Replacer.Replace(setCase, "_fieldType", tn != null ? tn.Expression : new Identifier(f.Type.Name.Name));
                switchLocalsSetValue.Cases.Add(setCase);

                QualifiedIdentifier localInputOrOutput = new QualifiedIdentifier(new Identifier("LocType"), new Identifier("Local"));

                Property accessor = GetAccessorProperty("localAccessor", f.Type,
                                                        localVar.Name, f.Name, idf.Name, localInputOrOutput);

                Identifier qualifier = new Identifier("locals");

                localsClass.Members.Add(f);
                localsClass.Members.Add(idf);
                localsClass.Members.Add(accessor);
                f.DeclaringType = localsClass;
                idf.DeclaringType = localsClass;
                accessor.DeclaringType = localsClass;
                if (accessor.Getter != null)
                {
                    localsClass.Members.Add(accessor.Getter);
                    accessor.Getter.DeclaringType = localsClass;
                }
                if (accessor.Setter != null)
                {
                    localsClass.Members.Add(accessor.Setter);
                    accessor.Setter.DeclaringType = localsClass;
                }

                if (zingType is Struct && !zingType.IsPrimitive && f.Type != SystemTypes.Decimal)
                    collectStructAccessors(false, (Struct)zingType, f.Name,
                                           localVar.Name.Name, localsClass);

                copier.Body.Statements.Add(GetCopyStatement(f.Name));
                locals.Add(f);
            }

            Method writer = (Method)Templates.GetMemberByName(localsClass.Members, "WriteString");
            Method traverser = (Method)Templates.GetMemberByName(localsClass.Members, "TraverseFields");

            for (int i = 0, n = locals.Count; i < n; i++)
            {
                Field f = (Field)locals[i];
                writer.Body.Statements.Add(GetWriterStatement("this", f.Type, f.Name));
                traverser.Body.Statements.Add(GetTraverserStatement("this", f.Type, f.Name));
            }
        }

        private void GenerateBasicBlocks(Class newClass, ZMethod zMethod)
        {
            //
            // Split the method into basic blocks, and then do code generation
            // based on this analysis
            //
            if (labelString != null)
                basicBlockToLabel = new Hashtable();
            List<BasicBlock> basicBlocks = BBSplitter.SplitMethod(zMethod, this);
            if (labelString != null)
            {
                string methodName = zMethod.FullName;
                int index = methodName.IndexOf('(');
                if (index < 0)
                    AddToLabelString(methodName);
                else
                {
                    Debug.Assert(index > 0);
                    AddToLabelString(methodName.Substring(0, index));
                }
            }

            //
            // Look for opportunities to combine or remove blocks
            //
            // NOTE: this breaks summarization because it removes "transition" blocks
            // around atomic regions that they rely on.
            //
            //basicBlocks = BBOptimizer.Optimize(basicBlocks);

            EnumNode enumNode = (EnumNode)Templates.GetMemberByName(newClass.Members, "Blocks");
            Field entryPointField = (Field)enumNode.Members[1];
            int nextBlockEnumValue = 2;

            // Accumulate a list of scopes for which we've generated cleanup methods. These are
            // scopes that contain pointers.
            List<Scope> nonTrivialScopes = new List<Scope>();
            foreach (BasicBlock block in basicBlocks)
            {
                if (!nonTrivialScopes.Contains(block.Scope) && ScopeNeedsCleanup(block.Scope))
                {
                    nonTrivialScopes.Add(block.Scope);
                    AddScopeCleanupMethod(newClass, block.Scope);
                }
            }

            foreach (BasicBlock block in basicBlocks)
            {
                // Add the blocks to the Blocks enum
                if (!block.IsEntryPoint)
                {
                    Field f = new Field(enumNode, null, entryPointField.Flags,
                        new Identifier(block.Name), entryPointField.Type, null);
                    f.Initializer = new Literal(nextBlockEnumValue++, SystemTypes.UInt16);
                    enumNode.Members.Add(f);
                }

                // Emit a method for this block
                AddBlockMethod(zMethod, newClass, block, nonTrivialScopes);
            }

            PatchDispatchMethod(newClass, basicBlocks);
            PatchRunnableMethod(newClass, basicBlocks);
            PatchIsAtomicEntryMethod(newClass, basicBlocks);
            PatchValidEndStateProperty(newClass, basicBlocks);
            PatchSourceContextProperty(zMethod, newClass, basicBlocks);
            PatchContextAttributeProperty(zMethod, newClass, basicBlocks);
        }

        // Create a secondary constructor that provides for initialization of
        // the "this" pointer as well as the input parameters
        private void ExtendMethodConstructor(Class newClass, ZMethod zMethod)
        {
            int numAddedParams = 0;

            // Duplicate our basic constructor so we can make an extended version
            System.Compiler.Duplicator dup = new System.Compiler.Duplicator(null, null);
            InstanceInitializer ctor = dup.VisitInstanceInitializer((InstanceInitializer)newClass.Members[0]);

            if (!zMethod.IsStatic)
            {
                // For instance methods, add a "This" parameter to the constructor
                ctor.Parameters.Add(new Parameter(Identifier.For("This"), new TypeExpression(Identifier.For("Pointer"))));

                // this.This = This;
                ctor.Body.Statements.Add(
                    new ExpressionStatement(
                        new AssignmentExpression(
                            new AssignmentStatement(
                                new QualifiedIdentifier(new This(), Identifier.For("This")),
                                Identifier.For("This"))))
                );

                numAddedParams++;
            }

            for (int i = 0, n = zMethod.Parameters.Count; i < n; i++)
            {
                TypeNode paramType;
                Parameter param = zMethod.Parameters[i];

                if (param == null || param.Type == null)
                    continue;

                if (param.IsOut)
                    continue;

                if (GetTypeClassification(param.Type) == TypeClassification.Heap)
                    paramType = this.ZingPtrType;
                else if (!IsPredefinedType(param.Type))
                    paramType = new TypeExpression(new QualifiedIdentifier(
                        new Identifier("Application"), param.Type.Name));
                else
                    paramType = param.Type;

                ctor.Parameters.Add(new Parameter(param.Name, paramType));
                // inputs.foo = foo;
                ctor.Body.Statements.Add(
                    new ExpressionStatement(
                        new AssignmentExpression(
                            new AssignmentStatement(
                                new QualifiedIdentifier(Identifier.For("inputs"), param.Name),
                                param.Name)))
                    );

                numAddedParams++;
            }

            // If we didn't actually add any parameters, then the basic constructor is
            // all that we need. Only add the new constructor if it's different.
            if (numAddedParams > 0)
            {
                newClass.Members.Add(ctor);
                ctor.DeclaringType = newClass;
            }
        }

        private bool ScopeNeedsCleanup(Scope scope)
        {
            if (scope == null) return false;

            // The top-level scope (just below the MethodScope) is never exited
            // (until we return) so it doesn't need a cleanup method of its own.
            if (scope.OuterScope != null && scope.OuterScope is MethodScope)
                return false;

            for (int i = 0, n = scope.Members.Count; i < n; i++)
            {
                if (scope.Members[i] is Field)
                    return true;
            }
            return false;
        }

        private void AddScopeCleanupMethod(Class methodClass, Scope scope)
        {
            Method scopeMethod = (Method)Templates.GetTypeTemplateByName("ScopeMethod").Members[0];
            scopeMethod.Name = new Identifier("Scope" + scope.UniqueKey.ToString());
            methodClass.Members.Add(scopeMethod);
            scopeMethod.DeclaringType = methodClass;

            // Generate a "finalizing" statement as appropriate for each local variable in the scope
            for (int i = 0, n = scope.Members.Count; i < n; i++)
            {
                Field f = scope.Members[i] as Field;

                if (f == null)
                    continue;

                if (f.Type == SystemTypes.Boolean)
                {
                    // locals._field = false;
                    scopeMethod.Body.Statements.Add(
                        new ExpressionStatement(
                            new AssignmentExpression(
                                new AssignmentStatement(
                                    new QualifiedIdentifier(Identifier.For("locals"), f.Name),
                                    new Literal(false, SystemTypes.Boolean)
                                )
                            )
                        )
                    );
                }
                else // if (GetTypeClassification(f.Type) == TypeClassification.Heap)
                {
                    // locals._field = 0;   // note - this is good for pointers, ints, bytes, and enums
                    scopeMethod.Body.Statements.Add(
                        new ExpressionStatement(
                            new AssignmentExpression(
                                new AssignmentStatement(
                                    new QualifiedIdentifier(Identifier.For("locals"), f.Name),
                                    new Literal(0, SystemTypes.Int32)
                                )
                            )
                        )
                    );
                }
            }
        }

        private void AddBlockMethod(ZMethod zMethod, Class methodClass, BasicBlock block, List<Scope> nonTrivialScopes)
        {
            Method blockMethod = (Method)Templates.GetTypeTemplateByName("BlockMethod").Members[0];
            blockMethod.Name = new Identifier(block.Name);
            methodClass.Members.Add(blockMethod);
            blockMethod.DeclaringType = methodClass;

            // Generate the appropriate closing statements for the block. Indicate if the
            // block terminates an atomic region and establish the transfer of control to
            // the next block(s) or out of the method.

            if ((ZingCompilerOptions.IsPreemtive && !block.MiddleOfTransition && !block.IsReturn) || (block.Yields))
            {
                // p.MiddleOfTransition = false;
                blockMethod.Body.Statements.Add(
                    new ExpressionStatement(
                        new AssignmentExpression(
                            new AssignmentStatement(
                                new QualifiedIdentifier(Identifier.For("p"), Identifier.For("MiddleOfTransition")),
                                new Literal(false, SystemTypes.Boolean)
                            )
                        )
                    )
                );
            }

            // p.AtomicityLevel = this.SavedAtomicityLevel + X;
            blockMethod.Body.Statements.Add(
                new ExpressionStatement(
                    new AssignmentExpression(
                        new AssignmentStatement(
                            new QualifiedIdentifier(Identifier.For("p"), Identifier.For("AtomicityLevel")),
                            new BinaryExpression(
                                new QualifiedIdentifier(new This(), Identifier.For("SavedAtomicityLevel")),
                                new Literal(block.RelativeAtomicLevel, SystemTypes.Int32),
                                NodeType.Add
                            )
                        )
                    )
                )
            );

#if false
//
// The following code was added for summarization, but isn't quite right. It
// updates the nextBlock too early for some blocks. -- Tony
//
//
            // when generating summaries of type MaxCall, we need to
            // know the value of nextBlock before we invoke p.Call().
            // the first of the two basic blocks of a Zing method call
            // is guaranteed to fall through, so we only need to lift
            // the assignment of nextBlock for fall-through blocks.
            if (block.ConditionalTarget == null && block.UnconditionalTarget != null)
            {
                stmt = Templates.GetStatementTemplate("UnconditionalBlockTransfer");
                Replacer.Replace(stmt, "_UnconditionalBlock",
                                 new Identifier(block.UnconditionalTarget.Name));
                blockMethod.Body.Statements.Add(stmt);
            }
#endif

            if (block.Attributes != null)
            {
                Duplicator duplicator = new Duplicator(null, null);

                for (int i = 0, n = block.Attributes.Count; i < n; i++)
                {
                    if (block.Attributes[i] == null)
                        continue;

                    AttributeNode dupAttr = duplicator.VisitAttributeNode(block.Attributes[i]);

                    Normalizer normalizer = new Normalizer(false);
                    ExpressionList attrParams = normalizer.VisitExpressionList(dupAttr.Expressions);

                    // application.Trace(_context, _contextAttr, new Z.Attributes._attrName(...) );
                    ExpressionStatement traceStmt = new ExpressionStatement(
                        new MethodCall(
                            new QualifiedIdentifier(Identifier.For("application"), Identifier.For("Trace")),
                            new ExpressionList(
                                SourceContextConstructor(dupAttr.SourceContext),
                                new Literal(null, SystemTypes.Object),
                                new Construct(
                                    new MemberBinding(
                                        null,
                                        new TypeExpression(
                                            new QualifiedIdentifier(
                                                new QualifiedIdentifier(Identifier.For("Z"), Identifier.For("Attributes")),
                                                dupAttr.Type.Name
                                            )
                                        )
                                    ),
                                    attrParams
                                )
                            )
                        )
                    );
                    blockMethod.Body.Statements.Add(traceStmt);
                }
            }

            if (block.Statement != null)
            {
                if (block.SkipNormalizer)
                    blockMethod.Body.Statements.Add(block.Statement);
                else
                {
                    // Do statement-level code-gen pass on the block's statement
                    Normalizer normalizer = new Normalizer(this, block.Attributes, block.SecondOfTwo);

                    blockMethod.Body.Statements.Add((Statement)normalizer.Visit(block.Statement));
                }
            }

            if (block.ConditionalTarget != null && block.ConditionalExpression != null)
            {
                Block trueBlock, falseBlock;

                // if (_conditionalExpression)
                //     nextBlock = Blocks._conditionalTarget;
                // else
                //     nextBlock = Blocks._unconditionalTarget;
                blockMethod.Body.Statements.Add(
                    new If(
                        block.ConditionalExpression,
                        trueBlock = new Block(new StatementList(
                            new ExpressionStatement(
                                new AssignmentExpression(
                                    new AssignmentStatement(
                                        Identifier.For("nextBlock"),
                                        new QualifiedIdentifier(
                                            Identifier.For("Blocks"),
                                            Identifier.For(block.ConditionalTarget.Name)
                                        )
                                    )
                                )
                            )
                        )),
                        falseBlock = new Block(new StatementList(
                            new ExpressionStatement(
                                new AssignmentExpression(
                                    new AssignmentStatement(
                                        Identifier.For("nextBlock"),
                                        new QualifiedIdentifier(
                                            Identifier.For("Blocks"),
                                            Identifier.For(block.UnconditionalTarget.Name)
                                        )
                                    )
                                )
                            )
                        ))
                    )
                );

                AddScopeCleanupCalls(trueBlock.Statements, block, block.ConditionalTarget, nonTrivialScopes);
                AddScopeCleanupCalls(falseBlock.Statements, block, block.UnconditionalTarget, nonTrivialScopes);
            }
            else if (block.UnconditionalTarget != null)
            {
                // nextBlock = Blocks.X;
                blockMethod.Body.Statements.Add(
                    new ExpressionStatement(
                        new AssignmentExpression(
                            new AssignmentStatement(
                                Identifier.For("nextBlock"),
                                new QualifiedIdentifier(Identifier.For("Blocks"), Identifier.For(block.UnconditionalTarget.Name))
                            )
                        )
                    )
                );
                AddScopeCleanupCalls(blockMethod.Body.Statements, block, block.UnconditionalTarget, nonTrivialScopes);
            }
            else if (block.IsReturn)
            {
                Debug.Assert(block.UnconditionalTarget == null);

                Statement returnCall = Templates.GetStatementTemplate("ReturnBlockTransfer");
                SourceContext context;
                Return ret = block.Statement as Return;
                if (ret != null)
                {
                    context = ret.SourceContext;
                }
                else
                {
                    // If not a return stmt, the context is the closing brace of the method
                    context = zMethod.SourceContext;
                    context.StartPos = context.EndPos - 1;
                }

                Replacer.Replace(returnCall, "_context", SourceContextConstructor(context));
                Replacer.Replace(returnCall, "_contextAttr", ContextAttributeConstructor(block.Attributes));

                blockMethod.Body.Statements.Add(returnCall);

                // StateImpl.IsReturn = true;
                blockMethod.Body.Statements.Add(
                    new ExpressionStatement(
                        new AssignmentExpression(
                            new AssignmentStatement(
                                new QualifiedIdentifier(Identifier.For("StateImpl"), Identifier.For("IsReturn")),
                                new Literal(true, SystemTypes.Boolean)
                            )
                        )
                    )
                );
            }
        }

        private void AddScopeCleanupCalls(StatementList stmts, BasicBlock source, BasicBlock target, List<Scope> nonTrivialScopes)
        {
            // No scope change, so nothing needed here
            if (source.Scope == target.Scope)
                return;

            //
            // Scopes are different. Figure out if we're moving in or out. If the target scope
            // isn't "outward", then do nothing.
            for (Scope scope = source.Scope; scope != target.Scope; scope = scope.OuterScope)
            {
                // If we encounter the method-level scope, then this must be an inward move
                if (scope is MethodScope)
                    return;
            }

            // It's an outward move, so call the cleanup method for each scope that we're exiting
            for (Scope scope = source.Scope; scope != target.Scope; scope = scope.OuterScope)
            {
                Debug.Assert(scope != null);

                if (nonTrivialScopes.Contains(scope))
                {
                    stmts.Add(
                        new ExpressionStatement(
                            new MethodCall(
                                Identifier.For("Scope" + scope.UniqueKey.ToString()),
                                new ExpressionList()
                            )
                        )
                    );
                }
            }
        }

        private void PatchDispatchMethod(Class methodClass, List<BasicBlock> basicBlocks)
        {
            Method dispatchMethod = (Method)Templates.GetMemberByName(methodClass.Members, "Dispatch");

            Debug.Assert(dispatchMethod.Body.Statements.Count == 1);
            Debug.Assert(dispatchMethod.Body.Statements[0] is System.Compiler.Switch);
            System.Compiler.Switch switchStmt = (System.Compiler.Switch)dispatchMethod.Body.Statements[0];

            foreach (BasicBlock block in basicBlocks)
            {
                if (block.IsEntryPoint)
                    continue;

                // case Blocks._blockName:
                //     _blockName(p);
                //     break;
                switchStmt.Cases.Add(
                    new SwitchCase(
                        new QualifiedIdentifier(Identifier.For("Blocks"), Identifier.For(block.Name)),
                        new Block(new StatementList(
                            new ExpressionStatement(
                                new MethodCall(
                                    Identifier.For(block.Name),
                                    new ExpressionList(Identifier.For("p"))
                                )
                            ),
                            new Exit()
                        ))
                    )
                );
            }
        }

        private void PatchRunnableMethod(Class methodClass, List<BasicBlock> basicBlocks)
        {
            Method runnableJSMethod = (Method)
                Templates.GetMemberByName(methodClass.Members, "GetRunnableJoinStatements");
            Debug.Assert(runnableJSMethod.Body.Statements[0] is System.Compiler.Switch);
            System.Compiler.Switch switchStmt = (System.Compiler.Switch)runnableJSMethod.Body.Statements[0];

            Normalizer normalizer = new Normalizer(this, null, false);

            foreach (BasicBlock block in basicBlocks)
            {
                SwitchCase newCase;

                if (block.selectStmt != null)
                {
                    Expression runnableExpr = null;

                    for (int i = 0, n = block.selectStmt.joinStatementList.Length; i < n; i++)
                    {
                        Expression jsRunnable = normalizer.GetRunnablePredicate(block.selectStmt.joinStatementList[i]);

                        if (jsRunnable == null)
                            continue;

                        Expression jsRunnableBit = Templates.GetExpressionTemplate("JoinStatementRunnableBit");
                        Replacer.Replace(jsRunnableBit, "_jsRunnableBoolExpr", jsRunnable);
                        Replacer.Replace(jsRunnableBit, "_jsBitMask", (Expression)new Literal((ulong)(1 << i), SystemTypes.UInt64));

                        if (runnableExpr == null)
                            runnableExpr = jsRunnableBit;
                        else
                            runnableExpr = new BinaryExpression(runnableExpr, jsRunnableBit, NodeType.Or, block.selectStmt.SourceContext);
                    }

                    if (runnableExpr != null)
                    {
                        newCase = ((System.Compiler.Switch)Templates.GetStatementTemplate("RunnableSwitchSelect")).Cases[0];
                        Replacer.Replace(newCase, "_BlockName", new Identifier(block.Name));
                        Replacer.Replace(newCase, "_expr", runnableExpr);
                        switchStmt.Cases.Add(newCase);
                    }

                    //
                    // Now check for blocks that flow atomically into this one and add cases for them
                    // as well. This can happen when the target of a "goto" is a label preceding a
                    // select statement.
                    //
                    foreach (BasicBlock previousBlock in basicBlocks)
                    {
                        if (previousBlock.UnconditionalTarget == block && previousBlock.ConditionalTarget == null &&
                            previousBlock.Statement == null && previousBlock.MiddleOfTransition)
                        {
                            newCase = ((System.Compiler.Switch)Templates.GetStatementTemplate("RelatedSwitchSelect")).Cases[0];
                            Replacer.Replace(newCase, "_BlockName", new Identifier(previousBlock.Name));
                            Replacer.Replace(newCase, "_TargetName", new Identifier(block.Name));
                            switchStmt.Cases.Add(newCase);
                        }
                    }
                }
            }
        }

        private void PatchIsAtomicEntryMethod(Class methodClass, List<BasicBlock> basicBlocks)
        {
            Method atomicEntryMethod = (Method)
                Templates.GetMemberByName(methodClass.Members, "IsAtomicEntryBlock");
            Debug.Assert(atomicEntryMethod.Body.Statements[0] is System.Compiler.Switch);
            System.Compiler.Switch switchStmt = (System.Compiler.Switch)atomicEntryMethod.Body.Statements[0];

            Normalizer normalizer = new Normalizer(this, null, false);

            foreach (BasicBlock block in basicBlocks)
            {
                SwitchCase newCase;
                Expression trueExpr;

                if (block.IsAtomicEntry)
                {
                    Debug.Assert(block.RelativeAtomicLevel == 1);

                    trueExpr = new Literal(true, SystemTypes.Boolean);
                    newCase = ((System.Compiler.Switch)Templates.GetStatementTemplate("RunnableSwitchSelect")).Cases[0];
                    Replacer.Replace(newCase, "_BlockName", new Identifier(block.Name));
                    Replacer.Replace(newCase, "_expr", trueExpr);
                    switchStmt.Cases.Add(newCase);
                }
            }
        }

        private void PatchValidEndStateProperty(Class methodClass, List<BasicBlock> basicBlocks)
        {
            Property validEndStateProperty = (Property)Templates.GetMemberByName(methodClass.Members, "ValidEndState");
            Method validEndStateMethod = validEndStateProperty.Getter;
            Debug.Assert(validEndStateMethod.Body.Statements[0] is System.Compiler.Switch);
            System.Compiler.Switch switchStmt = (System.Compiler.Switch)validEndStateMethod.Body.Statements[0];

            foreach (BasicBlock block in basicBlocks)
            {
                if (block.selectStmt != null && block.selectStmt.validEndState)
                {
                    SwitchCase newCase;

                    newCase = ((System.Compiler.Switch)Templates.GetStatementTemplate("ValidEndStateSwitch")).Cases[0];
                    Replacer.Replace(newCase, "_BlockName", new Identifier(block.Name));
                    switchStmt.Cases.Add(newCase);

                    //
                    // Now check for blocks that flow atomically into this one and add cases for them
                    // as well. This can happen when the target of a "goto" is a label preceding a
                    // select statement.
                    //
                    foreach (BasicBlock previousBlock in basicBlocks)
                    {
                        if (previousBlock.UnconditionalTarget == block && previousBlock.ConditionalTarget == null &&
                            previousBlock.Statement == null && previousBlock.MiddleOfTransition)
                        {
                            newCase = ((System.Compiler.Switch)Templates.GetStatementTemplate("RelatedSwitchSelect")).Cases[0];
                            Replacer.Replace(newCase, "_BlockName", new Identifier(previousBlock.Name));
                            Replacer.Replace(newCase, "_TargetName", new Identifier(block.Name));
                            switchStmt.Cases.Add(newCase);
                        }
                    }
                }
            }
        }

        private void PatchSourceContextProperty(ZMethod zMethod, Class methodClass, List<BasicBlock> basicBlocks)
        {
            Property contextProperty = (Property)Templates.GetMemberByName(methodClass.Members, "Context");
            Method contextMethod = contextProperty.Getter;
            Debug.Assert(contextMethod.Body.Statements[0] is System.Compiler.Switch);
            System.Compiler.Switch switchStmt = (System.Compiler.Switch)contextMethod.Body.Statements[0];

            foreach (BasicBlock block in basicBlocks)
            {
                SourceContext ctxt = new SourceContext(null, 0, 0);

                //ctxt.Document = null;
                //ctxt.StartPos = 0;
                //ctxt.EndPos = 0;

                if (block.SourceContext.StartPos != 0 || block.SourceContext.EndPos != 0)
                {
                    // If the block says something explicit about its context, take that
                    // as final.
                    ctxt = block.SourceContext;
                }
                else
                {
                    // The block doesn't say anything, so figure it out from the statements
                    // inside the block.

                    BasicBlock effectiveBlock = block;
                    //
                    // If we only fall through to another BB without any executable code,
                    // conditional branching, interleaving, or return - then consider our
                    // source context to be the next "real" thing that happens.
                    //
                    while ((effectiveBlock.Statement == null || effectiveBlock.SkipNormalizer) &&
                        effectiveBlock.ConditionalExpression == null &&
                        effectiveBlock.SourceContext.SourceText == null &&
                        effectiveBlock.MiddleOfTransition &&
                        !effectiveBlock.IsReturn && !effectiveBlock.PropagatesException)
                    {
                        effectiveBlock = effectiveBlock.UnconditionalTarget;
                    }

                    // See which source context is the most appropriate for this block.
                    if (effectiveBlock.Statement != null)
                    {
                        if (effectiveBlock.Statement.SourceContext.SourceText != null)
                            ctxt = effectiveBlock.Statement.SourceContext;
                        else
                        {
                            Block b = effectiveBlock.Statement as Block;
                            if (b != null)
                            {
                                for (int i = 0, n = b.Statements.Count; i < n; i++)
                                {
                                    if (b.Statements[i] != null)
                                    {
                                        ctxt = b.Statements[i].SourceContext;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    else if (effectiveBlock.ConditionalExpression != null)
                        ctxt = effectiveBlock.ConditionalExpression.SourceContext;
                    else if (effectiveBlock.SourceContext.SourceText != null)
                        ctxt = effectiveBlock.SourceContext;
                    else
                    {
                        // For "return" blocks, show the closing brace as the source context.
                        ctxt.Document = zMethod.SourceContext.Document;
                        ctxt.EndPos = zMethod.SourceContext.EndPos;
                        ctxt.StartPos = ctxt.EndPos - 1;
                    }
                }

                if (ctxt.StartPos < 0)
                    ctxt.StartPos = 0;

                SwitchCase newCase = new SwitchCase(
                    new QualifiedIdentifier(Identifier.For("Blocks"), Identifier.For(block.Name)),
                    new Block(new StatementList(
                        new Return(SourceContextConstructor(ctxt))))
                    );

                switchStmt.Cases.Add(newCase);
            }
        }

        internal Expression SourceContextConstructor(SourceContext ctx)
        {
            return new Construct(
                new MemberBinding(null, new TypeExpression(Identifier.For("ZingSourceContext"))),
                new ExpressionList(
                    (ctx.Document != null && this.sourceDocuments.Contains(ctx.Document))
                        ? new Literal((int)this.sourceDocuments[ctx.Document], SystemTypes.Int32)
                        : new Literal(0, SystemTypes.Int32),
                    new Literal(ctx.StartPos, SystemTypes.Int32),
                    new Literal(ctx.EndPos, SystemTypes.Int32)));
        }

        internal Expression ContextAttributeConstructor(AttributeList attrs)
        {
            AttributeNode contextAttr = this.GetContextAttribute(attrs);

            if (contextAttr == null)
                return new Literal(null, SystemTypes.Object);

            Duplicator duplicator = new Duplicator(null, null);
            AttributeNode dupAttr = duplicator.VisitAttributeNode(contextAttr);

            Construct cons = (Construct)Templates.GetExpressionTemplate("ContextAttributeConstructor");

            Replacer.Replace(cons, "_AttributeName", dupAttr.Type.Name);

            Normalizer normalizer = new Normalizer(false);
            cons.Operands = normalizer.VisitExpressionList(dupAttr.Expressions);

            return cons;
        }

        private void PatchContextAttributeProperty(ZMethod zMethod, Class methodClass, List<BasicBlock> basicBlocks)
        {
            Property contextProperty = (Property)Templates.GetMemberByName(methodClass.Members, "ContextAttribute");
            Method contextMethod = contextProperty.Getter;
            Debug.Assert(contextMethod.Body.Statements[0] is System.Compiler.Switch);
            System.Compiler.Switch switchStmt = (System.Compiler.Switch)contextMethod.Body.Statements[0];

            foreach (BasicBlock block in basicBlocks)
            {
                AttributeNode contextAttr = GetContextAttribute(block.Attributes);

                if (contextAttr == null)
                    continue;               // no context attribute found

                SwitchCase newCase = ((System.Compiler.Switch)Templates.GetStatementTemplate("ContextAttributeSwitch")).Cases[0];
                Replacer.Replace(newCase, "_BlockName", new Identifier(block.Name));

                // TODO: for user-defined attributes, we'll need to construct a qualified identifier here
                Replacer.Replace(newCase, "_AttributeName", contextAttr.Type.Name);

                // Patch the selected context columns into the constructor template
                Return ret = (Return)newCase.Body.Statements[0];
                Construct cons = (Construct)ret.Expression;
                cons.Operands = contextAttr.Expressions;

                switchStmt.Cases.Add(newCase);
            }
        }

        private AttributeNode GetContextAttribute(AttributeList attrs)
        {
            AttributeNode contextAttr = null;

            if (attrs == null)
                return null;

            for (int i = 0, n = attrs.Count; i < n; i++)
            {
                AttributeNode attr = attrs[i];

                if (attr == null)
                    continue;

                TypeNode attrBase = ((Class)attr.Type).BaseClass;

                if (attrBase.FullName == "Microsoft.Zing.ZingAttribute")
                {
                    // Only attributes derived from this base class may be used
                    // for source context annotation. We currently take only the
                    // first such attribute for our context. We could extend this
                    // to return an array if necessary.
                    contextAttr = attr;
                    break;
                }
            }

            return contextAttr;
        }

        private MemberList CollectGlobals()
        {
            MemberList members = new MemberList();

            for (int c = 0, nc = this.cZing.CompilationUnits.Count; c < nc; c++)
            {
                for (int t = 0, nTypes = ((Namespace)this.cZing.CompilationUnits[c].Nodes[0]).Types.Count; t < nTypes; t++)
                {
                    TypeNode type = ((Namespace)this.cZing.CompilationUnits[c].Nodes[0]).Types[t];

                    if (type.NodeType == NodeType.Class)
                    {
                        for (int m = 0, nMembers = type.Members.Count; m < nMembers; m++)
                        {
                            Field f = type.Members[m] as Field;

                            if (f != null && f.Type != null && f.IsStatic)
                                members.Add(f);
                        }
                    }
                }
            }
            return members;
        }

        private TypeNodeList CollectClasses()
        {
            TypeNodeList classes = new TypeNodeList();

            for (int c = 0, nc = this.cZing.CompilationUnits.Count; c < nc; c++)
            {
                for (int t = 0, nTypes = ((Namespace)this.cZing.CompilationUnits[c].Nodes[0]).Types.Count; t < nTypes; t++)
                {
                    TypeNode type = ((Namespace)this.cZing.CompilationUnits[c].Nodes[0]).Types[t];

                    if (type.NodeType == NodeType.Class)
                    {
                        classes.Add(type);
                    }
                }
            }
            return classes;
        }

        private void collectStructAccessors(bool isGlobalVar, Struct s,
                                            Expression expPrefix,
                                            string strPrefix, Class theClass)
        {
            if (s == null)
                return;

            for (int i = 0, n = s.Members.Count; i < n; i++)
            {
                Field f = s.Members[i] as Field;

                if (f == null)
                    continue;

                string name = strPrefix + "_" + f.Name.Name;
                QualifiedIdentifier qf = new QualifiedIdentifier(expPrefix, f.Name);

                TypeNode generatedType = f.Type;
                if (GetTypeClassification(f.Type) == TypeClassification.Heap)
                    generatedType = this.ZingPtrType;
                else if (!IsPredefinedType(f.Type))
                    generatedType = new TypeExpression(new QualifiedIdentifier(
                        new Identifier("Application"), f.Type.Name), f.Type.SourceContext);

                QualifiedIdentifier localsInputsOrOutputs = isGlobalVar ?
                    new QualifiedIdentifier(new Identifier("LocType"), new Identifier("Global")) :
                    new QualifiedIdentifier(new Identifier("LocType"), new Identifier("Local"));

                Property accessor =
                    GetStructAccessorProperty(isGlobalVar ? "globalAccessor" : "localAccessor",
                                        generatedType, new Identifier("__strprops_" + name),
                                        qf, localsInputsOrOutputs);

                theClass.Members.Add(accessor);
                accessor.DeclaringType = theClass;
                if (accessor.Getter != null)
                {
                    theClass.Members.Add(accessor.Getter);
                    accessor.Getter.DeclaringType = theClass;
                }
                if (accessor.Setter != null)
                {
                    theClass.Members.Add(accessor.Setter);
                    accessor.Setter.DeclaringType = theClass;
                }

                if (f.Type is Struct && !f.Type.IsPrimitive && f.Type != SystemTypes.Decimal)
                    collectStructAccessors(isGlobalVar, (Struct)f.Type,
                                           qf, name, theClass);
            }
        }

        private void ProcessGlobals(MemberList globals)
        {
            // Locate the "Globals" struct so we can add fields to it.
            Class globalsClass = (Class)Templates.GetMemberByName(appClass.Members,
                                                                   "GlobalVars");

            // Locate the "initialization" constructor so we can add initialization
            // statements to it.
            Debug.Assert(appClass.Members[1].Name.Name == ".ctor");
            Method initCtor = (Method)appClass.Members[1];

            // Locate the WriteString method so we can add statements to write out the globals
            Method writer =
                (Method)Templates.GetMemberByName(globalsClass.Members, "WriteString");
            Method copier =
                (Method)Templates.GetMemberByName(globalsClass.Members, "CopyContents");
            Method traverser = (Method)Templates.GetMemberByName(globalsClass.Members, "TraverseFields");

            Normalizer normalizer = new Normalizer(false);

            Method getValue = (Method)Templates.GetMemberByName(globalsClass.Members, "GetValue");
            Method setValue = (Method)Templates.GetMemberByName(globalsClass.Members, "SetValue");
            System.Compiler.Switch switchGetValue =
                (System.Compiler.Switch)Templates.GetStatementTemplate("GetFieldInfoSwitch");
            getValue.Body.Statements.Add(switchGetValue);
            System.Compiler.Switch switchSetValue =
                (System.Compiler.Switch)Templates.GetStatementTemplate("SetFieldInfoSwitch");
            setValue.Body.Statements.Add(switchSetValue);

            for (int i = 0, n = globals.Count; i < n; i++)
            {
                // Make a shallow copy of the field since we're going to tinker with it
                Field f = (Field)((Field)globals[i]).Clone();
                string name = f.DeclaringType.Name.Name + "_" + f.Name.Name;

                TypeNode zingType = f.Type;

                if (GetTypeClassification(f.Type) == TypeClassification.Heap)
                    f.Type = this.ZingPtrType;
                else if (!IsPredefinedType(f.Type))
                    f.Type = new TypeExpression(new QualifiedIdentifier(
                        new Identifier("Application"), zingType.Name), f.Type.SourceContext);

                // Mangle the name to guarantee uniqueness across the globals
                f.Name = new Identifier("priv_" + name);
                f.Flags &= ~FieldFlags.Static;
                // Sriram: I have made this into a public field so that
                // the fieldInfo below works. If the field is internal
                // then fieldInfo gets set to null (there is some access
                // permission problem)
                // Need to check with Tony as to
                // whether this is tbe best solution

                // The following two lines are Tony's code
                //-----------------------------------------------------------
                // f.Flags &= (FieldFlags)(~TypeFlags.VisibilityMask);
                //f.Flags |= FieldFlags.Assembly;
                //-----------------------------------------------------------

                //This is my replacement
                f.Flags |= FieldFlags.Public;

                /*
                Identifier idFieldName = new Identifier("id_" + name);
                System.Compiler.Expression idTypeExpr =
                    new QualifiedIdentifier(
                        new QualifiedIdentifier(new Identifier("System"), new Identifier("Reflection")),
                        new Identifier("FieldInfo"));
                System.Compiler.TypeNode idfType =  new System.Compiler.TypeExpression(idTypeExpr);
                Field idf = new Field(globalsClass, null, FieldFlags.Public|FieldFlags.Static,
                                       idFieldName,
                                       idfType, null);
                idf.Initializer = Templates.GetExpressionTemplate("GetFieldInfo");
                Replacer.Replace(idf.Initializer, "_class", globalsClass.Name);
                Replacer.Replace(idf.Initializer, "_fieldName", new Literal(f.Name.Name, SystemTypes.String));
                */

                Identifier idFieldName = new Identifier("id_" + name);
                Field idf = new Field(globalsClass, null, FieldFlags.Public | FieldFlags.Static,
                    idFieldName,
                    SystemTypes.Int32, null);
                idf.Initializer = new Literal(i, SystemTypes.Int32);
                SwitchCase getCase = ((System.Compiler.Switch)Templates.GetStatementTemplate("GetFieldInfoCase")).Cases[0];
                Replacer.Replace(getCase, "_fieldId", new Literal(i, SystemTypes.Int32));
                Replacer.Replace(getCase, "_fieldName", new Identifier(f.Name.Name));
                switchGetValue.Cases.Add(getCase);

                SwitchCase setCase = ((System.Compiler.Switch)Templates.GetStatementTemplate("SetFieldInfoCase")).Cases[0];
                Replacer.Replace(setCase, "_fieldId", new Literal(i, SystemTypes.Int32));
                Replacer.Replace(setCase, "_fieldName", new Identifier(f.Name.Name));
                TypeExpression tn = f.Type as TypeExpression;
                Replacer.Replace(setCase, "_fieldType", tn != null ? tn.Expression : new Identifier(f.Type.Name.Name));
                switchSetValue.Cases.Add(setCase);

                //The last argument to the call below is a dont care
                Property accessor = GetAccessorProperty("globalAccessor", f.Type,
                                                        new Identifier(name), f.Name, idFieldName, f.Name);

                globalsClass.Members.Add(f);
                globalsClass.Members.Add(idf);
                globalsClass.Members.Add(accessor);
                f.DeclaringType = globalsClass;
                idf.DeclaringType = globalsClass;
                accessor.DeclaringType = globalsClass;
                if (accessor.Getter != null)
                {
                    globalsClass.Members.Add(accessor.Getter);
                    accessor.Getter.DeclaringType = globalsClass;
                }
                if (accessor.Setter != null)
                {
                    globalsClass.Members.Add(accessor.Setter);
                    accessor.Setter.DeclaringType = globalsClass;
                }

                if (zingType is Struct && !zingType.IsPrimitive && f.Type != SystemTypes.Decimal)
                    collectStructAccessors(true, (Struct)zingType, f.Name,
                                           name, globalsClass);

                if (f.Initializer != null)
                {
                    Statement stmt = Templates.GetStatementTemplate("InitializeGlobal");
                    Replacer.Replace(stmt, "_FieldName", f.Name);
                    Replacer.Replace(stmt, "_FieldInitializer", normalizer.VisitFieldInitializer(f.Initializer));
                    initCtor.Body.Statements.Add(stmt);
                    f.Initializer = null;
                }

                writer.Body.Statements.Add(GetWriterStatement(null, zingType, f.Name));
                copier.Body.Statements.Add(GetCopyStatement(f.Name));
                traverser.Body.Statements.Add(GetTraverserStatement(null, zingType, f.Name));
                /*if(GetTypeClassification(f.Type) == TypeClassification.Heap)
                {
                    refTraverser.Body.Statements.Add(GetTraverserStatement(null, zingType, f.Name));
                }
                */
            }
        }

        #endregion Classes (+ methods & globals)

        #region Enums

        private void GenerateEnum(EnumNode enumNode)
        {
            // Make the enum type "internal" in the generated code.
            enumNode.Flags |= TypeFlags.NotPublic;
            enumNode.Flags &= ~TypeFlags.Public;
            InstallType(enumNode);

            TypeNode enumClass = Templates.GetTypeTemplateByName("Enum");

            Debug.Assert(enumClass.Members[0] is Field);
            Field f = (Field)enumClass.Members[0];
            f.Name = new Identifier(enumNode.Name.Name + "Choices");

            choosableTypes.Add(enumNode.Name, f.Name);

            Debug.Assert(f.Initializer != null && f.Initializer is ConstructArray);
            ConstructArray consArray = (ConstructArray)f.Initializer;
            for (int i = 1, n = enumNode.Members.Count; i < n; i++)
                consArray.Initializers.Add(new QualifiedIdentifier(enumNode.Name, enumNode.Members[i].Name));

            appClass.Members.Add(f);
            f.DeclaringType = appClass;
        }

        #endregion Enums

        #region Ranges

        private void GenerateRange(Range rangeNode)
        {
            TypeNode rangeClass = Templates.GetTypeTemplateByName("Range");

            Debug.Assert(rangeClass.Members[0] is EnumNode);
            EnumNode rangeEnum = (EnumNode)rangeClass.Members[0];
            rangeEnum.Name = rangeNode.Name;
            Replacer.Replace(rangeEnum, "_MinValue", rangeNode.Min);
            Replacer.Replace(rangeEnum, "_MaxValue", rangeNode.Max);
            InstallType(rangeEnum);

            Debug.Assert(rangeClass.Members[1] is Field);
            Field f = (Field)rangeClass.Members[1];
            f.Name = new Identifier(rangeNode.Name.Name + "Choices");

            choosableTypes.Add(rangeNode.Name, f.Name);

            Debug.Assert(f.Initializer != null && f.Initializer is ConstructArray);
            ConstructArray consArray = (ConstructArray)f.Initializer;
            Debug.Assert(rangeNode.Min is Literal);
            int min = (int)((Literal)rangeNode.Min).Value;
            int max = (int)((Literal)rangeNode.Max).Value;
            for (int i = min; i <= max; i++)
                consArray.Initializers.Add(new Literal(i, SystemTypes.Int32));

            appClass.Members.Add(f);
            f.DeclaringType = appClass;
        }

        #endregion Ranges

        #region Structs

        private void GenerateStruct(Struct structNode)
        {
            TypeNode newStruct = Templates.GetTypeTemplateByName("Struct");
            Method writer = (Method)Templates.GetMemberByName(newStruct.Members, "WriteString");

            // Replace all references to the struct name
            Replacer.Replace(newStruct, newStruct.Name, structNode.Name);

            for (int i = 0, n = structNode.Members.Count; i < n; i++)
            {
                Field f = structNode.Members[i] as Field;
                if (f != null)
                {
                    // Clone the field since we might tinker with it (see other TODO below)
                    Field newField = (Field)f.Clone();

                    if (GetTypeClassification(f.Type) == TypeClassification.Heap)
                        newField.Type = this.ZingPtrType;
                    else if (!IsPredefinedType(f.Type))
                        newField.Type = new TypeExpression(new QualifiedIdentifier(
                            new Identifier("Application"), f.Type.Name), f.Type.SourceContext);

                    newStruct.Members.Add(newField);
                    newField.DeclaringType = newStruct;

                    writer.Body.Statements.Add(GetWriterStatement("this", f.Type, newField.Name));
                }
            }

            // TODO: Add a CompareTo method to the struct (low priority)

            string s = writer.FullName;
            InstallType(newStruct);
        }

        #endregion Structs

        #region Sets

        private void GenerateSet(Set setNode)
        {
            Expression ns = null;
            string setStyle = null;

            if (setNode == null || setNode.SetType == null)
                return;

            switch (GetTypeClassification(setNode.SetType))
            {
                case TypeClassification.Simple:
                    setStyle = "SimpleSet";
                    ns = setNode.SetType.Namespace;
                    break;

                case TypeClassification.Enum:
                    setStyle = "EnumSet";
                    break;

                case TypeClassification.Struct:
                    setStyle = "StructSet";
                    break;

                case TypeClassification.Heap:
                    setStyle = "ComplexSet";
                    if (setNode.SetType == SystemTypes.Object)
                        ns = setNode.SetType.Namespace;
                    break;
            }

            Class setClass = (Class)Templates.GetTypeTemplateByName(setStyle);

            Replacer.Replace(setClass, setStyle, setNode.Name);

            if (ns == null)
                ns = new QualifiedIdentifier(new Identifier("Microsoft.Zing"), new Identifier("Application"));

            Replacer.Replace(setClass, "_ElementType", new QualifiedIdentifier(ns, setNode.SetType.Name));
            SetTypeId(setClass);
            InstallType(setClass);
        }

        #endregion Sets

        #region Arrays

        private void GenerateArray(ZArray arrayNode)
        {
            Expression ns = null;
            string arrayStyle = null;

            if (arrayNode == null || arrayNode.domainType == null || arrayNode.ElementType == null)
                return;

            switch (GetTypeClassification(arrayNode.ElementType))
            {
                case TypeClassification.Simple:
                    ns = arrayNode.ElementType.Namespace;
                    arrayStyle = "SimpleArray"; break;
                case TypeClassification.Enum:
                    arrayStyle = "EnumArray"; break;
                case TypeClassification.Struct:
                    arrayStyle = "StructArray"; break;
                case TypeClassification.Heap:
                    if (arrayNode.ElementType == SystemTypes.Object)
                        ns = arrayNode.ElementType.Namespace;
                    arrayStyle = "ComplexArray"; break;
            }
            Class arrayClass = (Class)Templates.GetTypeTemplateByName(arrayStyle);

            if (ns == null)
                ns = new QualifiedIdentifier(new Identifier("Microsoft.Zing"), new Identifier("Application"));

            Replacer.Replace(arrayClass, arrayStyle, arrayNode.Name);
            Replacer.Replace(arrayClass, "_ElementType", new QualifiedIdentifier(ns, arrayNode.ElementType.Name));
            // Replacer.Replace(arrayClass, "_size", new Literal(arrayNode.Sizes[0], SystemTypes.Int32));
            SetTypeId(arrayClass);
            InstallType(arrayClass);
        }

        #endregion Arrays

        #region Channels

        private void GenerateChan(Chan chanNode)
        {
            Expression ns = null;
            string chanStyle = null;

            switch (GetTypeClassification(chanNode.ChannelType))
            {
                case TypeClassification.Simple:
                    ns = chanNode.ChannelType.Namespace;
                    chanStyle = "SimpleChan"; break;
                case TypeClassification.Enum:
                    chanStyle = "EnumChan"; break;
                case TypeClassification.Struct:
                    chanStyle = "StructChan"; break;
                case TypeClassification.Heap:
                    if (chanNode.ChannelType == SystemTypes.Object)
                        ns = chanNode.ChannelType.Namespace;
                    chanStyle = "ComplexChan"; break;
            }
            Class chanClass = (Class)Templates.GetTypeTemplateByName(chanStyle);

            if (ns == null)
                ns = new QualifiedIdentifier(new Identifier("Microsoft.Zing"), new Identifier("Application"));

            Replacer.Replace(chanClass, chanStyle, chanNode.Name);
            Replacer.Replace(chanClass, "_ElementType", new QualifiedIdentifier(ns, chanNode.ChannelType.Name));
            SetTypeId(chanClass);
            InstallType(chanClass);
        }

        #endregion Channels

        #region Application-level code-gen

        private void InstallType(TypeNode type)
        {
            appClass.Members.Add(type);
            type.DeclaringType = appClass;
            type.Flags = (type.Flags & ~TypeFlags.VisibilityMask) | TypeFlags.NestedFamORAssem;
        }

        private void SetTypeId(TypeNode type)
        {
            foreach (Member m in type.Members)
            {
                Field f = m as Field;
                if (f != null && f.Name.Name == "typeId")
                {
                    Debug.Assert(f.Initializer is Literal);
                    f.Initializer = new Literal(this.nextTypeId++); // LJW: construct a new literal, rather than modifying the old
                    return;
                }
            }
            Debug.Assert(false);
            throw new ApplicationException("Can't find typeId field");
        }

        private void SetSourceStrings(Compilation compilation)
        {
            Field sourceField = (Field)Templates.GetMemberByName(appClass.Members, "sources");
            Field filesField = (Field)Templates.GetMemberByName(appClass.Members, "sourceFiles");

            Debug.Assert(sourceField.Initializer is ConstructArray);
            ExpressionList sourceList = ((ConstructArray)sourceField.Initializer).Initializers;

            Debug.Assert(filesField.Initializer is ConstructArray);
            ExpressionList filesList = ((ConstructArray)filesField.Initializer).Initializers;

            string source = string.Empty;
            string file = string.Empty;

            for (int i = 0, n = compilation.CompilationUnits.Count; i < n; i++)
            {
                CompilationUnit cu = compilation.CompilationUnits[i];

                if (((Namespace)cu.Nodes[0]).Types.Count > 0)
                {
                    if (((Namespace)cu.Nodes[0]).Types[0].SourceContext.Document != null)
                    {
                        this.sourceDocuments.Add(((Namespace)cu.Nodes[0]).Types[0].SourceContext.Document, i);
                        DocumentText sourceText = ((Namespace)cu.Nodes[0]).Types[0].SourceContext.Document.Text;
                        source = sourceText.Substring(0, sourceText.Length);
                        file = ((Namespace)cu.Nodes[0]).Types[0].SourceContext.Document.Name;
                        sourceList.Add(new Literal(source, SystemTypes.String));
                        filesList.Add(new Literal(file, SystemTypes.String));
                    }
                }
            }
        }

        private void SetExceptionList()
        {
            EnumNode e = (EnumNode)Templates.GetMemberByName(appClass.Members, "Exceptions");

            int nextValue = 0;
            foreach (DictionaryEntry de in this.exceptionNames)
            {
                Identifier id = new Identifier((string)de.Key);
                Field f = new Field(e, null, FieldFlags.Public | FieldFlags.Literal | FieldFlags.Static | FieldFlags.HasDefault, id, e, null);
                f.Initializer = new Literal(nextValue++, SystemTypes.Int32);
                e.Members.Add(f);
                f.DeclaringType = e;
            }
        }

        private void GenerateTypeChoiceHelper()
        {
            TypeNode choiceHelperClass = Templates.GetTypeTemplateByName("ChoiceHelper");
            Debug.Assert(choiceHelperClass.Members[0] is Method);
            Method choiceHelperMethod = (Method)choiceHelperClass.Members[0];
            appClass.Members.Add(choiceHelperMethod);
            choiceHelperMethod.DeclaringType = appClass;

            if (choosableTypes.Count == 0)
            {
                choiceHelperMethod.Body.Statements.Add(Templates.GetStatementTemplate("ChoiceHelperEnd"));
                return;
            }

            If lastIf = null;

            foreach (DictionaryEntry de in choosableTypes)
            {
                If thisIf = (If)Templates.GetStatementTemplate("ChoiceHelperBody");
                Replacer.Replace(thisIf, "_ChoiceType", (Node)de.Key);
                Replacer.Replace(thisIf, "_ChoiceTypeChoices", (Node)de.Value);

                if (lastIf != null)
                    lastIf.FalseBlock = new Block(new StatementList(thisIf));
                else
                    choiceHelperMethod.Body.Statements.Add(thisIf);

                lastIf = thisIf;
            }

            lastIf.FalseBlock = new Block(new StatementList(Templates.GetStatementTemplate("ChoiceHelperEnd")));
        }

        #endregion Application-level code-gen

        #region Utility methods

        private enum TypeClassification { Simple, Enum, Struct, Heap };

        private string GetTypeName(TypeNode type)
        {
            string typeName;

            if (type.Name != null)
                typeName = type.Name.Name;
            else if (type is TypeExpression)
            {
                Expression expr = ((TypeExpression)type).Expression;
                if (expr is Identifier)
                    typeName = ((Identifier)expr).Name;
                else if (expr is QualifiedIdentifier)
                    typeName = ((QualifiedIdentifier)expr).Identifier.Name;
                else
                    throw new ApplicationException("unexpected type node");
            }
            else
                throw new ApplicationException("unexpected type node");

            return typeName;
        }

        private TypeClassification GetTypeClassification(TypeNode type)
        {
            // We see references on output parameters - they aren't interesting
            while (type is Reference)
                type = ((Reference)type).ElementType;

            if (type == this.ZingPtrType)
                return TypeClassification.Heap;

            string typeName = GetTypeName(type);

            for (int c = 0, nc = this.cZing.CompilationUnits.Count; c < nc; c++)
            {
                for (int t = 0, nTypes = ((Namespace)this.cZing.CompilationUnits[c].Nodes[0]).Types.Count; t < nTypes; t++)
                {
                    TypeNode tn = ((Namespace)this.cZing.CompilationUnits[c].Nodes[0]).Types[t];

                    if (tn.Name.Name == typeName)
                    {
                        if (tn is Set)
                            return TypeClassification.Heap;
                        else if (tn is Chan)
                            return TypeClassification.Heap;
                        else if (tn is Struct)
                            return TypeClassification.Struct;
                        else if (tn is EnumNode)
                            return TypeClassification.Enum;
                        else if (tn is Range)
                            return TypeClassification.Simple;
                        else
                            return TypeClassification.Heap;
                    }
                }
            }

            if (type.TypeCode == TypeCode.Object)
                return TypeClassification.Heap;
            else
                return TypeClassification.Simple;
        }

        private Statement GetCloneStatement(Identifier varName)
        {
            Statement stmt = Templates.GetStatementTemplate("CloneField");
            Replacer.Replace(stmt, "_FieldName", varName);
            return stmt;
        }

        private Statement GetCopyStatement(Identifier varName)
        {
            Statement stmt = Templates.GetStatementTemplate("CopyField");
            Replacer.Replace(stmt, "_FieldName", varName);
            return stmt;
        }

        private Statement GetTraverserStatement(string prefix, TypeNode type, Identifier varName)
        {
            Expression fullName;

            if (prefix != null)
            {
                if (prefix == "this")
                {
                    fullName = Templates.GetExpressionTemplate("SimpleFieldRef");
                    Replacer.Replace(fullName, "_fieldName", varName);
                }
                else
                    fullName = (Expression)new QualifiedIdentifier(new Identifier(prefix), varName);
            }
            else
                fullName = (Expression)varName;

            Statement travStmt = Templates.GetStatementTemplate("FieldTraverser");
            Replacer.Replace(travStmt, "_Name", fullName);
            return travStmt;
        }

        private Statement GetWriterStatement(string prefix, TypeNode type, Identifier varName)
        {
            Expression fullName;
            Statement writeStmt = null;

            TypeClassification typeClass = GetTypeClassification(type);

            if (prefix != null)
            {
                if (prefix == "this")
                {
                    fullName = Templates.GetExpressionTemplate("SimpleFieldRef");
                    Replacer.Replace(fullName, "_fieldName", varName);
                }
                else
                    fullName = (Expression)new QualifiedIdentifier(new Identifier(prefix), varName);
            }
            else
                fullName = (Expression)varName;

            switch (typeClass)
            {
                case TypeClassification.Simple:
                    // TODO: Sriram -- Clean this up and make this part of the classification
                    if (type.FullName == "System.String")
                        writeStmt = Templates.GetStatementTemplate("ConditionalWriter");
                    else
                        writeStmt = Templates.GetStatementTemplate("SimpleWriter");
                    break;

                case TypeClassification.Enum:
                    writeStmt = Templates.GetStatementTemplate("EnumWriter");
                    break;

                case TypeClassification.Struct:
                    writeStmt = Templates.GetStatementTemplate("StructWriter");
                    break;

                case TypeClassification.Heap:
                    writeStmt = Templates.GetStatementTemplate("ComplexWriter");
                    break;
            }

            Replacer.Replace(writeStmt, "_Name", fullName);

            return writeStmt;
        }

        private bool HeapUsed()
        {
            for (int c = 0, nc = this.cZing.CompilationUnits.Count; c < nc; c++)
            {
                for (int t = 0, nTypes = ((Namespace)this.cZing.CompilationUnits[c].Nodes[0]).Types.Count; t < nTypes; t++)
                {
                    TypeNode node = ((Namespace)this.cZing.CompilationUnits[c].Nodes[0]).Types[t];

                    if (node is Set || node is ZArray || node is Chan)
                        return true;

                    if (node is Class)
                    {
                        for (int m = 0, nMembers = node.Members.Count; m < nMembers; m++)
                        {
                            if (node.Members[m] != null && !node.Members[m].IsStatic)
                                return true;
                        }
                    }
                }
            }
            return false;
        }

        #endregion Utility methods
    }

    internal static class Templates
    {
        public static Module module;

        private static CompilationUnit cuParts;
        private static CompilationUnit cuExpressions;
        private static CompilationUnit cuStatements;
        private static CompilationUnit cuProperties;

        static public void InitializeTemplates()
        {
            // Compile the templates from which we'll duplicate nodes later.
            cuParts = Templates.CompileTemplate("TemplateParts");
            cuExpressions = Templates.CompileTemplate("TemplateExprs");
            cuStatements = Templates.CompileTemplate("TemplateStmts");
            cuProperties = Templates.CompileTemplate("TemplateProps");
        }

        static public void ReleaseTemplates()
        {
            cuParts = null;
            cuExpressions = null;
            cuStatements = null;
            cuProperties = null;
        }

        public static CompilationUnit GetApplicationTemplate(Module targetModule, CompilerParameters options, bool hasGlobals, bool usesHeap)
        {
            CSOPTIONS csOptions = new CSOPTIONS();
            csOptions.OutputAssembly = options.OutputAssembly;
            csOptions.AllowUnsafeCode = true;

#if REDUCE_LOCAL_ACCESS
            csOptions.DefinedPreProcessorSymbols = new StringList(1);
            csOptions.DefinedPreProcessorSymbols.Add("REDUCE_LOCAL_ACCESS");
#endif

            Stream templateStream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(
                "Microsoft.Zing.Template.cs");
            System.IO.StreamReader reader = new StreamReader(templateStream, true);
            string src = reader.ReadToEnd();

            Document doc = new Document("Template", 1, src, SymDocumentType.Text,
                typeof(DebuggerLanguage).GUID, SymLanguageVendor.Microsoft);

            System.Compiler.ErrorNodeList errorNodes = new ErrorNodeList();

            CS.Parser parser = new CS.Parser(doc, errorNodes, targetModule, csOptions);

            CompilationUnit cu = parser.ParseCompilationUnit(src, "Template.cs", csOptions, errorNodes, null);

            if (errorNodes.Count > 0)
                throw new ApplicationException("Internal error: application template fails to compile");

            return cu;
        }

        private static CompilationUnit CompileTemplate(string templateName, params object[] additionalDefines)
        {
            CSOPTIONS csOptions = new CSOPTIONS();
            csOptions.OutputAssembly = @"c:\temp.dll";
            csOptions.AllowUnsafeCode = true;
#if REDUCE_LOCAL_ACCESS
            const bool reduceLocalAccess = true;
#else
            const bool reduceLocalAccess = false;
#endif

            if (additionalDefines.Length > 0 || reduceLocalAccess)
            {
                csOptions.DefinedPreProcessorSymbols = new StringList();

                if (reduceLocalAccess)
                    csOptions.DefinedPreProcessorSymbols.Add("REDUCE_LOCAL_ACCESS");

                foreach (string ppSym in additionalDefines)
                    csOptions.DefinedPreProcessorSymbols.Add(ppSym);
            }

            Stream templateStream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(
                "Microsoft.Zing." + templateName + ".cs");
            System.IO.StreamReader reader = new StreamReader(templateStream, true);
            string src = reader.ReadToEnd();

            Document doc = new Document(templateName, 1, src, SymDocumentType.Text,
                typeof(DebuggerLanguage).GUID, SymLanguageVendor.Microsoft);

            System.Compiler.ErrorNodeList errorNodes = new ErrorNodeList();

            Module module = (new CS.Compiler()).CreateModule(csOptions, errorNodes);

            CS.Parser parser = new CS.Parser(doc, errorNodes, module, csOptions);

            CompilationUnit cu = parser.ParseCompilationUnit(src, templateName + ".cs", csOptions, errorNodes, null);

            if (errorNodes.Count > 0)
                throw new ApplicationException("Internal error: template fails to compile");

            return cu;
        }

        public static Statement GetStatementTemplate(string name)
        {
            for (int i = 0, n = ((Namespace)cuStatements.Nodes[0]).NestedNamespaces[0].Types[0].Members.Count; i < n; i++)
            {
                Method m = ((Namespace)cuStatements.Nodes[0]).NestedNamespaces[0].Types[0].Members[i] as Method;

                if (m != null && m.Name.Name == name)
                {
                    CS.Duplicator duplicator = new CS.Duplicator(module, null);
                    duplicator.SkipBodies = false;
                    StatementList stmtList = duplicator.VisitStatementList(m.Body.Statements);
                    return stmtList[0];
                }
            }
            throw new ArgumentException(string.Format("Statement template '{0}' not found", name));
        }

        public static StatementList GetStatementsTemplate(string name)
        {
            for (int i = 0, n = ((Namespace)cuStatements.Nodes[0]).NestedNamespaces[0].Types[0].Members.Count; i < n; i++)
            {
                Method m = ((Namespace)cuStatements.Nodes[0]).NestedNamespaces[0].Types[0].Members[i] as Method;

                if (m != null && m.Name.Name == name)
                {
                    CS.Duplicator duplicator = new CS.Duplicator(module, null);
                    duplicator.SkipBodies = false;
                    return duplicator.VisitStatementList(m.Body.Statements);
                }
            }
            throw new ArgumentException(string.Format("Statement template '{0}' not found", name));
        }

        public static Property GetPropertyTemplate(string name)
        {
            int i, n = ((Namespace)cuProperties.Nodes[0]).NestedNamespaces[0].Types[0].Members.Count;
            for (i = 0; i < n; i++)
            {
                Property p = ((Namespace)cuProperties.Nodes[0]).NestedNamespaces[0].Types[0].Members[i] as Property;

                if (p != null && p.Name.Name == name)
                {
                    CS.Duplicator duplicator = new CS.Duplicator(module, null);
                    duplicator.SkipBodies = false;
                    return duplicator.VisitProperty(p);
                }
            }
            throw new ArgumentException
                (string.Format("Property template '{0}' not found", name));
        }

        public static Expression GetExpressionTemplate(string name)
        {
            for (int i = 0, n = ((Namespace)cuExpressions.Nodes[0]).NestedNamespaces[0].Types[0].Members.Count; i < n; i++)
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

            for (int i = 0, n = ((Namespace)template.Nodes[0]).NestedNamespaces[0].Types[0].Members.Count; i < n; i++)
            {
                Member m = ((Namespace)template.Nodes[0]).NestedNamespaces[0].Types[0].Members[i];

                if (!(m is TypeNode))
                    continue;

                TypeNode tn = (TypeNode)m;

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
            for (int i = 0, n = members.Count; i < n; i++)
            {
                if (members[i] != null && members[i].Name.Name == name)
                    return members[i];
            }

            throw new ApplicationException(string.Format("Member '{0}' not found", name));
        }

        public static int GetMemberIndexByName(MemberList members, string name)
        {
            for (int i = 0, n = members.Count; i < n; i++)
            {
                if (members[i] != null && members[i].Name.Name == name)
                    return i;
            }

            throw new ApplicationException(string.Format("Member '{0}' not found", name));
        }
    }
}