using System;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Xml;

namespace Microsoft.Zing
{
    /// <summary>
    /// Base class for all Zing classes
    /// </summary>
    [CLSCompliant(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class ZingClass : HeapElement
	{
		protected ZingClass(StateImpl app) : base(app)
		{
		}
	
        // <summary>Constructor</summary>
        protected ZingClass()
        {
        }

		protected ZingClass(ZingClass clone) : base(clone)
		{
		}

        // If the user class has non-static members, these methods will all be
        // overridden. So we never expect these methods to be called.

        public override object Clone()
        {
            throw new InvalidOperationException("internal error: invalid call to ZingClass.Clone()");
        }

        public override void WriteString(StateImpl state, BinaryWriter bw)
        {
            throw new InvalidOperationException("internal error: invalid call to ZingClass.WriteString()");
        }
		public override void TraverseFields(FieldTraverser ft)
		{
			throw new InvalidOperationException("internal error: invalid call to ZingClass.TraverseFields()");
		}

        public override string ToString()
        {
            // Iterate over each field in the class - get the field value (via
            // late binding) and display its type, name, and value. All public
            // fields are user-defined at this point. We'll need to special-case
            // this if we add implementation-related fields later on.

            // throw new ApplicationException("not implemented");

            
            string s = string.Empty;

            foreach (FieldInfo fi in this.GetType().GetFields())
            {
				if (fi.Name.Length > 5 && fi.Name.Substring(0, 5) == "priv_") 
				{
					object val = this.GetType().InvokeMember(
						fi.Name,
						BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetField,
						null, this, new object [] {}, CultureInfo.CurrentCulture);

					if (fi.FieldType == typeof(Pointer))
						s += string.Format(CultureInfo.CurrentUICulture, "      ZingPointer {0} = {1}\r\n",
							fi.Name.Substring(8), (uint) ((Pointer) val));
					else
						s += string.Format(CultureInfo.CurrentUICulture, "      {0} {1} = {2}\r\n",
							Utils.Externalize(fi.FieldType), fi.Name.Substring(8), val);
				}
            }

            return s;    
        }

        public override void ToXml(XmlElement containerElement)
        {
            XmlDocument doc = containerElement.OwnerDocument;

            XmlElement elem = doc.CreateElement("Object");
            containerElement.AppendChild(elem);

            XmlAttribute attr;

            attr = doc.CreateAttribute("type");
            attr.Value = Utils.Externalize(this.GetType());
            elem.SetAttributeNode(attr);

            Type thisType = this.GetType();

            foreach (FieldInfo fi in thisType.GetFields())
            {
                const string prefix = "priv____";

                if (!fi.Name.StartsWith(prefix))
                    continue;

                object val = thisType.InvokeMember(
                    fi.Name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetField,
                    null, this, new object [] {}, CultureInfo.CurrentCulture);

                string name = fi.Name.Substring(prefix.Length);

                XmlElement memberElem = doc.CreateElement("Field");
                elem.AppendChild(memberElem);

                attr = doc.CreateAttribute("name");
                attr.Value = name;
                memberElem.SetAttributeNode(attr);

                Type memberType = val.GetType();
                if (memberType == typeof(Pointer))
                {
                    attr = doc.CreateAttribute("type");
                    attr.Value = "Microsoft.Zing.Pointer";
                    memberElem.SetAttributeNode(attr);

                    memberElem.InnerText = val.ToString();
                }
                else
                {
                    string typeName = Utils.Externalize(memberType);

                    attr = doc.CreateAttribute("type");
                    attr.Value = typeName;
                    memberElem.SetAttributeNode(attr);

                    // TODO: if this is a struct, we need to let it format its contents

                    string valText = val.ToString();

                    if (memberType.IsEnum)
                        valText = Utils.Unmangle(valText);

                    memberElem.InnerText = valText;
                }
            }
        }
    }

    /// <summary>
    /// Base class for all classes directly defined in ZOM
    /// </summary>
    [CLSCompliant(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class ZOMClass : HeapElement
    {

        protected ZOMClass(StateImpl app)
            : base(app)
        {
        }

        // <summary>Constructor</summary>
        protected ZOMClass()
        {
        }

        protected ZOMClass(ZOMClass clone)
            : base(clone)
        {
        }

        //NOTE: we are allocating TypeIdCounts of 15000 and more for
        // ZOM classes.
        private static short libTypeIdCount = 15000;
        protected static short getNextLibTypeIdCount()
        {
            return (libTypeIdCount++);
        }

        // If the user class has non-static members, these methods will all be
        // overridden. So we never expect these methods to be called.

        public override object Clone()
        {
            throw new InvalidOperationException("internal error: invalid call to ZOMClass.Clone()");
        }

        public override void WriteString(StateImpl state, BinaryWriter bw)
        {
            throw new InvalidOperationException("internal error: invalid call to ZOMClass.WriteString()");
        }
        public override void TraverseFields(FieldTraverser ft)
        {
            throw new InvalidOperationException("internal error: invalid call to ZOMClass.TraverseFields()");
        }
        public override object GetValue(int fieldIndex)
        {
            throw new InvalidOperationException("internal error: invalid call to ZOMClass.GetValue()");
        }
        public override void SetValue(int fieldIndex, object value)
        {
            throw new InvalidOperationException("internal error: invalid call to ZOMClass.GetValue()");
        }
        public override string ToString()
        {
            // Iterate over each field in the class - get the field value (via
            // late binding) and display its type, name, and value. All public
            // fields are user-defined at this point. We'll need to special-case
            // this if we add implementation-related fields later on.

            // throw new ApplicationException("not implemented");


            string s = string.Empty;

            foreach (FieldInfo fi in this.GetType().GetFields())
            {
                   object val = this.GetType().InvokeMember(
                        fi.Name,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetField,
                        null, this, new object[] { }, CultureInfo.CurrentCulture);

                    if (fi.FieldType == typeof(Pointer))
                        s += string.Format(CultureInfo.CurrentUICulture, "      ZingPointer {0} = {1}\r\n",
                            fi.Name.Substring(8), (uint)((Pointer)val));
                    else
                        s += string.Format(CultureInfo.CurrentUICulture, "      {0} {1} = {2}\r\n",
                            Utils.Externalize(fi.FieldType), fi.Name.Substring(8), val);
            }
            return s;
        }

        public override void ToXml(XmlElement containerElement)
        {
            XmlDocument doc = containerElement.OwnerDocument;

            XmlElement elem = doc.CreateElement("Object");
            containerElement.AppendChild(elem);

            XmlAttribute attr;

            attr = doc.CreateAttribute("type");
            attr.Value = Utils.Externalize(this.GetType());
            elem.SetAttributeNode(attr);

            Type thisType = this.GetType();

            foreach (FieldInfo fi in thisType.GetFields())
            {
                object val = thisType.InvokeMember(
                    fi.Name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetField,
                    null, this, new object[] { }, CultureInfo.CurrentCulture);

                XmlElement memberElem = doc.CreateElement("Field");
                elem.AppendChild(memberElem);

                attr = doc.CreateAttribute("name");
                attr.Value = fi.Name;
                memberElem.SetAttributeNode(attr);

                Type memberType = val.GetType();
                if (memberType == typeof(Pointer))
                {
                    attr = doc.CreateAttribute("type");
                    attr.Value = "Microsoft.Zing.Pointer";
                    memberElem.SetAttributeNode(attr);

                    memberElem.InnerText = val.ToString();
                }
                else
                {
                    string typeName = Utils.Externalize(memberType);

                    attr = doc.CreateAttribute("type");
                    attr.Value = typeName;
                    memberElem.SetAttributeNode(attr);

                    // TODO: if this is a struct, we need to let it format its contents

                    string valText = val.ToString();

                    if (memberType.IsEnum)
                        valText = Utils.Unmangle(valText);

                    memberElem.InnerText = valText;
                }
            }
        }
    }

    //
    // For "sizeof", and perhaps other things, it's helpful to have a common base
    // class for sets, channels, and arrays.
    //
    [CLSCompliant(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class ZingCollectionType : HeapElement
    {
		protected ZingCollectionType() { }
		protected ZingCollectionType(StateImpl app) : base(app) { }
		protected ZingCollectionType(ZingCollectionType clone) : base(clone) { }
        public abstract int Count { get; }
        public virtual object[] GetChoices() { throw new NotImplementedException(); }
		public override object GetValue(int fieldIndex) 
		{
			Debug.Assert(false);
			return null;
		}
        public override void SetValue(int fieldIndex, object value)
		{
			Debug.Assert(false);
		}
    }

    [CLSCompliant(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class ZingChan : ZingCollectionType
    {
        protected ZingChan()
        {
            queue = new Queue();
        }

		protected ZingChan(StateImpl app) : base(app)
		{
			queue = new Queue();
		}

		protected ZingChan(ZingChan clone) : base(clone)
		{
			queue = new Queue();
		}

        protected Queue Queue { get { return queue; } }
        private Queue queue;

        public abstract Type MessageType { get; }

        // For reference types, this is fine. For channels of value types, the derived
        // class will need to override this.
		public override void WriteString(StateImpl state, BinaryWriter bw)
		{
			bw.Write(this.TypeId);
			bw.Write(this.Count);
			foreach (Pointer ptr in this.queue)
				bw.Write(state.GetCanonicalId((uint) ptr));
		}
		public override void TraverseFields(FieldTraverser ft)
		{
			ft.DoTraversal(this.TypeId);
			ft.DoTraversal(this.Count);
			foreach (Pointer ptr in this.queue)
				ft.DoTraversal(ptr);
		}

        public override string ToString()
        {
            string s = string.Format(CultureInfo.CurrentUICulture, "      Chan: MsgType={0}, {1} elements\r\n", Utils.Unmangle(MessageType), Count);

            int n=0;
            foreach (object obj in queue)
            {
                if (MessageType == typeof(Pointer))
                    s += string.Format(CultureInfo.CurrentUICulture, "        {0}: {1}\r\n", n++, (uint) ((Pointer)obj));
                else
                    s += string.Format(CultureInfo.CurrentUICulture, "        {0}: {1}\r\n", n++, obj);
            }
            return s;
        }

        public override void ToXml(XmlElement containerElement)
        {
            XmlDocument doc = containerElement.OwnerDocument;

            XmlElement elem = doc.CreateElement("Channel");
            containerElement.AppendChild(elem);

            XmlAttribute attr;

            attr = doc.CreateAttribute("type");
            attr.Value = Utils.Externalize(this.GetType());
            elem.SetAttributeNode(attr);

            attr = doc.CreateAttribute("messageType");
            attr.Value = Utils.Externalize(this.MessageType);
            elem.SetAttributeNode(attr);

            foreach (object obj in queue)
            {
                XmlElement msgElem = doc.CreateElement("Message");
                if (MessageType == typeof(Pointer))
                    msgElem.InnerText = ((uint) ((Pointer) obj)).ToString(CultureInfo.CurrentUICulture);
                else
                    msgElem.InnerText = obj.ToString();

                elem.AppendChild(msgElem);
            }
        }

        public void Send(StateImpl stateImpl, object obj, ZingSourceContext context,
            ZingAttribute contextAttribute)
        {
			SetDirty();
            if (Options.EnableEvents)
            {
                if (Options.DegreeOfParallelism == 1)
                {
                    stateImpl.ReportEvent(new SendEvent(context, contextAttribute, this, obj));
                }
                else
                {
                    stateImpl.ReportEvent(new SendEvent(context, contextAttribute, this, obj, stateImpl.MySerialNum));
                }
            }

            queue.Enqueue(obj);
        }

        public object Receive(StateImpl stateImpl, ZingSourceContext context,
            ZingAttribute contextAttribute)
        {
			SetDirty();
			
			object obj;
            obj = queue.Dequeue();

            if (Options.EnableEvents)
            {
                if (Options.DegreeOfParallelism == 1)
                {
                    stateImpl.ReportEvent(new ReceiveEvent(context, contextAttribute, this, obj));
                }
                else
                {
                    stateImpl.ReportEvent(new ReceiveEvent(context, contextAttribute, this, obj, stateImpl.MySerialNum));
                }
            }

            return obj;
        }

        public bool CanReceive
        {
            get
            {		
				return queue.Count > 0;
            }
        }

        public override int Count
        {
            get
            {
				return queue.Count;
            }
        }
    }

    /// <summary>
    /// Base class for all Zing set types.
    /// </summary>
    [CLSCompliant(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class ZingSet : ZingCollectionType
    {
        protected ZingSet()
        {
            arraylist = new ArrayList();
        }

		protected ZingSet(StateImpl app) : base(app)
		{
			arraylist = new ArrayList();
		}
		protected ZingSet(ZingSet clone) : base(clone)
		{
			arraylist = new ArrayList();
		}

        protected ArrayList ArrayList { get { return arraylist; } }
        private ArrayList arraylist;

        public abstract Type ElementType { get; }

        // For reference types, this is fine. For sets of value types, the derived
        // class will need to override this.
        public override void WriteString(StateImpl state, BinaryWriter bw)
        {
            bw.Write(this.TypeId);
            bw.Write(this.Count);
            foreach (Pointer ptr in this.arraylist)
                bw.Write(state.GetCanonicalId((uint) ptr));
        }
		public override void TraverseFields(FieldTraverser ft)
		{
			ft.DoTraversal(this.TypeId);
			ft.DoTraversal(this.Count);
			foreach (Pointer ptr in this.arraylist)
				ft.DoTraversal(ptr);
		}

        public override string ToString()
        {
            string s = string.Format(CultureInfo.CurrentUICulture, "      Set: ElementType={0}, {1} elements\r\n", Utils.Unmangle(ElementType), Count);

            int n=0;
            foreach (object obj in arraylist)
            {
                if (ElementType == typeof(Pointer))
                    s += string.Format(CultureInfo.CurrentUICulture, "        {0}: {1}\r\n", n++, (uint) ((Pointer)obj));
                else
                    s += string.Format(CultureInfo.CurrentUICulture, "        {0}: {1}\r\n", n++, obj);
            }
            return s;
        }

        public override void ToXml(XmlElement containerElement)
        {
            XmlDocument doc = containerElement.OwnerDocument;

            XmlElement elem = doc.CreateElement("Set");
            containerElement.AppendChild(elem);

            XmlAttribute attr;

            attr = doc.CreateAttribute("type");
            attr.Value = Utils.Externalize(this.GetType());
            elem.SetAttributeNode(attr);

            attr = doc.CreateAttribute("memberType");
            attr.Value = Utils.Externalize(this.ElementType);
            elem.SetAttributeNode(attr);

            foreach (object obj in arraylist)
            {
                XmlElement memberElem = doc.CreateElement("Member");
                if (this.ElementType == typeof(Pointer))
                    memberElem.InnerText = ((uint) ((Pointer) obj)).ToString(CultureInfo.CurrentUICulture);
                else
                    memberElem.InnerText = obj.ToString();

                elem.AppendChild(memberElem);
            }
        }

        public void Add(object obj)
        {
            if (!arraylist.Contains(obj))
            {
                SetDirty();
                arraylist.Add(obj);
                arraylist.Sort();
            }
        }

        public void Remove(object obj)
        {
            if (arraylist.Contains(obj))
            {
                SetDirty();
                arraylist.Remove(obj);
                arraylist.Sort();
            }
        }

        public void AddSet(ZingSet set)
        {
            bool setDirtyDone = false;
            foreach (object obj in set.arraylist)
            {
                if (!arraylist.Contains(obj))
                {
                    if (!setDirtyDone)
                    {
                        SetDirty();
                        setDirtyDone = true;
                    }
                    arraylist.Add(obj);
                    arraylist.Sort();
                }
            }
        }

        public void RemoveSet(ZingSet set)
        {
            bool setDirtyDone = false;
			foreach (object obj in set.arraylist)
            {
                if (arraylist.Contains(obj))
                {
                    if (!setDirtyDone)
                    {
                        SetDirty();
                        setDirtyDone = true;
                    }
                    arraylist.Remove(obj);
                    arraylist.Sort();
                }
            }
        }

        public object GetItem(int index)
        {			
			return arraylist[index];
        }

        public bool IsMember(object obj)
        {	
			return arraylist.Contains(obj);
        }

        public override object[] GetChoices()
        {	
			return arraylist.ToArray();
        }

        public override int Count
        {
            get
            {		
				return arraylist.Count;
            }
        }
    }

    /// <summary>
    /// Base class for all Zing array types.
    /// </summary>
    [CLSCompliant(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class ZingArray : ZingCollectionType
    {
        protected ZingArray() {}
		protected ZingArray(StateImpl app) : base(app) {}
		protected ZingArray(ZingArray clone) : base(clone) {}

        protected abstract Array Array { get; }
        public abstract Type ElementType { get; }

        // For reference types, this is fine. For arrays of value types, the derived
        // class will need to override this.
        public override void WriteString(StateImpl state, BinaryWriter bw)
        {
            bw.Write(this.TypeId);
            bw.Write(this.Count);
            foreach (Pointer ptr in this.Array)
                bw.Write(state.GetCanonicalId((uint) ptr));
        }
		public override void TraverseFields(FieldTraverser ft)
		{
			ft.DoTraversal(this.TypeId);
			ft.DoTraversal(this.Count);
			foreach (Pointer ptr in this.Array)
				ft.DoTraversal(ptr);
		}

		public override string ToString()
        {
            string s = string.Format(CultureInfo.CurrentUICulture, "      Array: ElementType={0}, {1} elements\r\n", Utils.Unmangle(ElementType), Array.Length);

            int n=0;
            foreach (object obj in Array)
            {
                if (ElementType == typeof(Pointer))
                    s += string.Format(CultureInfo.CurrentUICulture, "        {0}: {1}\r\n", n++, (uint) ((Pointer)obj));
                else
                    s += string.Format(CultureInfo.CurrentUICulture, "        {0}: {1}\r\n", n++, obj);
            }
            return s;
        }

        public override void ToXml(XmlElement containerElement)
        {
            XmlDocument doc = containerElement.OwnerDocument;

            XmlElement elem = doc.CreateElement("Array");
            containerElement.AppendChild(elem);

            XmlAttribute attr;

            attr = doc.CreateAttribute("type");
            attr.Value = Utils.Externalize(this.GetType());
            elem.SetAttributeNode(attr);

            attr = doc.CreateAttribute("elementType");
            attr.Value = Utils.Externalize(this.ElementType);
            elem.SetAttributeNode(attr);

            foreach (object obj in Array)
            {
                XmlElement memberElem = doc.CreateElement("Element");
                if (this.ElementType == typeof(Pointer))
                    memberElem.InnerText = ((uint) ((Pointer) obj)).ToString(CultureInfo.CurrentUICulture);
                else
                    memberElem.InnerText = obj.ToString();

                elem.AppendChild(memberElem);
            }
        }

        public override object[] GetChoices()
        {
            object[] choices = new object[Count];
            Array.CopyTo(choices, 0);
            return choices;
        }

        public override int Count
        {
            get
            {
                return Array.Length;
            }
        }
    }

    /// <summary>
    /// This attribute is used to mark "method" classes (which derive from ZingMethod)
    /// as being "activated". A process is created for each method so marked.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    sealed public class ActivateAttribute : Attribute
    {
        public ActivateAttribute()
        {
        }
    }

    [CLSCompliant(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class UndoableStorage
    {
        private bool dirty;
        private UndoableStorage savedCopy;

		#region The constructor
		protected UndoableStorage() 
		{
		}
		#endregion

		#region Operations on the dirty bit
        public bool IsDirty { 
            get { return dirty; } 
        }

        public void SetDirty() 
        {
            if (!dirty) {
                savedCopy = (UndoableStorage) this.MakeInstance();
				savedCopy.CopyContents(this);
                dirty = true;
            }
        }
		#endregion

		#region Methods for copying/cloning
        public abstract UndoableStorage MakeInstance();
        public abstract void CopyContents(UndoableStorage source);
        //public abstract UndoableStorage Clone();
		#endregion

		#region Methods for reading and writing fields to avoid reflection
		public abstract object GetValue(int fieldIndex);
		public abstract void SetValue(int fieldIndex, object value);
		#endregion

        #region Data-structure for undo logs
        private class UndoLogEntry 
        {
            internal readonly UndoableStorage SavedStorage;
            internal UndoLogEntry(UndoableStorage saved) 
            { 
                SavedStorage = saved; 
            }
        }
        #endregion

        #region State Delta operations
        // state delta -- globals is undoable, so this function
        // clears the dirty bits, returns undo log
        public object DoCheckIn()
        {
			UndoLogEntry res = null;

			if (dirty && savedCopy != null) 
			{
				res = new UndoLogEntry(savedCopy);
			}          

			dirty = false;
			savedCopy = null;
            return res;
        }

        // does nothing if clean; otherwise we revert to the savedCopy
        public void DoRevert()
        {
			if (dirty && savedCopy != null) 
			{
				CopyContents(savedCopy);
			}

			dirty = false;
			savedCopy = null;
        }

        // revert to an earlier state
        public void DoRollback(object[] uleList)
        {
            // Debug.Assert(!dirty);
            // Debug.Assert(savedCopy == null);

			UndoLogEntry latest = null;

            // a small optimization here
            for (int i = 0, n = uleList.Length; i < n; i++) 
			{
                if (uleList[i] == null)
                    continue;
                
                latest = (UndoLogEntry) uleList[i];
            }

			if (latest != null)
			{
				CopyContents(latest.SavedStorage);
			}
			else if (dirty && savedCopy != null) 
			{
				CopyContents(savedCopy);
			}

			dirty = false;
			savedCopy = null;
        }
        #endregion
    }

    [CLSCompliant(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class ZingGlobals : UndoableStorage
    {
        private StateImpl app;
        protected StateImpl Application { get { return app; } }

        protected ZingGlobals(StateImpl application) : base()
        {
            this.app = application;
        }

		// used in the factory method MakeInstance()
        protected ZingGlobals() : base() {}

        public ZingGlobals Clone(StateImpl application)
        {
            ZingGlobals clone = (ZingGlobals) MakeInstance();
            clone.app = application;
            
            clone.CopyContents(this);
            return clone;
        }

        public abstract void WriteString(StateImpl state, BinaryWriter bw);

        public abstract void TraverseFields(FieldTraverser ft);

        public ZingPointerSet UnallocatedWrites
        {
            get { return unallocatedWrites; }
        }
        private ZingPointerSet unallocatedWrites;

        public void AddPointerToUnallocatedWrites(Pointer ptr)
        {   
            if (unallocatedWrites == null)
                unallocatedWrites = new ZingPointerSet(app);
            unallocatedWrites.Add(ptr);
        }
     }

    public class ZingPointerSet : ZingSet
    {
        protected override short TypeId { get { return ZingPointerSet.typeId; } }
        private const short typeId = 1;
        public ZingPointerSet()
            : base()
        {
        }
        public ZingPointerSet(StateImpl app)
            : base(app)
        {
        }
        public ZingPointerSet(ZingPointerSet obj)
            : base(obj)
        {
        }
        public override Type ElementType
        {
            get { return typeof(Pointer); }
        }
        public override object Clone()
        {
            ZingPointerSet newObj = new ZingPointerSet(this);

            foreach (Pointer ptr in this.ArrayList)
                newObj.ArrayList.Add(ptr);

            return newObj;
        }
    }
}
