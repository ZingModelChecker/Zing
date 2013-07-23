using System;
using System.IO;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Zing
{
    /// <exclude/>
    /// <summary>
    /// Base class for all Zing methods. Represents an invocation of the method.
    /// </summary>
    /// <remarks>
    /// Subclasses of ZingMethod are always nested within a subclass of ZingClass.
    /// </remarks>
    [CLSCompliant(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class ZingMethod
    {
        // this property should be overridden in the generated code for instance methods
        public virtual Pointer This
        {
            get { return new Pointer(0); }
            set { }
        }

        public abstract string ProgramCounter { get; }
        public string MethodName 
        {
            get
            {
                string name = this.GetType().ToString();
                return name.Replace("Microsoft.Zing.Application+", "").Replace('+', '.');
            }
        }
        // <summary>Constructor</summary>
        protected ZingMethod()
        {
        }

        public abstract ushort NextBlock { get; set; }

        public abstract UndoableStorage Locals { get; }
        public abstract UndoableStorage Inputs { get; }
        public abstract UndoableStorage Outputs { get; }

        public abstract int CompareTo(object obj);

        public abstract void WriteString(StateImpl state, BinaryWriter bw);

        public abstract void WriteOutputsString(StateImpl state, BinaryWriter bw);

        public abstract void Dispatch(Process process);

        public virtual ulong GetRunnableJoinStatements(Process process) { return ulong.MaxValue; }

        public virtual bool IsAtomicEntryBlock() { return false; }

        // This field is set when the select statement actually begins execution and is
        // examined in the join statement "tester" blocks. This just avoids having to
        // calculate the runnable flags multiple times.
        private ulong savedRunnableJoinStatements;
        protected ulong SavedRunnableJoinStatements
        {
            get { return savedRunnableJoinStatements; }
            set { savedRunnableJoinStatements = value; }
        }

        public bool IsRunnable(Process process) { return GetRunnableJoinStatements(process) != 0; }

        public virtual bool ValidEndState { get { return true; } }

        public virtual ZingSourceContext Context { get { return null; } }
        public virtual ZingAttribute ContextAttribute { get { return null; } }

        public ZingMethod Caller { 
            get { return caller; } 
            set { caller = value; } 
        }
        private ZingMethod caller;

        public int CurrentException { 
            get { return currentException; } 
            set { currentException = value; } 
        }
        private int currentException;

        private int savedAtomicityLevel;
        public int SavedAtomicityLevel
        {
            get { return savedAtomicityLevel; }
            set { savedAtomicityLevel = value; }
        }

        // This is overridden by methods that return bool so that they can be used as
        // predicates in wait-style join conditions.
        public virtual bool BooleanReturnValue
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        // Returns true if the exception can be handled in this stack frame
        [SuppressMessage("Microsoft.Design", "CA1030:UseEventsWhereAppropriate")]
        public abstract bool RaiseZingException(int exception);

        public abstract StateImpl StateImpl { get; set; }

        // all these are actually static members of each derived method
        // thus, they are abstract in tbe base class and
        // are overridden using static members in TemplateParts.cs
        public abstract Hashtable TransientStates { get; }
        public abstract SortedList SummaryTable { get; }
        public abstract Hashtable SummaryHashtable { get; }
        public abstract ZingMethod Clone(StateImpl myState, Process myProcess, bool shallowCopy);
        public abstract int ExecutionCount{ get; set;}

        #region Hints for each method
        public abstract Hints Hints { get; }
        #endregion

       

        public bool  IsTransient(StateImpl state) 
        {
            Fingerprint fp = state.Fingerprint;
            return (TransientStates.ContainsKey(fp));
        }        

       

        public void RemoveTransient(Fingerprint fp)
        {
            TransientStates.Remove(fp);
        }
 

        public object DoCheckIn()
        {
            //Console.WriteLine("checking in stack frame {0}", stackFrameId);
            object othersULE = DoCheckInOthers();

            if (othersULE != null || Locals.IsDirty || Outputs.IsDirty || Inputs.IsDirty) {
                // something has changed
                ZingMethodULE ule = new ZingMethodULE();
                
                ule.localsULE = Locals.DoCheckIn();
                ule.inputsULE = Inputs.DoCheckIn();
                ule.outputsULE = Outputs.DoCheckIn();
                ule.othersULE = othersULE;

                return ule;
            }
            return null;
        }

        internal void DoRevert()
        {
            Locals.DoRevert();
            Inputs.DoRevert();
            Outputs.DoRevert();
            DoRevertOthers();
        }

        internal void DoRollback(object[] uleList)
        {
            int n = uleList.Length;
            object[] lc_ules = new object[n];
            object[] in_ules = new object[n];
            object[] out_ules = new object[n];
            object[] oth_ules = new object[n];
            int i;

            for (i = 0; i < n; i++) {
                if (uleList[i] == null) {
                    lc_ules[i] = null;
                    in_ules[i] = null;
                    out_ules[i] = null;
                    oth_ules[i] = null;
                } else {
                    ZingMethodULE ule = (ZingMethodULE) uleList[i];
                    lc_ules[i] = ule.localsULE;
                    in_ules[i] = ule.inputsULE;
                    out_ules[i] = ule.outputsULE;
                    oth_ules[i] = ule.othersULE;
                }
            }
            Locals.DoRollback(lc_ules);
            Inputs.DoRollback(in_ules);
            Outputs.DoRollback(out_ules);
            DoRollbackOthers(oth_ules);
        }

        private class ZingMethodULE
        {
            public object localsULE;
            public object inputsULE;
            public object outputsULE;
            public object othersULE;
                
            internal ZingMethodULE() 
            {
            }
        }

        public abstract object DoCheckInOthers();
        public abstract void DoRevertOthers();
        public abstract void DoRollbackOthers(object[] uleList);

        // Note: The emitted code generates a derived class for each method
        // defined on a Zing class. The generated classes will contain nested
        // classes named "Locals", "Inputs", and "Outputs". The generated code
        // will refer to these classes and their members in a strongly-typed
        // way.

        public bool ContainsVariable(string name)
        {
            return Utils.ContainsVariable(this, "locals", name) ||
                   Utils.ContainsVariable(this, "inputs", name) ||
                   Utils.ContainsVariable(this, "outputs", name);
        }

        public object LookupValueByName(string name)
        {
            if (Utils.ContainsVariable(this, "locals", name))
                return Utils.LookupValueByName(this, "locals", name);
            else if (Utils.ContainsVariable(this, "inputs", name))
                return Utils.LookupValueByName(this, "inputs", name);
            else
                return Utils.LookupValueByName(this, "outputs", name);
        }

        abstract public void TraverseFields(FieldTraverser ft);
    }
}
