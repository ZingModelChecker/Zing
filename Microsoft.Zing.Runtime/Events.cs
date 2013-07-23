using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Xml;

namespace Microsoft.Zing
{

    public class LTSEvent
    {
        public ExternalEvent externalEvent;

        public LTSEvent(ExternalEvent e)
        {
            externalEvent = e;
        }
    }
    /// <summary>
    /// Contains information about external communication made visible with event
    /// statements and join conditions.
    /// </summary>
    /// <remarks>
    /// External events are used for refinement checking. These are used to report sends
    /// and receives on "external" channels. These events are generated when Zing executes
    /// an "event" statement or a join statement within a select that contains an "event"
    /// join condition. Because these are used during checking, they must be as efficient
    /// as possible.
    /// </remarks>
    public struct ExternalEvent
    {
        private byte    eventData;
        private byte    msgType;

        /// <exclude/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public ExternalEvent(int channel, int msgType, bool isSend)
        {
            eventData = (byte) ((channel & chanMask) | (isSend ? sendEvent : recvEvent));
            this.msgType = (byte) msgType;
        }

        /// <exclude/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public ExternalEvent(bool isTau)
        {
            eventData = tauEvent;
            msgType = 0;
        }

        private const byte statusMask = 0xc0;
        private const byte unusedEvent = 0;
        private const byte tauEvent = 0x40;
        private const byte sendEvent = 0x80;
        private const byte recvEvent = 0xc0;

        private const byte chanMask = 0x3f;

        /// <summary>
        /// Returns true if the external event slot contains an actual event.
        /// </summary>
        public bool IsUsed    { get { return (eventData & statusMask) != unusedEvent; } }
        /// <summary>
        /// Returns true if the event represents a tau (internal) event.
        /// </summary>
        public bool IsTau     { get { return (eventData & statusMask) == tauEvent; } }
        /// <summary>
        /// Returns true if the event represents an outgoing message.
        /// </summary>
        public bool IsSend    { get { return (eventData & statusMask) == sendEvent; } }
        /// <summary>
        /// Returns true if the event represents an incoming message.
        /// </summary>
        public bool IsReceive { get { return (eventData & statusMask) == recvEvent; } }
        /// <summary>
        /// Returns the number of the logical channel through which the message passed.
        /// </summary>
        public int Channel { get { return (int) (eventData & chanMask); } }
        /// <summary>
        /// Returns an integer value denoting the type of message sent or received.
        /// </summary>
        public int MessageType { get { return (int) this.msgType; } }

        /// <exclude/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator==(ExternalEvent e1, ExternalEvent e2)
        {
            return e1.eventData == e2.eventData && e1.msgType == e2.msgType;
        }

        /// <exclude/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator!=(ExternalEvent e1, ExternalEvent e2)
        {
            return e1.eventData != e2.eventData || e1.msgType != e2.msgType;
        }

        /// <exclude/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override bool Equals(object obj)
        {
            ExternalEvent other = (ExternalEvent) obj;
            return other.eventData == this.eventData && other.msgType == this.msgType;
        }

        /// <exclude/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override int GetHashCode()
        {
            return (int) this.eventData | (((int) this.msgType) << 8);
        }

        /// <exclude/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override string ToString()
        {
            if (this.IsUsed)
            {
                if (this.IsSend)
                    return string.Format(CultureInfo.CurrentUICulture, "{0}/{1}!", this.Channel, this.MessageType);
                else if (this.IsReceive)
                    return string.Format(CultureInfo.CurrentUICulture, "{0}/{1}?", this.Channel, this.MessageType);
                else
                    return "tau";
            }
            else
                return string.Empty;
        }

        /// <exclude/>
        public void ToXml(XmlElement parent)
        {
            if (!IsUsed)
                return;

            XmlDocument doc = parent.OwnerDocument;

            XmlElement eventElem = doc.CreateElement("ExternalEvent");
            parent.AppendChild(eventElem);

            XmlAttribute dir = doc.CreateAttribute("type");
            dir.Value = IsSend ? "send" : (IsReceive ? "receive" : "tau");
            eventElem.SetAttributeNode(dir);

            if (!IsTau)
            {
                XmlAttribute chan = doc.CreateAttribute("channel");
                chan.Value = Channel.ToString(CultureInfo.CurrentUICulture);
                eventElem.SetAttributeNode(chan);

                XmlAttribute type = doc.CreateAttribute("msgType");
                type.Value = MessageType.ToString(CultureInfo.CurrentUICulture);
                eventElem.SetAttributeNode(type);
            }
        }
    }

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
        [EditorBrowsable(EditorBrowsableState.Never)]
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

        [EditorBrowsable(EditorBrowsableState.Never)]
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
        [EditorBrowsable(EditorBrowsableState.Never)]
        public abstract void ToXml(XmlElement parent);

        /// <exclude/>
        [EditorBrowsable(EditorBrowsableState.Never)]
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
    /// This event is generated when an external event is produced.
    /// <seealso cref="State.GetEvents"/>
    /// </summary>
    /// <remarks>
    /// This event class corresponds directly to external events, but when full eventing is
    /// enabled, this event allows external events to be temporally ordered wrt other kinds
    /// of events.
    /// </remarks>
    public class ExternalEventEvent : ZingEvent
    {
        private ExternalEvent @event;

        /// <exclude/>
        internal ExternalEventEvent(ZingSourceContext context, ZingAttribute contextAttribute,
            ExternalEvent @event)
            : base(context, contextAttribute)
        {
            this.@event = @event;
        }

        internal ExternalEventEvent(ZingSourceContext context, ZingAttribute contextAttribute,
            ExternalEvent @event, int SerialNumber)
            : base(context, contextAttribute, SerialNumber)
        {
            this.@event = @event;
        }

        /// <summary>
        /// Returns the underlying ExternalEvent.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public ExternalEvent Event { get { return @event; } }

        /// <exclude/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override string ToString()
        {
            return @event.ToString();
        }

        /// <summary>
        /// Returns true if the event represents a "tau" action by the model.
        /// </summary>
        public bool IsTau     { get { return @event.IsTau; } }

        /// <summary>
        /// Returns true if the event represents an outgoing message on an
        /// external channel.
        /// </summary>
        public bool IsSend    { get { return @event.IsSend; } }

        /// <summary>
        /// Returns true if the event represents an incoming message on an
        /// external channel.
        /// </summary>
        public bool IsReceive { get { return @event.IsReceive; } }

        /// <summary>
        /// Returns the number of the external channel on which the
        /// communication was observed.
        /// </summary>
        public int Channel { get { return @event.Channel; } }

        /// <exclude/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override void ToXml(XmlElement parent)
        {
            XmlDocument doc = parent.OwnerDocument;

            XmlElement elem = doc.CreateElement("ExternalEventEvent");
            parent.AppendChild(elem);

            XmlElement elemBody;

            if (@event.IsTau)
                elemBody = doc.CreateElement("Tau");
            else
            {
                if (@event.IsSend)
                    elemBody = doc.CreateElement("Send");
                else
                    elemBody = doc.CreateElement("Receive");

                XmlAttribute chanAttr = doc.CreateAttribute("channel");
                chanAttr.Value = @event.Channel.ToString(CultureInfo.CurrentUICulture);
                elemBody.SetAttributeNode(chanAttr);

                XmlAttribute typeAttr = doc.CreateAttribute("type");
                typeAttr.Value = @event.MessageType.ToString(CultureInfo.CurrentUICulture);
                elemBody.SetAttributeNode(typeAttr);
            }

            elem.AppendChild(elemBody);

            base.AddBaseData(elem);
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

        [EditorBrowsable(EditorBrowsableState.Never)]
        internal TraceEvent(ZingSourceContext context, ZingAttribute contextAttribute,
            string message, params object[] arguments)
            : base(context, contextAttribute)
        {
            this.message = message;
            this.arguments = arguments;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
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
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override string ToString()
        {
            return string.Format(CultureInfo.CurrentUICulture, message, arguments);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
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
    /// This event is generated when an attributed statement is executed.
    /// <seealso cref="State.GetEvents"/>
    /// </summary>
    /// <remarks>
    /// Attribute events are generated when an attributed statement is executed.
    /// </remarks>
    public class AttributeEvent : ZingEvent
    {
        private ZingAttributeBaseAttribute attr;

        /// <exclude/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        internal AttributeEvent(ZingSourceContext context, ZingAttribute contextAttribute,
            ZingAttributeBaseAttribute attr)
            : base(context, contextAttribute)
        {
            this.attr = attr;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        internal AttributeEvent(ZingSourceContext context, ZingAttribute contextAttribute,
            ZingAttributeBaseAttribute attr, int SerialNumber)
            : base(context, contextAttribute, SerialNumber)
        {
            this.attr = attr;
        }

        /// <summary>
        /// Returns the attribute associated with the executed statement.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public Attribute Attribute { get { return attr; } }

        /// <exclude/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override string ToString()
        {
            return string.Format(CultureInfo.CurrentUICulture, "AttributeEvent: {0}", attr);
        }

        /// <exclude/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override void ToXml(XmlElement parent)
        {
            XmlDocument doc = parent.OwnerDocument;

            XmlElement elem = doc.CreateElement("AttributeEvent");
            parent.AppendChild(elem);

            attr.ToXml(elem);

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
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override string ToString()
        {
            return string.Format(CultureInfo.CurrentUICulture, "CreateProcess - process='{0}', child='{1}'", this.ProcName, this.newProcName);
        }

        /// <exclude/>
        [EditorBrowsable(EditorBrowsableState.Never)]
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
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override string ToString()
        {
            return string.Format(CultureInfo.CurrentUICulture, "TerminateProcess - process='{0}'", this.ProcName);
        }

        /// <exclude/>
        [EditorBrowsable(EditorBrowsableState.Never)]
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
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override string ToString()
        {
            return string.Format(CultureInfo.CurrentUICulture, "Send(chan='{0}({1})', data='{2}')", chanType, chanPtr, data);
        }

        /// <exclude/>
        [EditorBrowsable(EditorBrowsableState.Never)]
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
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override string ToString()
        {
            return string.Format(CultureInfo.CurrentUICulture, "Receive(chan='{0}({1}', data='{2}')", chanType, chanPtr, data);
        }

        /// <exclude/>
        [EditorBrowsable(EditorBrowsableState.Never)]
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