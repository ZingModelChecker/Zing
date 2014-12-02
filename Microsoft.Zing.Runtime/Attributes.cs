using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Xml;

namespace Microsoft.Zing
{
    /// <summary>
    /// The base class for all Zing attributes.
    /// </summary>
    [ComVisible(true)]
    public abstract class ZingAttributeBaseAttribute : Attribute
    {
        /// <exclude/>
        
        public abstract void ToXml(XmlElement parent);
    }

    /// <summary>
    /// The base class for attributes used to correlate with foreign source code.
    /// </summary>
    /// <remarks>
    /// This class is used as the base class for attributes that serve both
    /// as context markers and event generators. The constructor parameters
    /// for attributes derived from this class may include only string and
    /// integer literals.
    /// </remarks>
    [ComVisible(true)]
    public abstract class ZingAttribute : ZingAttributeBaseAttribute
    {
    }

    // For testing, and simple usage scenarios, we provide a couple of simple
    // attributes.
    namespace Attributes
    {
#if DEBUG
        /// <summary>
        /// This namespace contains attributes supported by the Zing compiler and runtime.
        /// These attributes are currently used to annotate Zing statements, but may be
        /// extended for other uses in the future.
        /// </summary>
        public class NamespaceDoc 
        {
        }
#endif

        /// <summary>
        /// This attribute is used to correlate foreign source code with Zing statements.
        /// Foreign source code locations are identified by a file path, a starting line
        /// number, and an ending line number.
        /// </summary>
        [AttributeUsage(AttributeTargets.All)]
        sealed public class SourceContextAttribute : ZingAttribute
        {
            /// <summary>
            /// The pathname of the foreign source code file.
            /// </summary>
            public string File { get { return file; } }
            private string file;

            /// <summary>
            /// The starting line number of the associated statement in the
            /// foreign source code.
            /// </summary>
            public int StartLine { get { return startLine; } }
            private int startLine;

            /// <summary>
            /// The ending line number of the associated statement in the
            /// foreign source code.
            /// </summary>
            public int EndLine { get { return endLine; } }
            private int endLine;

            /// <summary>
            /// Constructor for SourceContextAttribute
            /// </summary>
            /// <param name="file">Pathname of the foreign source code file</param>
            /// <param name="startLine">Starting line of the foreign statement</param>
            /// <param name="endLine">Ending line of the foreign statement</param>
            public SourceContextAttribute(string file, int startLine, int endLine)
            {
                this.file = file;
                this.startLine = startLine;
                this.endLine = endLine;
            }

            /// <exclude/>
            
            public override string ToString()
            {
                return string.Format(CultureInfo.CurrentUICulture, "SourceContextAttribute: {0}, {1}, {2}", file, startLine, endLine);
            }

            /// <exclude/>
            
            public override void ToXml(XmlElement parent)
            {
                XmlDocument doc = parent.OwnerDocument;

                XmlElement elem = doc.CreateElement("SourceContextAttribute");
                parent.AppendChild(elem);

                XmlElement elemFile = doc.CreateElement("File");
                elemFile.InnerText = this.file;
                elem.AppendChild(elemFile);

                XmlElement elemStart = doc.CreateElement("StartLine");
                elemStart.InnerText = this.startLine.ToString(CultureInfo.CurrentUICulture);
                elem.AppendChild(elemStart);

                XmlElement elemEnd = doc.CreateElement("EndLine");
                elemEnd.InnerText = this.endLine.ToString(CultureInfo.CurrentUICulture);
                elem.AppendChild(elemEnd);
            }
        }

    }
}