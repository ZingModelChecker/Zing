using Z = Microsoft.Zing;
using Y = Microsoft.Zap;
using System;
//using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Collections;
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
    public class ___AssocArray
    : Z.ZingClass
    {
        public ___AssocArray(StateImpl application)
            : base(application)
        {
        }
        public ___AssocArray()
        {
        }
        private ___AssocArray(___AssocArray c)
            : base(c)
        {
        }
        protected override short TypeId
        {
            get
            {
                return ___AssocArray.typeId;
            }
        }
        //private static readonly short typeId = getNextLibTypeIdCount();
        private static readonly short typeId = 2;

        private Hashtable h;

        //member functions
        public void ___initialize(Process p)
        {
            SetDirty();
            h = new Hashtable();
        }

        public void ___Add(Process p, string key, Z.Pointer value)
        {                        
            SetDirty();
            h[key] = value;
        }

        public Z.Pointer ___Lookup(Process p, string key)
        {
            object result = h[key];
            if (result == null)
            {
                return new Z.Pointer(0);
            }
            else
            {
                return (Z.Pointer) result;
            }
        }

        public string ___StringHelper(Process p, string s, int i)
        {
            return s + i;
        }

        //***************USER WRITTEN SUPPORT CODE FOR FINGERPRINT**************************************
        public override void WriteString(StateImpl state, BinaryWriter bw)
        {
            bw.Write(this.TypeId);
            if (this.h != null)
            {
                foreach (Object k in h.Keys)
                {
                    String s = (String)k;
                    Z.Pointer p = (Z.Pointer) h[s];                    
                    bw.Write(s);
                    bw.Write(state.GetCanonicalId(p));
                }
            }
        }

        //***************FOR NOW, USER WRITTEN SUPPORT CODE**************************************
        public override void TraverseFields(FieldTraverser ft)
        {
            ft.DoTraversal(this.TypeId);
            if (this.h != null)
            {
                foreach (Object k in h.Keys)
                {
                    String s = (String)k;
                    Z.Pointer p = (Z.Pointer) h[s];
                    ft.DoTraversal(s);
                    ft.DoTraversal(p);
                }
            }
        }

        //***************USER WRITTEN SUPPOxRT CODE**************************************
        public override object Clone()
        {
            ___AssocArray newObj = new ___AssocArray(this);
            if (this.h != null)
            {
                newObj.h = new Hashtable();
                foreach (string s in this.h.Keys)
                {
                    Object v = this.h[s];
                    newObj.h[s] = v;
                }
            }
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

        public override void ToXml(XmlElement containerElement)
        {
            XmlDocument doc = containerElement.OwnerDocument;

            XmlElement elem = doc.CreateElement("Object");
            containerElement.AppendChild(elem);

            XmlAttribute attr;
            attr = doc.CreateAttribute("type");
            attr.Value = "Microsoft.Zing.AssocArray";
            elem.SetAttributeNode(attr);

            if (this.h != null)
            {
                foreach (Object k in h.Keys)
                {
                    XmlElement memberElem = doc.CreateElement("Field");
                    elem.AppendChild(memberElem);

                    attr = doc.CreateAttribute("name");
                    attr.Value = (string)k;
                    memberElem.SetAttributeNode(attr);
                    attr = doc.CreateAttribute("type");
                    attr.Value = "Microsoft.Zing.Pointer";
                    memberElem.SetAttributeNode(attr);
                    memberElem.InnerText = h[k].ToString();
                }
            }
        }

    }
 
}