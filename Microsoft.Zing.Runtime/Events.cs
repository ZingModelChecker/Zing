using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Xml;

namespace Microsoft.Zing
{

    /// <summary>
    /// This is the abstract class from which all Zing events are derived.
    /// <seealso cref="State.GetEvents"/>
    /// </summary>
    /// <remarks>
    /// The base class provides the properties <code>StepNumber</code>
    /// and <code>ProcName</code> which are common to all events.
    /// </remarks>
    public abstract class ZingEvent
    {
        /// <exclude/>
        private ushort stepNumber;
        /// <exclude/>
        private string procName;
        /// <exclude/>
        private ZingSourceContext context;
        /// <exclude/>
        private ZingAttribute contextAttribute;

        /// <summary>
        /// The step number during which the event was produced.
        /// </summary>
        public int StepNumber { get { return (int) stepNumber; } }

        /// <summary>
        /// The name of the Zing process that caused the event.
        /// </summary>
        public string ProcName { get { return procName; } }

        /// <summary>
        /// The Zing-level source context at which the event was generated.
        /// </summary>
        public ZingSourceContext Context { get { return context; } }

        /// <summary>
        /// The context attribute (if any) associated with the statement that
        /// generated the event.
        /// </summary>
        public ZingAttribute ContextAttribute { get { return contextAttribute; } }

        /// <exclude/>
        
        protected ZingEvent(ZingSourceContext context, ZingAttribute contextAttribute)
        {
            this.context = context;
            this.contextAttribute = contextAttribute;

            Process p = Process.CurrentProcess;

            if (p != null)
            {
                procName = Utils.Unmangle(p.Name);
                stepNumber = p.StateImpl.stepNumber;
            }
            else
            {
                procName = "<init>";
            }
        }

        
        protected ZingEvent(ZingSourceContext context, ZingAttribute contextAttribute, int SerialNum)
        {
            this.context = context;
            this.contextAttribute = contextAttribute;

            Process p = Process.GetCurrentProcess(SerialNum);

            if (p != null)
            {
                procName = Utils.Unmangle(p.Name);
                stepNumber = p.StateImpl.stepNumber;
            }
            else
            {
                procName = "<init>";
            }
        }

        /// <exclude/>
        
        public abstract void ToXml(XmlElement parent);

        /// <exclude/>
        
        protected void AddBaseData(XmlElement element)
        {
            XmlDocument doc = element.OwnerDocument;

            XmlAttribute attrStep = doc.CreateAttribute("stepNumber");
            attrStep.Value = stepNumber.ToString(CultureInfo.CurrentUICulture);
            element.SetAttributeNode(attrStep);

            XmlAttribute attrProc = doc.CreateAttribute("processName");
            attrProc.Value = (procName == "<init>") ? "" : procName;
            element.SetAttributeNode(attrProc);
        }
    }


    /// <summary>
    /// This event is generated when a "trace" statement is executed.
    /// <seealso cref="State.GetEvents"/>
    /// </summary>
    /// <remarks>
    /// Trace events are generated as the result of executing a Zing "trace" statement.
    /// The parameters in a trace statement consist of a string literal (required) and
    /// a variable-length list of additional parameters. By convention, the first
    /// parameter is treated as a format string for the remaining parameters, and the
    /// ToString method of TraceEvent makes this assumption. This is not required,
    /// however, in which case ToString will return only the first parameter.
    /// </remarks>
    public class TraceEvent : ZingEvent
    {
        private string message;
        private object[] arguments;

        
        internal TraceEvent(ZingSourceContext context, ZingAttribute contextAttribute,
            string message, params object[] arguments)
            : base(context, contextAttribute)
        {
            this.message = message;
            this.arguments = arguments;
        }

        
        internal TraceEvent(ZingSourceContext context, ZingAttribute contextAttribute,
            string message, int SerialNumber, params object[] arguments)
            : base(context, contextAttribute, SerialNumber)
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
        /// Returns an array of objects containing any additional parameters provided
        /// in the trace statement (may contain zero elements).
        /// </summary>
        /// <returns>Array of trace arguments</returns>
        public object[] GetArguments() { return arguments; }

        /// <exclude/>
        /// <summary>
        /// Formats the trace event by treating the first parameter as a format string
        /// for any additional parameters.
        /// </summary>
        /// <returns>A formatted string based on the parameters of the trace statement.</returns>
        
        public override string ToString()
        {
            return string.Format(CultureInfo.CurrentUICulture, message, arguments);
        }

        
        public override void ToXml(XmlElement parent)
        {
            XmlDocument doc = parent.OwnerDocument;

            XmlElement elem = doc.CreateElement("TraceEvent");
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

            base.AddBaseData(elem);
        }
    }

    /// <summary>
    /// This event is generated when a Zing process is created.
    /// <seealso cref="State.GetEvents"/>
    /// </summary>
    /// <remarks>
    /// This event is generated whenever a new process is created. For processes
    /// created using the "activate" qualifier, this event appears on the initial
    /// state of the model. For processes created dynamically using "async" calls,
    /// the creation event appears on the state in which the async call executed.
    /// </remarks>
    public class CreateProcessEvent : ZingEvent
    {
        private string newProcName;

        /// <exclude/>
        internal CreateProcessEvent(ZingSourceContext context, ZingAttribute contextAttribute,
            string newProcName)
            : base(context, contextAttribute)
        {
            this.newProcName = Utils.Unmangle(newProcName);
        }

        internal CreateProcessEvent(ZingSourceContext context, ZingAttribute contextAttribute,
            string newProcName, int SerialNumber)
            : base(context, contextAttribute, SerialNumber)
        {
            this.newProcName = Utils.Unmangle(newProcName);
        }

        /// <summary>
        /// Returns the name of the new process. Processes are named based on their
        /// entry point method.
        /// </summary>
        public string NewProcName { get { return newProcName; } }

        /// <exclude/>
        /// <summary>
        /// Formats the event showing the names of the new process and its parent.
        /// </summary>
        
        public override string ToString()
        {
            return string.Format(CultureInfo.CurrentUICulture, "CreateProcess - process='{0}', child='{1}'", this.ProcName, this.newProcName);
        }

        /// <exclude/>
        
        public override void ToXml(XmlElement parent)
        {
            XmlDocument doc = parent.OwnerDocument;

            XmlElement elem = doc.CreateElement("CreateProcessEvent");
            parent.AppendChild(elem);

            XmlAttribute attrNewProc = doc.CreateAttribute("newProcessName");
            attrNewProc.Value = this.newProcName;
            elem.SetAttributeNode(attrNewProc);

            base.AddBaseData(elem);
        }
    }

    /// <summary>
    /// This event is generated when a process terminates normally.
    /// <seealso cref="State.GetEvents"/>
    /// </summary>
    /// <remarks>
    /// Processes terminate normally by returning from their top-level method.
    /// </remarks>
    public class TerminateProcessEvent : ZingEvent
    {
        /// <exclude/>
        internal TerminateProcessEvent(ZingSourceContext context, ZingAttribute contextAttribute)
            : base(context, contextAttribute)
        {
        }

        internal TerminateProcessEvent(ZingSourceContext context, ZingAttribute contextAttribute, int SerialNumber)
            : base(context, contextAttribute, SerialNumber)
        {
        }

        /// <exclude/>
        /// <summary>
        /// Formats the event showing the name of the terminating process.
        /// </summary>
        
        public override string ToString()
        {
            return string.Format(CultureInfo.CurrentUICulture, "TerminateProcess - process='{0}'", this.ProcName);
        }

        /// <exclude/>
        
        public override void ToXml(XmlElement parent)
        {
            XmlDocument doc = parent.OwnerDocument;

            XmlElement elem = doc.CreateElement("TerminateProcessEvent");
            parent.AppendChild(elem);

            base.AddBaseData(elem);
        }
    }

    /// <summary>
    /// This event is generated when a "send" statement is executed.
    /// </summary>
    /// <remarks>
    /// A send event is generated when a process executes a send statement. The channel
    /// is available as a uint value corresponding to its Zing pointer. The message
    /// data is available as an object. The channel type is also available, primarily
    /// as a means of accessing the type name.
    /// </remarks>
    public class SendEvent : ZingEvent
    {
        private Pointer chanPtr;
        private object data;
        private Type chanType;

        /// <exclude/>
        internal SendEvent(ZingSourceContext context, ZingAttribute contextAttribute,
            ZingChan chan, object data)
            : base(context, contextAttribute)
        {
            this.data = data;
            this.chanPtr = Process.CurrentProcess.StateImpl.ReverseLookupObject(chan);
            this.chanType = chan.GetType();
        }

        internal SendEvent(ZingSourceContext context, ZingAttribute contextAttribute,
            ZingChan chan, object data, int SerialNumber)
            : base(context, contextAttribute, SerialNumber)
        {
            this.data = data;
            this.chanPtr = Process.GetCurrentProcess(SerialNumber).StateImpl.ReverseLookupObject(chan);
            this.chanType = chan.GetType();
        }



        /// <summary>
        /// Returns the integer value corresponding to the channel's address in the Zing heap.
        /// </summary>
        /// <remarks>
        /// This isn't useful in itself, but it does provide a way of distinguishing
        /// between different channels of the same type.
        /// </remarks>
        public int ChanPtr { get { return (int) (uint) chanPtr; } }

        /// <summary>
        /// Returns the data being transmitted through the channel as an object.
        /// </summary>
        /// <remarks>
        /// This is most useful for channels of simple types.
        /// </remarks>
        public object Data { get { return data; } }

        /// <summary>
        /// Returns the .Net type of the channel.
        /// </summary>
        public Type ChanType { get { return chanType; } }

        /// <exclude/>
        /// <summary>
        /// Formats the Send event showing the channel type, number, and message data.
        /// </summary>
        
        public override string ToString()
        {
            return string.Format(CultureInfo.CurrentUICulture, "Send(chan='{0}({1})', data='{2}')", chanType, chanPtr, data);
        }

        /// <exclude/>
        
        public override void ToXml(XmlElement parent)
        {
            XmlDocument doc = parent.OwnerDocument;

            XmlElement elem = doc.CreateElement("SendEvent");
            parent.AppendChild(elem);

            XmlElement elemChanType = doc.CreateElement("ChannelType");
            elemChanType.InnerText = chanType.ToString();
            elem.AppendChild(elemChanType);

            XmlElement elemChanPtr = doc.CreateElement("ChannelPointer");
            elemChanPtr.InnerText = chanPtr.ToString();
            elem.AppendChild(elemChanPtr);

            XmlElement elemData = doc.CreateElement("Data");
            elemData.InnerText = data.ToString();
            elem.AppendChild(elemData);

            base.AddBaseData(elem);
        }
    }

    /// <summary>
    /// This event is generated when a process consumes a message.
    /// <seealso cref="State.GetEvents"/>
    /// </summary>
    /// <remarks>
    /// A receive event is generated when a process consumes a message from a channel. The channel
    /// is available as a uint value corresponding to its Zing pointer. The message
    /// data is available as an object. The channel type is also available, primarily
    /// as a means of accessing the type name.
    /// </remarks>
    public class ReceiveEvent : ZingEvent
    {
        private Pointer chanPtr;
        private object data;
        private Type chanType;

        /// <exclude/>
        internal ReceiveEvent(ZingSourceContext context, ZingAttribute contextAttribute,
            ZingChan chan, object data)
            : base (context, contextAttribute)
        {
            this.data = data;
            this.chanPtr = Process.CurrentProcess.StateImpl.ReverseLookupObject(chan);
            this.chanType = chan.GetType();
        }

        internal ReceiveEvent(ZingSourceContext context, ZingAttribute contextAttribute,
            ZingChan chan, object data, int SerialNumber)
            : base(context, contextAttribute, SerialNumber)
        {
            this.data = data;
            this.chanPtr = Process.CurrentProcess.StateImpl.ReverseLookupObject(chan);
            this.chanType = chan.GetType();
        }


        /// <summary>
        /// Returns the integer value corresponding to the channel's address in the Zing heap.
        /// </summary>
        /// <remarks>
        /// This isn't useful in itself, but it does provide a way of distinguishing
        /// between different channels of the same type.
        /// </remarks>
        public int ChanPtr { get { return (int) (uint) chanPtr; } }

        /// <summary>
        /// Returns the data being transmitted through the channel as an object.
        /// </summary>
        /// <remarks>
        /// This is most useful for channels of simple types.
        /// </remarks>
        public object Data { get { return data; } }

        /// <summary>
        /// Returns the .Net type of the channel.
        /// </summary>
        public Type ChanType { get { return chanType; } }

        /// <exclude/>
        /// <summary>
        /// Formats the Receive event showing the channel type, number, and message data.
        /// </summary>
        
        public override string ToString()
        {
            return string.Format(CultureInfo.CurrentUICulture, "Receive(chan='{0}({1}', data='{2}')", chanType, chanPtr, data);
        }

        /// <exclude/>
        
        public override void ToXml(XmlElement parent)
        {
            XmlDocument doc = parent.OwnerDocument;

            XmlElement elem = doc.CreateElement("ReceiveEvent");
            parent.AppendChild(elem);

            XmlElement elemChanType = doc.CreateElement("ChannelType");
            elemChanType.InnerText = chanType.ToString();
            elem.AppendChild(elemChanType);

            XmlElement elemChanPtr = doc.CreateElement("ChannelPointer");
            elemChanPtr.InnerText = chanPtr.ToString();
            elem.AppendChild(elemChanPtr);

            XmlElement elemData = doc.CreateElement("Data");
            elemData.InnerText = data.ToString();
            elem.AppendChild(elemData);

            base.AddBaseData(elem);
        }
    }
}