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
        [EditorBrowsable(EditorBrowsableState.Never)]
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

    /// <summary>
    /// The base class for attributes that serve only as event generators.
    /// </summary>
    /// <remarks>
    /// This class is the base for attributes that serve only as event
    /// generators and do not provide "context" information for a
    /// statement. The constructor parameters for such an attribute may
    /// include arbitrary Zing expressions.
    /// </remarks>
    [AttributeUsage(AttributeTargets.All)]
    [ComVisible(true)]
    public abstract class ZingTraceAttribute : ZingAttributeBaseAttribute
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
        [EditorBrowsable(EditorBrowsableState.Never)]
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
            [EditorBrowsable(EditorBrowsableState.Never)]
            public override string ToString()
            {
                return string.Format(CultureInfo.CurrentUICulture, "SourceContextAttribute: {0}, {1}, {2}", file, startLine, endLine);
            }

            /// <exclude/>
            [EditorBrowsable(EditorBrowsableState.Never)]
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

        /// <exclude/>
        [AttributeUsage(AttributeTargets.All)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        sealed public class VBContextAttribute : ZingAttribute
        {
            private string file;
            public string File { get { return file; } }

            private int startLine;
            public int StartLine { get { return startLine; } }

            private int startCol;
            public int StartCol { get { return startCol; } }

            private int endLine;
            public int EndLine { get { return endLine; } }

            private int endCol;
            public int EndCol { get { return endCol; } }

            public VBContextAttribute(string file, int startLine, int startCol,
                int endLine, int endCol)
            {
                this.file = file;
                this.startLine = startLine;
                this.startCol = startCol;
                this.endLine = endLine;
                this.endCol = endCol;
            }

            public override string ToString()
            {
                return string.Format(CultureInfo.CurrentUICulture, "VBContextAttribute: '{0}': {1}/{2} - {3}/{4}",
                    file, startLine, startCol, endLine, endCol);
            }

            public override void ToXml(XmlElement parent)
            {
                XmlDocument doc = parent.OwnerDocument;

                XmlElement elem = doc.CreateElement("VBContextAttribute");
                parent.AppendChild(elem);

                XmlElement elemData = doc.CreateElement("file");
                elemData.InnerText = this.file;
                elem.AppendChild(elemData);

                elemData = doc.CreateElement("startLine");
                elemData.InnerText = this.startLine.ToString(CultureInfo.CurrentUICulture);
                elem.AppendChild(elemData);

                elemData = doc.CreateElement("startCol");
                elemData.InnerText = this.startCol.ToString(CultureInfo.CurrentUICulture);
                elem.AppendChild(elemData);

                elemData = doc.CreateElement("endLine");
                elemData.InnerText = this.endLine.ToString(CultureInfo.CurrentUICulture);
                elem.AppendChild(elemData);

                elemData = doc.CreateElement("endCol");
                elemData.InnerText = this.endCol.ToString(CultureInfo.CurrentUICulture);
                elem.AppendChild(elemData);
            }
        }

        /// <exclude/>
        [AttributeUsage(AttributeTargets.All)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        sealed public class WinOEContextAttribute : ZingAttribute
        {
            private string fileName;
            public string FileName { get { return fileName; } }

            private string guid;
            public string Guid { get { return guid; } }

            public WinOEContextAttribute(string fileName, string guid)
            {
                this.fileName = fileName;
                this.guid = guid;
            }

            public override string ToString()
            {
                return string.Format(CultureInfo.CurrentUICulture, "WinOEContextAttribute: '{0}': {1}",
                    fileName, guid);
            }

            public override void ToXml(XmlElement parent)
            {
                XmlDocument doc = parent.OwnerDocument;

                XmlElement elem = doc.CreateElement("WinOEContextAttribute");
                parent.AppendChild(elem);

                XmlElement elemData = doc.CreateElement("fileName");
                elemData.InnerText = this.fileName;
                elem.AppendChild(elemData);

                elemData = doc.CreateElement("guid");
                elemData.InnerText = this.guid;
                elem.AppendChild(elemData);
            }
        }

        /// <summary>
        /// This attribute is used to add application-specific information to an execution
        /// trace. It works much like the Zing "trace" statement.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1019:DefineAccessorsForAttributeArguments")]
        [CLSCompliant(false)]
        [AttributeUsage(AttributeTargets.All)]
        sealed public class TraceAttribute : ZingTraceAttribute
        {
            private string message;
            private object[] arguments;

            /// <summary>
            /// Construct a TraceAttribute from a message string and an optional list of
            /// additional arguments. By convention, the message argument is a .Net-style
            /// format string for the remaining arguments.
            /// </summary>
            /// <param name="message">By convention, a format string for the remaining arguments</param>
            /// <param name="arguments">Optional additional arguments</param>
            public TraceAttribute(string message, params object[] arguments)
            {
                this.message = message;
                this.arguments = arguments;
            }

            /// <summary>
            /// Returns the first parameter of the trace statement which is required to be
            /// a string literal.
            /// </summary>
            public string Message { get { return message; } }

            /// <summary>
            /// Returns an enumerator for the trace parameters.
            /// </summary>
            public IEnumerator Arguments { get { return arguments.GetEnumerator(); } }

            /// <summary>
            /// Returns an array of objects containing any additional parameters provided
            /// in the trace statement (may contain zero elements).
            /// </summary>
            /// <returns>Array of trace statement parameters</returns>
            public object[] GetArgumentsArray() { return arguments; }

            /// <summary>
            /// Formats the trace event by treating the first parameter as a format string
            /// for any additional parameters.
            /// </summary>
            /// <returns>A formatted string based on the parameters of the trace statement.</returns>
            [EditorBrowsable(EditorBrowsableState.Never)]
            public override string ToString()
            {
                return string.Format(CultureInfo.CurrentUICulture, message, arguments);
            }

            /// <exclude/>
            [EditorBrowsable(EditorBrowsableState.Never)]
            public override void ToXml(XmlElement parent)
            {
                XmlDocument doc = parent.OwnerDocument;

                XmlElement elem = doc.CreateElement("TraceAttribute");
                parent.AppendChild(elem);

                XmlElement elemFormatMsg = doc.CreateElement("FormattedMessage");
                elemFormatMsg.InnerText = string.Format(CultureInfo.CurrentUICulture, message, arguments);
                elem.AppendChild(elemFormatMsg);

                XmlElement elemMsg = doc.CreateElement("Message");
                elemMsg.InnerText = this.Message;
                elem.AppendChild(elemMsg);

                if (arguments.Length > 0)
                {
                    XmlElement elemArgs = doc.CreateElement("Arguments");
                    elem.AppendChild(elemArgs);

                    foreach (object arg in arguments)
                    {
                        XmlElement elemArg = doc.CreateElement("Argument");
                        elemArg.InnerText = arg.ToString();
                        elemArgs.AppendChild(elemArg);
                    }
                }
            }
        }
    }
}