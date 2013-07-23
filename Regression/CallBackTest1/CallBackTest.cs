using Z = Microsoft.Zing;
using Y = Microsoft.Zap;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Xml;

[assembly: AssemblyTitle(@"")]
[assembly: AssemblyDescription(@"")]
[assembly: AssemblyConfiguration(@"")]
[assembly: AssemblyCompany(@"")]
[assembly: AssemblyProduct(@"")]
[assembly: AssemblyCopyright(@"")]
[assembly: AssemblyTrademark(@"")]
[assembly: AssemblyCulture(@"")]
[assembly: AssemblyVersion(@"1.0.*")]
[assembly: AssemblyDelaySign(false)]
[assembly: CLSCompliant(false)]
[assembly: ComVisible(false)]

namespace Microsoft.Zing
{
    public class ___Virtualizer
    : Z.ZingClass
    {                
        /*** SOME PREAMBLE ****/        

        public ___Virtualizer(StateImpl application)
            : base(application)
        {            
        }
        public ___Virtualizer()
        {
            // Never use this constructor !!!
            // (It does not initialize the Application property)
            Debug.Assert(false);
        }
        private ___Virtualizer(___Virtualizer c)
            : base(c)
        {            
        }
        protected override short TypeId
        {
            get
            {
                return ___Virtualizer.typeId;
            }
        }
        //private static readonly short typeId = getNextLibTypeIdCount();
        private static readonly short typeId = 2;
        /**** USER CODE BEGINS HERE *****************/
        //  ***really private, and owned, never leaked, invisibe otherwise*** member variables
        
        // nothing here

        //member functions
        
        public void ___DoVirtualCall(Process p, Z.Pointer runnableObject, string methodName, Z.Pointer par)
        {
            Debug.Assert(runnableObject != 0);
            HeapElement hel = Application.LookupObject(runnableObject);
            Debug.Assert(hel != null);            
            Type run_method =  hel.GetType().GetNestedType("___" + methodName, BindingFlags.NonPublic);
            Debug.Assert(run_method != null);
            Debug.Assert(run_method.IsClass);
            ConstructorInfo constr = run_method.GetConstructor(new Type[] { Application.GetType() });
            Debug.Assert(constr != null);
            ZingMethod callee = (ZingMethod)constr.Invoke(new Object[] { Application });
            Debug.Assert(callee != null);            
            FieldInfo inputs = callee.GetType().GetField("inputs");            
            Debug.Assert(inputs != null);
            object inputsValue = inputs.GetValue(callee);
            Debug.Assert(inputsValue != null);                                    
            FieldInfo[] formal_parameters = inputsValue.GetType().GetFields();
            Debug.Assert(formal_parameters.Length == 2); // priv____... and id____...
            FieldInfo formal_parameter = null;
            foreach (FieldInfo fp in formal_parameters)
            {
                Debug.Assert(fp != null);
                if (fp.Name.ToString().Substring(0, 8).Equals("priv____"))
                {
                    formal_parameter = fp;
                    break;
                }
            }
            Debug.Assert(formal_parameter != null);
            Debug.Assert(formal_parameter.FieldType.FullName.Equals("Microsoft.Zing.Pointer"));

            formal_parameter.SetValue(inputsValue, par);
            callee.This = runnableObject;
            p.Call(callee);
            Application.IsCall = true;
        }

        public Z.Pointer ___GetResultForVirtualCall(Process p)
        {
            Z.Pointer result;
            
            FieldInfo outputs = p.LastFunctionCompleted.GetType().GetField("outputs");
            Debug.Assert(outputs != null);
            object outputsValue = outputs.GetValue(p.LastFunctionCompleted);
            Debug.Assert(outputsValue != null);
            PropertyInfo _Lfc_ReturnValue = outputsValue.GetType().GetProperty("_Lfc_ReturnValue");                                
            Debug.Assert(_Lfc_ReturnValue != null);
            MethodInfo getter = _Lfc_ReturnValue.GetGetMethod();
            Debug.Assert(getter != null);
            result = (Z.Pointer)getter.Invoke(outputsValue, new object[0]);            
            
            p.LastFunctionCompleted = null;
            
            return result;
        }
        


        //***************USER WRITTEN SUPPORT CODE FOR FINGERPRINT**************************************
        public override void WriteString(StateImpl state, BinaryWriter bw)
        {
            bw.Write(this.TypeId);            
        }

        //***************FOR NOW, USER WRITTEN SUPPORT CODE**************************************
        public override void TraverseFields(FieldTraverser ft)
        {
            ft.DoTraversal(this.TypeId);            
        }

        //***************USER WRITTEN SUPPOxRT CODE**************************************
        public override object Clone()
        {
            ___Virtualizer newObj = new ___Virtualizer(this);
            return newObj;
        }

        public override object GetValue(int fieldIndex)
        {
            Debug.Assert(false);
            return null;
        }
        public override void SetValue(int fieldIndex, object value)
        {
            Debug.Assert(false);
        }

    };    

}