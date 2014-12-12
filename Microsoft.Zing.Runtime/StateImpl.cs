//#define DEBUG_STATEIMPL

// Using this define defaults to fingerprinting to the old, nonincremental version
#define NONINC_FINGERPRINTS

// Turn these defines to debug incremental fingerprinting
//#define DEBUG_INC_FINGERPRINTS
//#define COMPARE_INC_NONINC_FINGERPRINTS
//#define PRINT_FINGERPRINTS

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security;
using System.Security.Permissions;
using System.Text;
using System.Xml;
using System.Linq;

namespace Microsoft.Zing
{
    /// <exclude/>
    /// <summary>
    /// Zing program base class - represents a complete state of the program
    /// </summary>
    /// <remarks>
    /// The StateImpl class describes the basic characteristics of a Zing state. At this level,
    /// we're providing a normalized view of all possible Zing "programs". This class is
    /// suitable for use by state explorers (model-checkers), simulators, debuggers, and
    /// so on.
    /// <newpar>
    /// The "program" emitted by the Zing compiler is basically a class derived from
    /// "StateImpl". It extends "StateImpl" by adding strongly typed nested classes for the various
    /// program elements in Zing (globals, locals, classes, methods, etc.)
    /// </newpar>
    /// <para>
    /// Once a state is "created" (by calling InitialState(), Execute(), or
    /// ExecuteWithChoice()) it is immutable. One may query the state or generate new states
    /// from it, but it cannot be altered.
    /// </para>
    /// </remarks>
    [SuppressMessage("Microsoft.Naming", "CA1711:IdentifiersShouldNotHaveIncorrectSuffix")]
    [CLSCompliant(false)]
    
    public abstract class StateImpl : ICloneable
    {
        #region Constructors and factory methods
        // This constructor is used for cloning.
        protected StateImpl()
        {

        }

        public abstract StateImpl MakeSkeleton();

        protected StateImpl (bool initialState)
        {
            Debug.Assert(initialState);

            this.processes = new ArrayList(6);  // probably a good default size
            this.savedNumProcesses = -1;
            this.nextProcessId = 0;
            
            if(ZingerConfiguration.DoDelayBounding)
            {
                ZingDBSchedState = ZingerConfiguration.ZExternalScheduler.zSchedState;
                ZingDBScheduler = ZingerConfiguration.ZExternalScheduler.zDelaySched;
            }

            foreach (Type nestedClassType in this.GetType().GetNestedTypes(BindingFlags.NonPublic))
            {
                if (nestedClassType.BaseType == typeof(ZingClass))
                {
                    // Found a Zing class - check for "activated" methods
                    foreach (Type methodClassType in nestedClassType.GetNestedTypes(BindingFlags.NonPublic))
                    {
                        if (methodClassType.BaseType == typeof(ZingMethod))
                        {
                            object[] activationAttr = methodClassType.GetCustomAttributes(
                                typeof(Microsoft.Zing.ActivateAttribute), false);

                            if (activationAttr.Length > 0)
                            {
                                this.CreateProcess(this,
                                    (ZingMethod)System.Activator.CreateInstance(methodClassType, new object[] { this }),
                                    methodClassType.Name, new ZingSourceContext(), null);
                            }
                        }
                    }
                }
            }

            this.stepNumber = 1;
        }
        #endregion

        #region AcceptingCycles
        /// <summary>
        /// This bit is set when the accepting transition is executed
        /// </summary>
        private Boolean isAcceptingState = false;

        public Boolean IsAcceptingState
        {
            get { return isAcceptingState; }
            set { isAcceptingState = value; }
        }

        #endregion

        #region Status of the state
        //IExplorable
        public StateType Type 
        { 
            get 
            { 
                if (Exception != null) 
                {
                    if (Exception is ZingAssumeFailureException)
                        return StateType.FailedAssumption;
                    else
                        return StateType.Error;
                }

                // TODO: keep track of pending choices at
                // SetPendingChoice
                if (NumChoices > 0) 
                    return StateType.Choice;

                for (int i = 0; i < processes.Count; i++)
                    if (GetProcessStatus(i) == ProcessStatus.Runnable)
                        return StateType.Execution;

                return StateType.NormalTermination;
            } 
        }
        //IExplorable
        public bool IsChoicePending 
        {
            get { return NumChoices > 0; }
        }
        //IExplorable
        public bool IsNormalState 
        {
            get { return Type == StateType.Execution; }
        }
        //IExplorable
        public bool IsTerminalState 
        { 
            get 
            {
                //
                // Asking for our StateType is expensive, so try to answer this
                // query in cheaper ways first.
                //
                if (Exception != null)
                    return true;

                return (Type == StateType.NormalTermination);
            } 
        }
        //IExplorable
        public bool IsErroneous 
        {
            get { return Exception != null && !(Exception is ZingAssumeFailureException); }
        }
        //IExplorable
        public bool IsValidTermination 
        {
            get { return (Type == StateType.NormalTermination); }
        }
        //IExplorable
        public bool IsFailedAssumption 
        {
            get { return Exception != null && Exception is ZingAssumeFailureException; }
        }
        //IExplorable
        public ZingerResult ErrorCode
        {
            get
            {
                if (!this.IsErroneous)
                    return ZingerResult.Success;

                Exception e = this.Exception;

                if (e is ZingAssertionFailureException)
                    return ZingerResult.Assertion;
                else if (e is ZingInvalidEndStateException)
                    return ZingerResult.Deadlock;
                else if (e is ZingUnexpectedFailureException)
                    return ZingerResult.ZingRuntimeError;
                else if (e is ZingerDFSStackOverFlow)
                    return ZingerResult.DFSStackOverFlowError;
                else
                    return ZingerResult.ModelRuntimeError;
            }
        }
        #endregion

        #region State delta related private variables and methods
        
        private ArrayList dirtyHeapPointers;
        internal ArrayList garbageHeapEntries;
        
        internal void AddToDirtyHeapPointers(Pointer p)
        {
            if (dirtyHeapPointers == null) 
                dirtyHeapPointers = new ArrayList();
            dirtyHeapPointers.Add(p);
        }

        internal void AddToGarbageHeapEntries(HeapEntry he)
        {
            if(garbageHeapEntries == null)
                garbageHeapEntries = new ArrayList();
            garbageHeapEntries.Add(he);
        }

        // State Undo Log Entry
        private class StateULE 
        {
            // cloned components
            internal object[] choiceList;
            internal int choiceProcessNumber;
            internal uint nextProcessId;
            internal ushort stepNumber;
            internal uint lastHeapId;
            internal Exception exception;
            //internal Fingerprint fingerprint;
            
            // undoable components
            
            // - global variables
            internal object globalULE;

            // - dirty heap objects
            internal ArrayList dirtyHeapPointers;
            internal ArrayList garbageHeapEntries;

            // - processes and stacks
            internal int numProcesses;
            internal object[] processULEs;
        }


        private Int64 nonce;
        internal Int64 Nonce { get { return nonce; } }

        private Stack historyStack;

        private class HistoryEntry
        {
            internal object stateULE;
            internal Int64 receiptId;
        }

        // internal delta management functions -- called by the delta
        // manager when the state is checked in, checked out, reverted
        // or rolled back
        internal object DoCheckIn()
        {
            // this is for the heap...
#if DEBUG_STATEIMPL
            Fingerprint fp = this.Fingerprint;
            Console.WriteLine("debug: fp@checkin = {0}", fp.ToString());
#endif

            StateULE ule = new StateULE();
            
            // cloned components
            ule.choiceList = choiceList; 
            ule.choiceProcessNumber = choiceProcessNumber; 
            ule.nextProcessId = nextProcessId;
            ule.stepNumber = stepNumber;
            ule.lastHeapId = lastHeapId;
            ule.exception = exception;
            
            //ule.fingerprint = this.fingerprint;
            // hybrid components
            int n = processes.Count;
            ule.numProcesses = savedNumProcesses = n;
            ule.processULEs = new object[n];
            for (int i = 0; i < n; i++)
            {
                if(processes[i] != null)
                    ule.processULEs[i] = ((Process)processes[i]).DoCheckIn();
                else
                    ule.processULEs[i] = null;
            }

            // undoable components

            // globals
            ule.globalULE = Globals.DoCheckIn();

            // heap
            ule.dirtyHeapPointers = null;
            ule.garbageHeapEntries = null;
            if (heap != null)
            {
                if(dirtyHeapPointers != null)
                {
                    ule.dirtyHeapPointers = new ArrayList(dirtyHeapPointers.Count);
                    // iterate only over the heap elements in dirtyHeapPointers
                    foreach (Pointer o in dirtyHeapPointers)
                    {
                        HeapEntry h = (HeapEntry) heap[o];
                        if(h == null)
                        {
                            // this dirty heap pointer has become garbage
                            continue;
                        }
                        ule.dirtyHeapPointers.Add(o);
                        h.DoCheckIn();
                    }
                    dirtyHeapPointers = null;
                }
                
                if(garbageHeapEntries != null)
                {
                    foreach(HeapEntry he in garbageHeapEntries)
                    {
                        he.DoCheckIn();
                    }
                    ule.garbageHeapEntries = garbageHeapEntries;
                    garbageHeapEntries = null;
                }
            }
            Debug.Assert(dirtyHeapPointers == null);
            Debug.Assert(garbageHeapEntries == null);
            // invalidateState();

            return ule;
        }
        
        internal void DoCheckout(object currULE)
        {
            int i;

            StateULE ule = (StateULE) currULE;
            
            // cloned components
            fingerprint = null;
            choiceList = ule.choiceList;
            choiceProcessNumber = ule.choiceProcessNumber;
            nextProcessId = ule.nextProcessId;
            stepNumber = ule.stepNumber;
            events = null;
            lastHeapId = ule.lastHeapId;
            exception = ule.exception;
            
            // nothing to be done for undoable heap

            // hybrid components
            Debug.Assert(processes.Count >= ule.numProcesses);
            if (processes.Count > ule.numProcesses)
                processes.RemoveRange(ule.numProcesses,
                    processes.Count - ule.numProcesses);

            for (i = 0; i < ule.numProcesses; i++)
            {
                if(processes[i] != null)
                {
                    ((Process)processes[i]).DoCheckout(ule.processULEs[i]);
                }
            }
            // undoable components do not need to be checked out

#if DEBUG_STATEIMPL
            Fingerprint fp = Fingerprint;
            Console.WriteLine("debug: fp@checkout = {0}", fp.ToString());
#endif
        }

        internal void DoRevert()
        {
            Debug.Assert(savedNumProcesses >= 0);

#if DEBUG_STATEIMPL
            Fingerprint fp = Fingerprint;
            Console.WriteLine("debug: fp@revert = {0}", fp.ToString());
#endif
            // first, do the heap
            if (heap != null)
            {
                if(dirtyHeapPointers != null) 
                {
                    foreach (Pointer o in dirtyHeapPointers)
                    {
                        HeapEntry h = (HeapEntry) heap[o];
                        if (h.DoRevert(nonce))
                        {   // this heap element did not exist at this rollback point 
                            // and therefore needs to be removed from heap.
                            //heap.Remove(o);
                            heap[o] = null;
                        }
                    }
                    dirtyHeapPointers = null;
                }
                if(garbageHeapEntries != null)
                {
                    foreach(HeapEntry he in garbageHeapEntries)
                    {
                        Debug.Assert(he.HeapObj != null);
                        Pointer ptr = he.HeapObj.Pointer;
                        Debug.Assert(heap[ptr] == null);
                        heap[ptr] = he;
                        bool ret = he.DoRevert(nonce);
                        Debug.Assert(!ret);
                    }
                }
            }

            // cloned components do not need to be reverted

            // hybrid components -- processes
            if (processes.Count > savedNumProcesses)
                processes.RemoveRange(savedNumProcesses, 
                    processes.Count - savedNumProcesses);

            for (int i = 0; i < savedNumProcesses; i++)
            {
                if(processes[i] != null)
                    ((Process)processes[i]).DoRevert();
            }
            // undoable components 
            
            // globals
            Globals.DoRevert();

            // invalidateState();
        }

        private void doRollbackOnPointerArray(ArrayList ptrArray, Int64 version) 
        {
            if (ptrArray != null)
            {
                foreach (Pointer o in ptrArray)
                {
                    HeapEntry h = (HeapEntry) heap[o];
                    if (h == null) 
                        continue;
                    if (h.DoRollback(version))
                    {   // this heap element did not exist at this rollback point 
                        // and therefore needs to be removed from heap.
                        //heap.Remove(o);
                        heap[o] = null;
                    }
                }
            }
        }

        private void doRollbackOnGarbage(ArrayList heArray, Int64 version)
        {
            if(heArray == null) return;
            
            foreach(HeapEntry he in heArray)
            {
                bool ret = he.DoRollback(version);
                if(!ret)
                {
                    Debug.Assert(he.HeapObj != null);
                    heap[he.HeapObj.Pointer] = he;
                }
                /*
                if(ret)
                {
                    System.Console.WriteLine();
                    Debug.Assert(!ret);
                }
                */
            }
        }

        internal void DoRollback(object[] ules, Int64 version)
        {
            // first, do the heap
            if (heap != null) 
            {
                doRollbackOnPointerArray(dirtyHeapPointers, version);
                doRollbackOnGarbage(garbageHeapEntries, version);
                for (int i = 0; i < ules.Length; i++)
                {
                    StateULE ule = (StateULE) ules[i];
                    doRollbackOnPointerArray(ule.dirtyHeapPointers, version);
                    doRollbackOnGarbage(ule.garbageHeapEntries, version);
                }
            }
            dirtyHeapPointers = null;
            garbageHeapEntries = null;

            int n = ules.Length;
            if (n == 0)
                return;

#if DEBUG_STATEIMPL
            Console.WriteLine("debug: rolling back...");
#endif

            // cloned components do not need to be rolled back

            // hybrid components

            // - processes

            //   1) trim num-processes down to the target level
            int targetNumProcs = ((StateULE) ules[n-1]).numProcesses;

            Debug.Assert(processes.Count >= targetNumProcs);
            if (processes.Count > targetNumProcs)
                processes.RemoveRange(targetNumProcs,
                    processes.Count - targetNumProcs);

            //   2) roll back these processes
            object[] processULEs = new object[n];

            for (int i = 0; i < targetNumProcs; i++) 
            {
                // rollback process "i" here
                for (int j = 0; j < n; j++)
                    processULEs[j] = ((StateULE) ules[j]).processULEs[i];
                
                if(processes[i] != null)
                    ((Process) processes[i]).DoRollback(processULEs);
            }
            
            //   3) rollback savedNumProcs
            savedNumProcesses = targetNumProcs;
            
            // undoable components

            object[] globalULEs = new object[n];

            for (int i = 0; i < n; i++)
                globalULEs[i] = ((StateULE) ules[i]).globalULE;

            // globals
            Globals.DoRollback(globalULEs);
        }
        #endregion

        /// <summary>
        /// Fields for storing the delaying scheduler information
        /// </summary>
        public ZingerDelayingScheduler ZingDBScheduler = null;
        public ZingerSchedulerState ZingDBSchedState = null;

        //IExplorable
        public virtual string[] GetSources() { return null; }
        //IExplorable
        public virtual string[] GetSourceFiles() { return null; }

       

        //IExplorable
        public Process GetProcess(int processNumber)
        {
            return ((Process)(processes[processNumber]));
        }

        private bool isCall;
        //IExplorable
        public bool IsCall
        {
            get { return isCall; }
            set { isCall = value; }
        }

        private bool isReturn;
        //IExplorable
        public bool IsReturn
        {
            get { return isReturn; }
            set { isReturn = value; }
        }

        private bool isEndAtomicBlock;
        //IExplorable
        public bool IsEndAtomicBlock
        {
            get { return isEndAtomicBlock; }
            set { isEndAtomicBlock = value; }
        }

        public bool IsInvalidEndState()
        {
            bool foundInvalidEndState = false;


            for (int i = 0; (i < processes.Count); i++)
            {
                Process p = (Process)processes[i];
                if (p == null) continue;
                switch (p.CurrentStatus)
                {
                    case ProcessStatus.Runnable:
                        // if anyone is runnable, return false
                        return (false);
                    case ProcessStatus.Blocked:
                        foundInvalidEndState = true;
                        break;
                    default:
                        break;
                }
            }
            if (foundInvalidEndState)
                exception = new ZingInvalidEndStateException();
            return (foundInvalidEndState);
        }

        //IExplorable
        public ZingMethod GetTopOfStack(int processId)
        {
            Process cp = (Process)processes[processId];
            return (cp.TopOfStack);
        }

        #region Process management
        // <summary>Collection class holding our process list</summary>
        // <remarks>
        // Indexed by process id.
        // </remarks>
        private ArrayList processes;
        private int savedNumProcesses;

        // <summary>
        // The next process id to be used. Ids are never reused.
        // </summary>
        internal uint nextProcessId;

        // <summary>
        // Tracks logical time.
        // </summary>
        internal ushort stepNumber;

        // <summary>
        // Create a new process
        // </summary>
        // <param name="entryPoint"></param>
        // <param name="name"></param>
        public void CreateProcess(StateImpl state, ZingMethod entryPoint, string name,
            ZingSourceContext context, ZingAttribute contextAttribute)
        {
            uint id = nextProcessId++;
            processes.Add(new Process(state, entryPoint, name, id, this.MySerialNum));
            if(ZingerConfiguration.DoDelayBounding)
            {
                //call Start Process function of the External Scheduler
                ZingDBScheduler.Start(ZingDBSchedState, (int)id);
            }
        }

        //IExplorable
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly")]
        public int NumProcesses 
        {
            get 
            { 
                // Debug.Assert(!IsTerminalState);
                return processes.Count; 
            }
        }

        //IExplorable
        public ProcessStatus GetProcessStatus(int processId)
        {
            Process p = (Process) processes[processId];

            return p.CurrentStatus;
        }

        //IExplorable
        public ProcessInfo GetProcessInfo(int processId)
        {
            Process p = (Process) processes[processId];
            ProcessInfo pi = new ProcessInfo(p);
            return pi;
        }

        //IExplorable
        public ProcessInfo[] GetProcessInfo()
        {
            ProcessInfo[] pi = new ProcessInfo[NumProcesses];

            for (int i = 0, n = NumProcesses; i < n; i++)
                pi[i] = new ProcessInfo((Process) processes[i]);
            return pi;
        }

        //IExplorable
        public ProcessStatus[] GetProcessStatus()
        {
            int nProcesses = processes.Count;
            ProcessStatus[] ret = new ProcessStatus[nProcesses];
            
            for (int i = 0; i < nProcesses; i++)
                ret[i] = ((Process) processes[i]).CurrentStatus;

            return ret;
        }

        public bool ContainsGlobalVariable(string name)
        {
            return Utils.ContainsVariable(this, "globals", name);
        }

        public bool ContainsLocalVariable(int processId, string name)
        {
            return ((Process)this.processes[processId]).ContainsVariable(name);
        }

        public object LookupGlobalVarByName(string name)
        {
            return Utils.LookupValueByName(this, "globals", name);
        }

        public object LookupLocalVarByName(int processId, string name)
        {
            return ((Process)this.processes[processId]).LookupValueByName(name);
        }

        #endregion

       
        #region Global Variables
        protected abstract ZingGlobals Globals { get; set; }
        #endregion

        //IExplorable
        public void TraverseGlobalReferences(FieldTraverser ft)
        {
            Globals.TraverseFields(ft);
        }

        #region Heap Management

        int nextFreePointer = 1;
        bool heapGarbage = true;

        int FindFreePointer()
        {       
            while(heap[nextFreePointer] != null)
            {
                nextFreePointer++;
                if(nextFreePointer >= heap.Length)
                {
                    if(heapGarbage)
                    {
                        heapGarbage = false;
                        nextFreePointer = 1;
                    }
                    else{
                        // grow the heap
                        HeapEntry[] newHeap = new HeapEntry[heap.Length * 2];
                        Array.Copy(heap, 0, newHeap, 0, heap.Length);
                        heap = newHeap;
                        //System.Console.WriteLine("Heap is growing to {0}", heap.Length);
                        break;
                    }
                }
            }
            return nextFreePointer;
        }

        // <summary>
        // Deposits the given object into the heap and returns a reference to it.
        // Called when Zing code allocates a complex type.
        // </summary>
        public Pointer Allocate(HeapElement he)
        {
            Debug.Assert(he.Application == this);

            // Defer creation of the hash table until the heap is actually used. We
            // want the overhead to be minimal for simple Zing apps.
            if (heap == null)
                //heap = new Hashtable();
                heap = new HeapEntry[16];

            // Allocate a heap entry
            HeapEntry entry = new HeapEntry(he);
            int ptr = FindFreePointer();
            //int ptr = ++lastHeapId;
            heap[ptr] = entry;
            //heap.Add(ptr, entry);
            he.Pointer = (Pointer)((uint)ptr);  // the other fields should already be initialized
            AddToDirtyHeapPointers(he.Pointer);
            
            return he.Pointer;
        }

        // <summary>
        // Assign omega to a pointer.
        // Do not actually create a heap object.
        // </summary>
        public Pointer AllocateOmega(HeapElement he)
        {
            Debug.Assert(he.Application == this);

            // Defer creation of the hash table until the heap is actually used. We
            // want the overhead to be minimal for simple Zing apps.
            if (heap == null)
                //heap = new Hashtable();
                heap = new HeapEntry[16];

            // Allocate a heap entry
            HeapEntry entry = new HeapEntry(he);
            int ptr = FindFreePointer(); 
            heap[ptr] = entry;
            he.Pointer = (Pointer) ((uint)ptr);  // the other fields should already be initialized
            return he.Pointer;
        }

        // <summary>
        // Returns a heap element given it's "pointer". Called when Zing code
        // dereferences a "Zing pointer".
        // </summary>
        // <param name="ptr"></param>
        // <returns></returns>
        public HeapElement LookupObject(Pointer ptr)
        {
           // TODO: need to return more context in this exception
            if (ptr == 0u)
                throw new ZingNullReferenceException();

            // This would be an internal error in our compiler or runtime.
            if (heap == null)
                throw new InvalidOperationException("Pointer dereference before first heap allocation");

            HeapEntry he = (HeapEntry) heap[ptr];
            return he.HeapObj;
        }

        public HeapElement LookupHeapElementFromIndex(uint index)
        {
            Pointer ptr = new Pointer(index);
            HeapEntry he = (HeapEntry) heap[ptr];
            return he.HeapObj;
        }

        internal Pointer ReverseLookupObject(HeapElement heapElement)
        {
/*          foreach (DictionaryEntry de in heap)
            {
                HeapEntry he = (HeapEntry) de.Value;
                if (he.HeapObj == heapElement)
                    return (Pointer) de.Key;
            }
*/
            for(int ptr = 0; ptr < heap.Length; ptr++)
            {
                if(heap[ptr] != null && heap[ptr].HeapObj == heapElement)
                    return (Pointer) ((uint)ptr);
            }
            throw new ArgumentException("ReverseLookupObject can't find heap element");
        }

        //IExplorable
        public void MarkGarbage(Pointer ptr)
        {
            HeapEntry he = heap[ptr];
            Debug.Assert(he != null);
            Debug.Assert(he.HeapObj != null);
            heap[ptr] = null;
            AddToGarbageHeapEntries(he);
            heapGarbage = true;
        }

        // <summary>
        // During any WriteString method, when a heap reference is encountered we
        // call this method to get a canonical id to write out rather than writing
        // the pointer directly. This ensures that "equivalent" heaps result in
        // identical strings (and fingerprints).
        //
        //  All the functionality is now shifted to the HeapCanonicalizer class --- madanm
        // </summary>
        // <param name="ptr"></param>
        // <returns></returns>
        //IExplorable
        public uint GetCanonicalId(Pointer ptr)
        {
            return StateImpl.GetHeapCanonicalizer(this.MySerialNum).GetCanonicalId(this, ptr);
        }

        internal void TraverseHeap(FieldTraverser edgeTrav, FieldTraverser nodeTrav)
        {
            HeapTraverser.TraverseHeap(this, edgeTrav, nodeTrav);
        }

        // <summary>
        // This is the heap. Map from Pointer to HeapEntry
        // </summary>

        private HeapEntry[] heap;
        //IExplorable
        internal HeapEntry[] Heap { get { return heap; } }

        private uint lastHeapId;

        #endregion

        #region Public methods
        // First, the methods related to state delta

        //IExplorable
        public  Receipt CheckIn()
        {
            HistoryEntry he;

            if (historyStack == null)
                historyStack = new Stack();

            Int64 receiptId = nonce;
            nonce++;
            
            if (historyStack.Count > 0) 
            {
                he = (HistoryEntry) historyStack.Peek();
            }

#if DEBUG_STATEIMPL
            Console.WriteLine("debug: Checking in ({0})...", receiptId);
#endif
            
            he = new HistoryEntry();
            he.stateULE = DoCheckIn();
            he.receiptId = receiptId;
            
            historyStack.Push(he);
            
            return new Receipt(receiptId);
        }

        //IExplorable
        public void Rollback(Receipt rcpt)
        {
            ArrayList ules = new ArrayList();

#if DEBUG_STATEIMPL
            Console.WriteLine("debug: Rolling back ({0})...", rcpt.Id);
#endif
            while (historyStack.Count > 0) 
            {
                HistoryEntry he = (HistoryEntry) historyStack.Peek();

                Debug.Assert(he.receiptId >= rcpt.Id);

                if (he.receiptId == rcpt.Id) 
                {
                    DoRollback(ules.ToArray(), rcpt.Id);
                    DoCheckout(he.stateULE);
                    return;
                }
                ules.Add(he.stateULE);
                historyStack.Pop();
            }
            throw new ArgumentException("invalid receipt");
        }

        //IExplorable
        public void Revert()
        {
            Debug.Assert(historyStack.Count > 0);

            HistoryEntry he = (HistoryEntry) historyStack.Peek();
            DoRevert();
            DoCheckout(he.stateULE);
        }

        //IExplorable
        public bool ShouldRunBlocksContinue(Process process)
        {
            return(!process.IsPreemptible && !process.choicePending && this.exception == null);
        }

        //IExplorable
        internal void RunBlocks(Process process)
        {
            int numBlocksExecuted = 0;
            process.BackTransitionEncountered = false;
            // set the middle of transition flag here
            do 
            {
                ushort thisBlock = process.TopOfStack.NextBlock;
                process.MiddleOfTransition = true;

                IsCall = false;
                IsReturn = false;
                IsEndAtomicBlock = false;
                int savedAtomicityLevel = process.AtomicityLevel;

                process.RunNextBlock();

                if (process.AtomicityLevel == savedAtomicityLevel - 1)
                    IsEndAtomicBlock = true;
                else
                    Debug.Assert(process.AtomicityLevel == savedAtomicityLevel || process.AtomicityLevel == savedAtomicityLevel + 1);

                // In the Blocks enum, 0 is None, and 1 is Enter. The remaining blocks have
                // the expected ordering, so we just exclude None and Enter from consideration.
                if (!IsCall && !IsReturn && thisBlock > 1 && process.TopOfStack != null && process.TopOfStack.NextBlock > thisBlock)
                    process.BackTransitionEncountered = true;

                // Stop infinite executions within an atomic block
                if (numBlocksExecuted++ > 100000)
                    this.exception = new ZingInfiniteLoopException();

            } while (ShouldRunBlocksContinue(process));
            
        }

        //IExplorable
        public void RunProcess(int processId)
        {
            if (!this.IsNormalState)
                throw new InvalidOperationException("Must call RunProcess() in a normal execution state");

            if (processId < 0 || processId >= this.NumProcesses)
                throw new ArgumentException("Process number is out of range", "processId");

            if (GetProcessStatus(processId) != ProcessStatus.Runnable)
                throw new InvalidOperationException("The selected process is not in a runnable state");

            Process p = (Process)this.processes[processId];

            Debug.Assert(!p.choicePending);
            Debug.Assert(p.IsPreemptible);

            RunBlocks(p);

            // Time increments after each atomic execution sequence completes.
            if (!p.choicePending)
                this.stepNumber++;

            fingerprint = null;

            // Check for invalid endstates. As a small optimization, we only check when
            // the process we just executed is itself not runnable.
            if (p.CurrentStatus == ProcessStatus.Runnable)
                return;


            // Check to see if anything else is runnable. If not, make sure all the
            // processes are completed or in a valid end state. If anyone is blocked
            // in an invalid endstate, put an exception on the state object so we
            // can treat this the same as other kinds of errors.
            //if we are summarizing, there could be other runnable processes
            //but we might not be seeing them because we are seeing a truncated state
            if (IsInvalidEndState())
                this.exception = new ZingInvalidEndStateException(); 

            if(ZingerConfiguration.DoDelayBounding && p.CurrentStatus == ProcessStatus.Completed)
            {
                this.ZingDBScheduler.Finish(this.ZingDBSchedState, (int)p.Id);
            }
        }

        //IExplorable
        public void RunChoice(int choice)
        {
            if (!this.IsChoicePending)
                throw new InvalidOperationException("Cannot proceed in a terminal state");

            if (choice < 0 || choice >= this.NumChoices)
                throw new ArgumentException("Choice is out of range", "choice");

            Debug.Assert(choiceProcessNumber >= 0);
            Process p = (Process)this.processes[choiceProcessNumber];
            Debug.Assert(p.choicePending);

            choiceTaken = choice;

            RunBlocks(p);

            if (!p.choicePending)
                this.stepNumber++;

            fingerprint = null; 
        }


        //IExplorable
        public Exception Exception
        {
            get { return exception; }
            set { exception = value; }
        }

        private Exception exception;


        //
        // We reuse a single memory stream and binary writer for all instances.
        // This is fine as long as we're single-threaded and is a big perf win.
        //
        // Abhishek: Not if we're using a parallel exploration model!

        private static MemoryStream[] memStream = new MemoryStream[ZingerConfiguration.DegreeOfParallelism];
        protected static MemoryStream MemoryStream { get { return memStream[0]; } }
        private static BinaryWriter[] binWriter = new BinaryWriter[ZingerConfiguration.DegreeOfParallelism];
        protected static BinaryWriter BinaryWriter { get { return binWriter[0]; } }

        public static MemoryStream GetMemoryStream(int SerialNumber)
        {
            if (memStream[SerialNumber] == null)
            {
                memStream[SerialNumber] = new MemoryStream();
            }
            return (memStream[SerialNumber]);
        }

        public static BinaryWriter GetBinaryWriter(int SerialNumber)
        {
            if (binWriter[SerialNumber] == null)
            {
                binWriter[SerialNumber] = new BinaryWriter(GetMemoryStream(SerialNumber));
            }
            return (binWriter[SerialNumber]);
        }

#if PRINT_FINGERPRINTS
        public void PrintFingerprintBuffer(byte[] buffer, int len, bool printLine)
        {
            for(int i=0; i<len; i++)
            {
                System.Console.Write("{0} ",buffer[i]); 
            }
            if(printLine)
            {
                System.Console.WriteLine();
            }
        }
#endif

#if PRINT_FINGERPRINTS
        internal static bool printFingerprintsFlag = true;
        private static int numFingerprints = 0;
#endif
        //IExplorable
        public Fingerprint Fingerprint
        {
            set { fingerprint = value; }
            get
            {
                if (fingerprint == null)
                {
#if PRINT_FINGERPRINTS
                    numFingerprints ++;
                    printFingerprintsFlag = (numFingerprints == 143101 || numFingerprints == 718618);
#endif

#if NONINC_FINGERPRINTS
                    StateImpl.GetHeapCanonicalizer(mySerialNum).OnHeapTraversalStart(this);
                    fingerprint = NonincrementalFingerprint();
                    StateImpl.GetHeapCanonicalizer(mySerialNum).OnHeapTraversalEnd(this);
#else


                    heapCanonicalizer.OnHeapTraversalStart(this);
                    fingerprint = ComputeFingerprint();
                    heapCanonicalizer.OnHeapTraversalEnd(this);

#if COMPARE_INC_NONINC_FINGERPRINTS
                    heapCanonicalizer.OnHeapTraversalStart(this);
                    Fingerprint noninc = NonincrementalFingerprint();
                    heapCanonicalizer.OnHeapTraversalEnd(this);
                    if(!fingerprint.Equals(noninc))
                    {
                        Debug.Assert(false, "Incremental fingerprint != Nonincremental fingerprint");                   
                    }
#endif
#endif
 
#if PRINT_FINGERPRINTS
                    if(printFingerprintsFlag)
                    {
                        System.Console.WriteLine("{0}: {1}", numFingerprints, fingerprint);
                    }
#endif
                }

                return fingerprint;
            }
        }

/*      public void ClearFingerPrint()
        {
            fingerprint = null;
        }
 */       
        private Fingerprint fingerprint;

        // <summary>
        // The number of choices currently available (if ChoiceIsPending == true)
        // </summary>
        // <remarks>
        // This property may only be queried when ChoicePending is true (otherwise
        // it throws an exception). It returns the total number of possible choices.
        // The choice number passed to ExecuteWithChoice must be less than NumChoices.
        // TODO: Debuggers and interactive simulators will want more information about
        // the possible choices. We'll need a facility for retrieving more friendly
        // information about the alternatives.
        // </remarks>
        //IExplorable
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly")]
        public ushort NumChoices
        {
            get
            {
                if (choiceList == null)
                    return 0;

                return (ushort)choiceList.Length;
            }
        }

        // <summary>
        // This member is set when we execute an expression containing the "choose"
        // operator. We dynamically construct an array of possible choices and allow
        // the caller to select from by index only. This needs to be public so the
        // generated code can supply the list of choices.
        // </summary>
        private object[] choiceList;

        // <summary>
        // This is the process in which choices are pending.
        // </summary>
        internal int choiceProcessNumber = -1;

        // <summary>
        // This member records the choice selected by the caller until the generated
        // code consumes it.
        // </summary>
        private int choiceTaken = -1;
        public int ChoiceTaken 
        {
            get { return choiceTaken; }
        }

        // <summary>
        // Called by the generated code when a "choose" operator is encountered.
        // </summary>
        // <param name="process"></param>
        // <param name="choices"></param>
        public void SetPendingChoices(Process process, object[] choices)
        {
            if (choices.Length == 0)
                throw new ZingInvalidChooseException();

            process.choicePending = true;

            // TODO: this is gross - fix it
            choiceProcessNumber = this.processes.IndexOf(process);
            choiceList = choices;
        }

#if UNUSED_CODE
        internal void FireChoiceSummary(Process p, ChoiceSummary cs)
        {
            if (cs == null) 
            {
                choiceProcessNumber = -1;
                p.choicePending = false;
                choiceList = null;
                return;
            }

            SetPendingChoices(p, cs.ChoiceList);
        }
#endif

        public bool SetPendingSelectChoices(Process process, ulong joinStatementFlags)
        {
            int nChoices = 0;
            int n;
            ulong mask;

            for (n = 0, mask = 1; n < 64 ;n++, mask <<= 1)
            {
                if ((joinStatementFlags & mask) != 0)
                    nChoices++;
            }

            Debug.Assert(nChoices > 0);

            if (nChoices == 1)
            {
                //
                // This insures that if we AREN'T going to have a choice node here,
                // that we'll proceed directly to the one available join statement
                // without leaving RunBlocks(). Otherwise, we lose the value of
                // savedRunnableJoinstatements.
                //
                process.MiddleOfTransition = true;
                return false;
            }

            object[] choices = new object[nChoices];

            for (nChoices = n = 0, mask = 1; n < 64 ;n++, mask <<= 1)
            {
                if ((joinStatementFlags & mask) != 0)
                    choices[nChoices++] = mask;
            }

            this.SetPendingChoices(process,  choices);

            return true;
        }

        // <summary>
        // Called by the generated code following a "choose" to obtain the chosen value.
        // </summary>
        // <param name="p">The calling process</param>
        // <returns></returns>
        public object GetSelectedChoiceValue(Process process)
        {
            object choiceValue;

            Debug.Assert(process.choicePending);
            Debug.Assert(choiceList != null);

            choiceValue = choiceList[choiceTaken];

            choiceProcessNumber = -1;
            choiceTaken = -1;
            choiceList = null;
            process.choicePending = false;

            return choiceValue;
        }
        #endregion

        #region Helpful overrides

        //IExplorable
        public override bool Equals(object obj)
        {
            StateImpl other = (StateImpl) obj;

            // Correct with high probability...
            return this.Fingerprint == other.Fingerprint;
        }

        //IExplorable
        public override int GetHashCode()
        {
            return Fingerprint.GetHashCode();
        }

        //IExplorable
        static public new bool Equals(object obj1, object obj2)
        {
            Debug.Assert(obj1 is StateImpl && obj2 is StateImpl);

            return obj1.Equals(obj2);
        }

        #endregion

        #region Lifted from State.cs

        //IExplorable
        public Exception Error 
        {
            get { return this.Exception; }
        }

        public static StateImpl Load (string zingAssemblyPath)
        {
            Assembly asm;

            asm = Assembly.LoadFrom(zingAssemblyPath);

            return StateImpl.Load(asm);
        }

        [ZoneIdentityPermissionAttribute(SecurityAction.Demand, Zone=SecurityZone.MyComputer)]
        //IExplorable
        public static StateImpl Load (Assembly zingAssembly, ZingerDelayingScheduler ZShed, ZingerSchedulerState ZShedState)
        {
            StateImpl s =
                (StateImpl) zingAssembly.CreateInstance("Microsoft.Zing.Application", false, 
                BindingFlags.CreateInstance, null, 
                new object[] { true, ZShed, ZShedState }, 
                null, new object[] {});
            if (s == null)
                throw new ArgumentException("invalid assembly");

            return s;
        }

        [ZoneIdentityPermissionAttribute(SecurityAction.Demand, Zone = SecurityZone.MyComputer)]
        //IExplorable
        public static StateImpl Load (Assembly zingAssembly)
        {
            StateImpl s =
                (StateImpl)zingAssembly.CreateInstance("Microsoft.Zing.Application", false,
                BindingFlags.CreateInstance, null,
                new object[] {true},
                null, new object[] { });
            if (s == null)
                throw new ArgumentException("invalid assembly");

            return s;
        }

        #endregion

        #region Events

        private ArrayList events;
        private ArrayList traceLog;

        public void InvokeScheduler(params object[] arguments)
        {
            if(ZingerConfiguration.DoDelayBounding)
            {
                ZingDBScheduler.Invoke(ZingDBSchedState, arguments);
            }
        }

        public void Trace(ZingSourceContext context, ZingAttribute contextAttribute,
            string message, params object[] arguments)
        {
            if (!ZingerConfiguration.ExecuteTraceStatements)
                return;


            if (ZingerConfiguration.DegreeOfParallelism == 1)
            {
                ReportEvent(new TraceEvent(context, contextAttribute, message, arguments));
                if (ZingerConfiguration.EnableTrace)
                {
                    ReportTrace(new TraceEvent(context, contextAttribute, message, arguments));
                }
            }
            else
            {
                ReportEvent(new TraceEvent(context, contextAttribute, message,  this.MySerialNum, arguments));
                if (ZingerConfiguration.EnableTrace)
                {
                    ReportTrace(new TraceEvent(context, contextAttribute, message, this.MySerialNum, arguments));
                }
            }
            
        }

        //IExplorable
        public ZingEvent[] GetEvents()
        {
            if (events == null)
                return new ZingEvent[] { };

            return (ZingEvent[]) events.ToArray(typeof(ZingEvent));
        }

        public ZingEvent[] GetTraceLog()
        {
            if (traceLog == null)
                return new ZingEvent[] { };

            return (ZingEvent[])traceLog.ToArray(typeof(ZingEvent));
        }

        internal void ReportEvent(ZingEvent ev)
        {
            Debug.Assert(ZingerConfiguration.ExecuteTraceStatements, 
                "Shouldn't call ReportEvent with events disabled");

            if (events == null)
                events = new ArrayList(1);

            events.Add(ev);
        }

        #endregion


        #region Trace
        internal void ReportTrace(ZingEvent ev)
        {
            Debug.Assert(ZingerConfiguration.ExecuteTraceStatements,
                "Shouldn't call ReportTrace with tracing disabled");

            if (traceLog == null)
                traceLog = new ArrayList(1);

            traceLog.Add(ev);
        }
        #endregion

        #region Cloning

        /// <summary>
        /// Used in parallel exploration as a unique identifier.
        /// Serial numbers begin from 0 and go upto Options.PLINQDegreeOfParallelism - 1
        /// </summary>
        private int mySerialNum = 0;

        public int MySerialNum
        {
            get { return mySerialNum; }
            set { mySerialNum = value; }
        }

        public object Clone(int SerialNum)
        {
            Object retval;
            StateImpl ClonedState;

            retval = this.Clone();
            ClonedState = retval as StateImpl;
            ClonedState.MySerialNum = SerialNum;

            // Set the serial numbers of the processes
            foreach (Process proc in ClonedState.processes)
            {
                proc.MyThreadId = ClonedState.mySerialNum;
            }
            return (retval);
        }

        // <summary>
        // Called by the Clone() method in derived classes to copy members that are private
        // to the base class.
        // </summary>
        // <param name="newState"></param>

        // Use this method to get a bunch of stateImpl objects for parallel exploration

        [SuppressMessage("Microsoft.Performance", "CA1804:RemoveUnusedLocals")]
        //IExplorable
        public object Clone()
        {
            //
            // This is overkill, but we need to make sure we've walked the heap references
            // before cloning. Otherwise, our OrderId's aren't set and we garbage-collect
            // everything incorrectly.
            //
            Fingerprint fp = null;

            fp = this.Fingerprint;

            StateImpl newState = MakeSkeleton();
            if (ZingerConfiguration.DoDelayBounding)
            {
                newState.ZingDBSchedState = ZingDBSchedState.Clone(false);
                newState.ZingDBScheduler = ZingDBScheduler;
            }
            newState.choiceList = this.choiceList;
            newState.choiceProcessNumber = this.choiceProcessNumber;

            newState.nextProcessId = this.nextProcessId;
            newState.stepNumber = this.stepNumber;
            newState.events = null;

            // Create a process list of the required size
            newState.processes = new ArrayList(this.processes.Count);

            // Clone each source process and add it to our list
            for (int i=0; i < this.processes.Count ;i++)
            {
                // Do a deep copy of each process
                // The second argument to clone is set to false to indicate
                // that a deep copy is required here
                if (processes[i] != null)
                {
                    newState.processes.Add(((Process)processes[i]).Clone(newState, false));
                    (newState.processes[i] as Process).MyThreadId = this.MySerialNum;
                }
                else
                {
                    newState.processes.Add(null);
                }
            }

            newState.Globals = (ZingGlobals) Globals.Clone(newState);

            // Clone the heap, if necessary
            if (this.heap != null)
            {
                newState.lastHeapId = this.lastHeapId;
                //newState.heap = new Hashtable(this.heap.Count);
                //foreach (DictionaryEntry de in this.heap)
                //{
                //  HeapEntry heapEntry = (HeapEntry) de.Value;

                newState.heap = new HeapEntry[this.heap.Length];
                for(int ptr = 0; ptr<this.heap.Length; ptr++)
                {
                    HeapEntry heapEntry = this.heap[ptr];
                    if(heapEntry == null) continue;

                        HeapElement elem = (HeapElement) heapEntry.HeapObj.Clone();
                        //BUGFIX: After we clone the heap element, set the delta management related fields appropriately
                        elem.Application = newState;
                        elem.IsDirty = false;
                        elem.version = newState.Nonce;
                        elem.next = null;
                        //Debug.Assert(elem.ptr == (Pointer) de.Key);
                        Debug.Assert(elem.Pointer == ptr);

                        HeapEntry he = new HeapEntry(elem);
                        //newState.heap.Add(de.Key, he);
                        newState.heap[ptr] = he;
                }
            }

            // Also make the memory streams point to the same thing
            // we do not need the new memory streams and bin writers in the 
            // new object
            newState.MySerialNum = this.MySerialNum;

            return newState;
        }
        #endregion

        #region Fingerprinting

        // This ought not to be static here for parallel explorations!
        // private static HeapCanonicalizer heapCanonicalizer = new HeapCanonicalizer();
        private static HeapCanonicalizer[] heapCanonicalizers = new HeapCanonicalizer[ZingerConfiguration.DegreeOfParallelism];

        private static HeapCanonicalizer HeapCanonicalizer
        {
            get
            {
                if (heapCanonicalizers[0] == null)
                {
                    heapCanonicalizers[0] = new HeapCanonicalizer(0);
                }
                return (heapCanonicalizers[0]);
            }
        }

        private static HeapCanonicalizer GetHeapCanonicalizer(int MySerialNumber)
        {
            if (heapCanonicalizers[MySerialNumber] == null)
            {
                heapCanonicalizers[MySerialNumber] = new HeapCanonicalizer(MySerialNumber);
            }
            return (heapCanonicalizers[MySerialNumber]);
        }

        private Fingerprint NonincrementalFingerprint()
        {
            BinaryWriter MyBinWriter = GetBinaryWriter(mySerialNum);
            MyBinWriter.Seek(0, SeekOrigin.Begin);
            this.WriteString(MyBinWriter);
            MemoryStream MyStream = GetMemoryStream(mySerialNum);
            Fingerprint noninc = computeHASH.GetFingerprint(MyStream.GetBuffer(), (int)MyStream.Position, 0);
#if PRINT_FINGERPRINTS
            if(printFingerprintsFlag)
            {
                PrintFingerprintBuffer(memStream.GetBuffer(), (int)memStream.Position, true);
            }
#endif
        return noninc;
        }
        /// <summary>
        /// Compute Fingerprint Incrementally
        /// 
        /// Fingerprint of State = concat of Fingerprint of all processes, and the Fingerprint of the heap 
        ///
        /// Note: Fingerprint of globals is computed by the Zing.Application subclass
        ///       
        /// </summary>
        /// <returns> Incrementally computed Fingerprint</returns>

        public virtual Fingerprint ComputeFingerprint()
        {
            Fingerprint myFingerprint = new Fingerprint();

            foreach (Process p in processes)
            {
                if( p != null)
                {
                    Fingerprint procPrint = p.ComputeFingerprint(this);
                    myFingerprint.Concatenate(procPrint);
                }
            }

            Fingerprint heapPrint = StateImpl.GetHeapCanonicalizer(mySerialNum).ComputeHeapFingerprint(this);
            myFingerprint.Concatenate(heapPrint);

            return myFingerprint;
        }

        public Fingerprint FingerprintNonHeapBuffer(byte[] buffer, int len)
        {
            Fingerprint f = StateImpl.GetHeapCanonicalizer(mySerialNum).FingerprintNonHeapBuffer(buffer, len);            

#if PRINT_FINGERPRINTS
            if(printFingerprintsFlag)
            {
                PrintFingerprintBuffer(buffer, len, true);
                System.Console.WriteLine("{0}", f);
            }
#endif
            return f;
        }

        //IExplorable
        public virtual void WriteString(BinaryWriter bw)
        {
            // Our subclass will write its globals before calling us.

            // Write the process stacks
            // TODO: sort the process list before writing its string
            foreach (Process p in processes)
            {
                if( p != null) 
                {
                    p.WriteString(this, bw);
                }
            }

            // Write the contents of the heap
            StateImpl.GetHeapCanonicalizer(mySerialNum).WriteString(this, bw);
        }

        #endregion

        #region Reflection-based implementation of ToString

        public string PrintNoHeap()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("State:");


            sb.Append("[fingerprint =");
            if (this.NumChoices == 0)
            {
                sb.Append(this.Fingerprint.ToString());
                sb.Append("]\r\n");
            }
            else
            {
                sb.Append("<no fingerprint - state is not stable>]\r\n");
                sb.AppendFormat("    {0} choices pending\r\n", NumChoices);
            }

            sb.Append("\r\n");
            sb.Append("\r\n");
            sb.AppendFormat("   IsAcceptingState - {0}", IsAcceptingState);
            sb.Append("\r\n");
            if (this.events != null)
            {
                sb.Append("  Events:\r\n");
                foreach (ZingEvent e in this.events)
                    sb.AppendFormat("    {0}\r\n", e);

                sb.Append("\r\n");
            }

            sb.Append("  Globals:\r\n");
            DumpGlobals(sb);
            sb.Append("\r\n");

            //DumpHeap(sb);

            sb.AppendFormat("  Processes: ({0})\r\n", processes.Count);

            foreach (Process p in processes)
                DumpProcess(sb, p);

            return sb.ToString();
        }



        // <summary>
        // Format the current state as a human-friendly string.
        // </summary>
        // <returns>String representation of the current state.</returns>
        //IExplorable
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("State:");

            
            sb.Append("[fingerprint =");
            if (this.NumChoices == 0)
            {
                sb.Append(this.Fingerprint.ToString());
                sb.Append("]\r\n");
            }
            else
            {
                sb.Append("<no fingerprint - state is not stable>]\r\n");
                sb.AppendFormat("    {0} choices pending\r\n", NumChoices);
            }

            sb.Append("\r\n");
            
            if (this.events != null)
            {
                sb.Append("  Events:\r\n");
                foreach (ZingEvent e in this.events)
                    sb.AppendFormat("    {0}\r\n", e);

                sb.Append("\r\n");
            }


            sb.Append("  Globals:\r\n");
            DumpGlobals(sb);
            sb.Append("\r\n");

            DumpHeap(sb);

            sb.AppendFormat("  Processes: ({0})\r\n", processes.Count);

            foreach (Process p in processes)
                DumpProcess(sb, p);

            return sb.ToString();
        }


        private void DumpGlobals(StringBuilder sb)
        {
            string leader = "  ";
            System.Type classType = this.GetType();
            Object structObj;

            try
            {
                structObj = classType.InvokeMember(
                    "globals",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.GetField,
                    null, this, new object [] {}, CultureInfo.CurrentCulture);
            }
            catch (System.MissingFieldException)
            {
                // Some fields are optional, so we don't complain if they aren't present
                return;
            }
            System.Type structType = structObj.GetType();

            FieldInfo[] structFields = structType.GetFields(BindingFlags.Instance|BindingFlags.Public);

            foreach (FieldInfo fi in structFields)
            {
                if (!fi.Name.StartsWith("priv_"))
                    continue;

                object val = structType.InvokeMember(
                    fi.Name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.GetField,
                    null, structObj, new object [] {}, CultureInfo.CurrentCulture);
                
                if (val.GetType() == typeof(Pointer))
                    sb.AppendFormat("{0}ZingPointer {2} = {3}\r\n",
                        leader, Utils.Unmangle(val.GetType()), fi.Name.Substring(8) /* skip priv____ */, (uint) ((Pointer) val));
                else
                    sb.AppendFormat("{0}{1} {2} = {3}\r\n",
                        leader, Utils.Unmangle(val.GetType()), fi.Name.Substring(8) /* skip priv____ */, val);
            }
        }

        private void DumpHeap(StringBuilder sb)
        {
            if (heap != null)
            {
                int itemCount = 0;

                /*
                foreach (DictionaryEntry de in heap)
                {
                    if (((HeapEntry) de.Value).Order != 0)
                        itemCount++;
                }
*/
                for(int ptr = 0; ptr<heap.Length; ptr++)
                {
                    HeapEntry he = heap[ptr];
                    if(he != null)   // && he.Order != 0)
                        itemCount++;
                }

                sb.AppendFormat("  Heap: ({0} items)\r\n", itemCount);

                //foreach (DictionaryEntry dictEntry in heap) 
                //{ 
                //    HeapEntry he = (HeapEntry) dictEntry.Value;
                //    Pointer   ptr = (Pointer) dictEntry.Key;
                for(int ptr = 0; ptr<heap.Length; ptr++)
                {
                    HeapEntry he = heap[ptr];
                    if(he == null) continue;

                    sb.AppendFormat("    Addr= {0}\r\n", (uint) ptr);
                    sb.AppendFormat("    Type= {0}\r\n", Utils.Unmangle(he.HeapObj.GetType()));
                    sb.Append("    Contents:\r\n");
                    sb.Append(he.HeapObj.ToString());
                    sb.Append("\r\n");
                }
            }
            sb.Append("\r\n");
        }

        private void DumpProcess(StringBuilder sb, Process p)
        {
            sb.Append("\r\n");

            sb.AppendFormat("    Process {0}: Name='{1}', Id={2}\r\n",
                processes.IndexOf(p), Utils.Unmangle(p.Name), p.Id);
            sb.AppendFormat("      Status: {0}\r\n", p.CurrentStatus);
            sb.AppendFormat("      Choice pending: {0}\r\n", p.choicePending);
            if (p.EntryPoint != null)
                sb.AppendFormat("      Entry point: {0}\r\n", Utils.Unmangle(p.EntryPoint.GetType()));

            if (p.LastFunctionCompleted != null)
            {
                sb.Append("      Last function completed:\r\n");
                sb.AppendFormat("        Function: {0}\r\n", Utils.Unmangle(p.LastFunctionCompleted.GetType()));
                int oldLen = sb.Length;
                DumpStructMembers(sb, p.LastFunctionCompleted, "outputs", "        ");
                if (sb.Length == oldLen)
                    sb.Append("        (no outputs or return value)\r\n");

                sb.Append("\r\n");
            }

            sb.Append("      Stack:\r\n");

            for (ZingMethod m = p.TopOfStack; m != null ;m = m.Caller)
            {
                sb.AppendFormat("        Function : {0}\r\n", Utils.Unmangle(m.GetType()));

                // If the stack frame includes a "this" pointer, show it
                object thisObj;
                try
                {
                    thisObj = m.GetType().InvokeMember("This", BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetField,
                        null, m, new object[] {}, CultureInfo.CurrentCulture);
                }
                catch (System.MissingFieldException)
                {
                    thisObj = null;
                }
                if (thisObj != null)
                    sb.AppendFormat("          This: {0}\r\n", (Pointer) thisObj);
    
                sb.AppendFormat("          NextBlock: {0}\r\n", m.ProgramCounter);

                sb.Append("          Inputs:\r\n");
                DumpStructMembers(sb, m, "inputs", "            ");

                sb.Append("          Outputs:\r\n");
                DumpStructMembers(sb, m, "outputs", "            ");

                sb.Append("          Locals:\r\n");
                DumpStructMembers(sb, m, "locals", "            ");
                sb.Append("\r\n");
            }
        }

        // <summary>
        // Dump the members of a (named) structure within a given object.
        // </summary>
        // <param name="obj">An object containing the structure of interest</param>
        // <param name="memberName">The name of the structure member</param>
        // <param name="leader">A leader string to predede all output</param>
        // <returns>Formatted string representation of the structure members</returns>
        private static void DumpStructMembers(StringBuilder sb, object obj, string memberName, string leader)
        {
            System.Type classType = obj.GetType();

            // Get the structure from the class
            object structObj;
            try
            {
                structObj = classType.InvokeMember(
                    memberName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetField,
                    null, obj, new object [] {}, CultureInfo.CurrentCulture);
            }
            catch (System.MissingFieldException)
            {
                // Some fields are optional, so we don't complain if they aren't present
                return;
            }

            System.Type structType = structObj.GetType();

            // Iterate over each field in the structure - get the field value (via
            // late binding) and display its type, name, and value

            foreach (FieldInfo fi in structType.GetFields())
            {

                if (!fi.Name.StartsWith("priv_"))
                    continue;

                object val = structType.InvokeMember(
                    fi.Name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetField,
                    null, structObj, new object [] {}, CultureInfo.CurrentCulture);

                if (fi.FieldType == typeof(Pointer))
                    sb.AppendFormat("{0}ZingPointer {2} = {3}\r\n",
                        leader, Utils.Unmangle(fi.FieldType), fi.Name.Substring(8), (uint) ((Pointer) val));
                else
                    sb.AppendFormat("{0}{1} {2} = {3}\r\n",
                        leader, Utils.Externalize(fi.FieldType), fi.Name.Substring(8), val);
            }
        }
        #endregion
        
        #region Reflection-based state dump to XML
        // <summary>
        // Format the current state as XML
        // </summary>
        // <returns>XML representation of the current state.</returns>
        //IExplorable
        public void ToXml(XmlElement rootElement)
        {
            XmlDocument doc = rootElement.OwnerDocument;
            XmlAttribute attr;
            XmlElement elem;

            attr = doc.CreateAttribute("stateType");
            attr.Value = this.Type.ToString();
            rootElement.SetAttributeNode(attr);

            if (Type == StateType.Choice)
            {
                attr = doc.CreateAttribute("numChoices");
                attr.Value = this.NumChoices.ToString(CultureInfo.CurrentUICulture);
                rootElement.SetAttributeNode(attr);
            }

            if (Type == StateType.Execution)
            {
                attr = doc.CreateAttribute("numProcesses");
                attr.Value = this.NumProcesses.ToString(CultureInfo.CurrentUICulture);
                rootElement.SetAttributeNode(attr);
            }

            if (this.NumChoices == 0)
            {
                attr = doc.CreateAttribute("fingerprint");
                attr.Value = this.Fingerprint.ToString();
                rootElement.SetAttributeNode(attr);
            }
            
            if (this.events != null)
            {
                elem = doc.CreateElement("Events");
                rootElement.AppendChild(elem);

                foreach (ZingEvent e in this.events)
                    e.ToXml(elem);
            }

            elem = doc.CreateElement("GlobalVariables");
            rootElement.AppendChild(elem);
            DumpGlobalsToXml(elem);

            elem = doc.CreateElement("Processes");
            rootElement.AppendChild(elem);

            attr = doc.CreateAttribute("numProcesses");
            attr.Value = processes.Count.ToString(CultureInfo.CurrentUICulture);
            elem.SetAttributeNode(attr);
            
            foreach (Process p in processes)
            {
                XmlElement elemProc = doc.CreateElement("Process");
                elem.AppendChild(elemProc);
                DumpProcessToXml(elemProc, p);
            }

            if (heap != null)
            {
                elem = doc.CreateElement("Heap");
                rootElement.AppendChild(elem);

                /*
                foreach (DictionaryEntry dictEntry in heap) 
                {
                    HeapEntry he = (HeapEntry) dictEntry.Value;
                    Pointer   ptr = (Pointer) dictEntry.Key;
                  */
                for(int ptr = 0; ptr<heap.Length; ptr++)
                {
                    HeapEntry he = heap[ptr];
                    if(he == null) continue;

                    XmlElement heapItem = doc.CreateElement("HeapItem");
                    elem.AppendChild(heapItem);

                    attr = doc.CreateAttribute("address");
                    attr.Value = ptr.ToString(CultureInfo.CurrentUICulture);
                    heapItem.SetAttributeNode(attr);

                    he.HeapObj.ToXml(heapItem);
                }
            }
        }

        private void DumpGlobalsToXml(XmlElement globals)
        {
            XmlDocument doc = globals.OwnerDocument;
            System.Type classType = this.GetType();
            Object structObj;

            try
            {
                structObj = classType.InvokeMember(
                    "globals",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.GetField,
                    null, this, new object [] {}, CultureInfo.CurrentCulture);
            }
            catch (System.MissingFieldException)
            {
                return;     // No global variables, which is fine...
            }
            System.Type structType = structObj.GetType();

            FieldInfo[] structFields = structType.GetFields(BindingFlags.Instance|BindingFlags.Public);

            foreach (FieldInfo fi in structFields)
            {
                XmlAttribute attr;

                if (!fi.Name.StartsWith("priv_"))
                    continue;

                object val = structType.InvokeMember(
                    fi.Name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.GetField,
                    null, structObj, new object [] {}, CultureInfo.CurrentCulture);

                string name = fi.Name.Substring(8); // skip "priv____");
                int sepPos = name.IndexOf("____");
                string className = name.Substring(0, sepPos);
                string fieldName = name.Substring(sepPos+4);
                
                XmlElement global = doc.CreateElement("GlobalVariable");
                globals.AppendChild(global);

                attr = doc.CreateAttribute("className");
                attr.Value = className;
                global.SetAttributeNode(attr);

                attr = doc.CreateAttribute("fieldName");
                attr.Value = fieldName;
                global.SetAttributeNode(attr);

                Type fieldType = val.GetType();
                if (fieldType == typeof(Pointer))
                {
                    attr = doc.CreateAttribute("type");
                    attr.Value = "Microsoft.Zing.Pointer";
                    global.SetAttributeNode(attr);

                    global.InnerText = val.ToString();
                }
                else
                {
                    attr = doc.CreateAttribute("type");
                    attr.Value = Utils.Externalize(fieldType);
                    global.SetAttributeNode(attr);

                    // TODO: if this is a struct, we need to let it format its contents

                    string valText = val.ToString();

                    if (fieldType.IsEnum)
                        valText = Utils.Unmangle(valText);

                    global.InnerText = valText;
                }
            }
        }

        private void DumpProcessToXml(XmlElement elemProc, Process p)
        {
            XmlDocument doc = elemProc.OwnerDocument;
            XmlAttribute attr;

            attr = doc.CreateAttribute("index");
            attr.Value = processes.IndexOf(p).ToString(CultureInfo.CurrentUICulture);
            elemProc.SetAttributeNode(attr);

            attr = doc.CreateAttribute("name");
            attr.Value = Utils.Unmangle(p.Name);
            elemProc.SetAttributeNode(attr);

            attr = doc.CreateAttribute("id");
            attr.Value = p.Id.ToString(CultureInfo.CurrentUICulture);
            elemProc.SetAttributeNode(attr);

            attr = doc.CreateAttribute("status");
            attr.Value = p.CurrentStatus.ToString();
            elemProc.SetAttributeNode(attr);

            attr = doc.CreateAttribute("isChoicePending");
            attr.Value = p.choicePending.ToString();
            elemProc.SetAttributeNode(attr);

            if (p.EntryPoint != null)
            {
                attr = doc.CreateAttribute("entryPoint");
                attr.Value = Utils.Externalize(p.EntryPoint.GetType());
                elemProc.SetAttributeNode(attr);
            }

            if (p.LastFunctionCompleted != null)
            {
                XmlElement elemOutputs = doc.CreateElement("PendingOutputs");
                if (DumpStructMembersToXml(p.LastFunctionCompleted, "outputs", "Output", elemOutputs) > 0)
                {
                    elemProc.AppendChild(elemOutputs);

                    attr = doc.CreateAttribute("functionName");
                    attr.Value = Utils.Externalize(p.LastFunctionCompleted.GetType());
                    elemOutputs.SetAttributeNode(attr);
                }
            }

            XmlElement elemStack = doc.CreateElement("Stack");
            elemProc.AppendChild(elemStack);

            for (ZingMethod m = p.TopOfStack; m != null ;m = m.Caller)
            {
                XmlElement elemFrame = doc.CreateElement("StackFrame");
                elemStack.AppendChild(elemFrame);

                attr = doc.CreateAttribute("functionName");
                attr.Value = Utils.Externalize(m.GetType());
                elemFrame.SetAttributeNode(attr);

                // If the stack frame includes a "this" pointer, show it
                object thisObj;
                try
                {
                    thisObj = m.GetType().InvokeMember("privThis", BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetField,
                        null, m, new object[] {}, CultureInfo.CurrentCulture);
                }
                catch (System.MissingFieldException)
                {
                    thisObj = null;
                }
                if (thisObj != null)
                {
                    attr = doc.CreateAttribute("thisPointer");
                    attr.Value = thisObj.ToString();
                    elemFrame.SetAttributeNode(attr);
                }
    
                attr = doc.CreateAttribute("nextBlock");
                attr.Value = m.ProgramCounter;
                elemFrame.SetAttributeNode(attr);

                XmlElement elemInputs = doc.CreateElement("InputParameters");
                if (DumpStructMembersToXml(m, "inputs", "InputParameter", elemInputs) > 0)
                    elemFrame.AppendChild(elemInputs);

                XmlElement elemOutputs = doc.CreateElement("OutputParameters");
                if (DumpStructMembersToXml(m, "outputs", "OutputParameter", elemOutputs) > 0)
                    elemFrame.AppendChild(elemOutputs);

                XmlElement elemLocals = doc.CreateElement("LocalVariables");
                if (DumpStructMembersToXml(m, "locals", "LocalVariable", elemLocals) > 0)
                    elemFrame.AppendChild(elemLocals);
            }
        }

        // <summary>
        // Dump the members of a (named) structure within a given object.
        // </summary>
        // <param name="obj">An object containing the structure of interest</param>
        // <param name="memberName">The name of the structure member</param>
        // <param name="leader">A leader string to predede all output</param>
        // <returns>Formatted string representation of the structure members</returns>
        private static int DumpStructMembersToXml(object obj, string memberName, string elementName, XmlElement containerElem)
        {
            XmlDocument doc = containerElem.OwnerDocument;
            int numFields = 0;

            System.Type classType = obj.GetType();

            // Get the structure from the class
            object structObj;
            try
            {
                structObj = classType.InvokeMember(
                    memberName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetField,
                    null, obj, new object [] {}, CultureInfo.CurrentCulture);
            }
            catch (System.MissingFieldException)
            {
                // Some fields are optional, so we don't complain if they aren't present
                return 0;
            }

            System.Type structType = structObj.GetType();

            // Iterate over each field in the structure - get the field value (via
            // late binding) and display its type, name, and value

            foreach (FieldInfo fi in structType.GetFields())
            {
                XmlAttribute attr;
                const string prefix = "priv____";

                if (!fi.Name.StartsWith(prefix))
                    continue;

                object val = structType.InvokeMember(
                    fi.Name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetField,
                    null, structObj, new object [] {}, CultureInfo.CurrentCulture);

                string name = fi.Name.Substring(prefix.Length);

                XmlElement memberElem = doc.CreateElement(elementName);
                containerElem.AppendChild(memberElem);

                attr = doc.CreateAttribute("name");
                attr.Value = name;
                memberElem.SetAttributeNode(attr);

                if (val == null)
                {
                    // It can happen in the case of string!!!
                    // This if-block added by Jiri Adamek to fix the problem with runtime exception when running
                    // viewer on a Zing code with strings

                    attr = doc.CreateAttribute("type");
                    attr.Value = "System.String";
                    memberElem.SetAttributeNode(attr);

                    memberElem.InnerText = "null";
                }
                else
                {

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

                numFields++;
            }

            return numFields;
        }
        #endregion
    }


    #region Field Traversal Interface
    /// <summary>
    /// Field Traverser is a 'functor'-like object. The client provides a customised FieldTraverer 
    /// object (derived from FieldTraverser) that encodes the required functionality in the doTraversal
    /// function. 
    /// 
    /// Implementation Note: This is the natural way to implement generic functionality in C++. However, 
    /// C# provides 'delgates', which are more natural. Unfortunately, the current implementation of delegates
    /// in the Everett/Whidbey CLR is around 6 times slower than a regular virtual function call --- madanm
    /// </summary>
    [CLSCompliant(false)]
    
    public abstract class FieldTraverser
    {
        /// <summary>
        /// This function will be called for every field in the object
        /// </summary>
        /// <param name="field"> The field object. Non object types will be automatically boxed</param>
        public abstract void DoTraversal(Object field);

        // To avoid the boxing overhead, override the following function.
        // They default to the boxing version

        public virtual void DoTraversal(Pointer ptr)
        {
            DoTraversal((Object)ptr);
        }

    }

    #endregion

    #region Heap-related definitions

    // <summary>
    // Simple structure defining a virtual pointer for our heap.
    // </summary>
    //public class Pointer
    [CLSCompliant(false)]
    
    public struct Pointer :IComparable
    {
        // <summary>
        // Construct a pointer type from an unsigned int
        // </summary>
        // <param name="ptr"></param>
        public Pointer (uint ptr)
        {
            this.ptr = ptr;
        }

        // <summary>
        // Implicit conversion allows Pointer to be transparently converted to
        // uint.
        // </summary>
        // <param name="p"></param>
        // <returns></returns>
        public static implicit operator uint(Pointer pointer)
        {
            return (uint)pointer.ptr;
        }

        // <summary>
        // Implicit conversion allows unsigned ints to be directly assigned to
        // Pointers.
        // </summary>
        // <param name="i"></param>
        // <returns></returns>
        public static implicit operator Pointer(uint value)
        {
            return new Pointer(value);
        }

        public override bool Equals(object obj)
        {
            return ptr == ((Pointer)obj).ptr;
        }

        public override int GetHashCode()
        {
            return (int) ptr;
        }
        
        public static bool operator == (Pointer p1, Pointer p2)
        {
            return p1.ptr == p2.ptr;
        }

        public static bool operator != (Pointer p1, Pointer p2)
        {   
            return p1.ptr != p2.ptr;
        }

        public static bool operator < (Pointer p1, Pointer p2)
        {
            return p1.CompareTo(p2) < 0;
        }
                 
        public static bool operator > (Pointer p1, Pointer p2)
        {
            return p1.CompareTo(p2) > 0;
        }
                 
        private uint ptr;

        public override string ToString()
        {
            return ptr.ToString(CultureInfo.CurrentUICulture);
        }       
 
        #region IComparable Members

        public int CompareTo(object obj)
        {
            Pointer p = (Pointer) obj;
            return this.ptr.CompareTo(p.ptr);
        }

        #endregion
    }

    /// <exclude/>
    /// <summary>
    /// HeapEntry is the type of every object in the heap's hash table
    /// HeapEntry is a pair: an order and a reference to a heap element
    /// </summary>
    internal class HeapEntry 
    {
        internal HeapEntry(HeapElement he)
        {
            heList = he;
        }

        // <summary>
        // accessor functions for the heap element associated with the heap entry
        // </summary>
        public HeapElement HeapObj { 
            get { 
                return heList;
            } 
        }

        public void DoCheckIn() 
        {
            HeapObj.IsDirty = false;
        }

#if UNUSED_CODE
        public void DoCheckout() 
        {
            return;
        }
#endif

        public bool DoRevert(Int64 nonce) 
        {
            Debug.Assert(HeapObj.IsDirty || heList.version == nonce);
            
            heList = heList.next;
                    
            Debug.Assert(heList == null || !HeapObj.IsDirty);
            
            return (heList == null);
        }

        public bool DoRollback(Int64 n)
        {
            while (heList != null) 
            {
                if (heList.version > n) 
                    heList = heList.next;
                else 
                    break;
            }

            Debug.Assert(heList == null || !HeapObj.IsDirty);

            return (heList == null);
        }

        // incremenal heap canoninicalization fields
        // These are temporary place holders valid during the fingerprint phase
        // At the end of the fingerprint, the currCanonId and currFingerprint values are
        // 'committed' to the HeapElement
        public int currCanonId;
        public Fingerprint currFingerprint;

        // list of HeapElements
        public HeapElement heList;
    }

    /// <exclude/>
    /// <summary>
    /// Base class for all complex Zing types.
    /// </summary>
    /// <remarks>
    /// This is the base class for all complex Zing types. This gives a common
    /// infrastructure for garbage collection. For each declared union, class,
    /// array, etc., the Zing compiler will construct a class deriving from
    /// HeapElement and will provide the implementation for any abstract
    /// methods specified here. A Mark() method is shown here as one possible
    /// candidate, but this will be entirely driven by the needs of the heap.
    /// </remarks>
    [CLSCompliant(false)]
    
    public abstract class HeapElement : ICloneable
    {
        internal Int64 version;
        internal HeapElement next;

        protected HeapElement()
        {
        }
        
        protected HeapElement(StateImpl application)
        {
            this.application = application;
            version = application.Nonce;
        }

        protected HeapElement(HeapElement he)
        {
            application = he.application;
            version = he.version;
            dirty = he.dirty;
            next = he.next;
            ptr = he.ptr;

            //carry over the fingerprint cache information
            fingerprint = he.fingerprint;
            canonId = he.canonId;
            childReferences = he.childReferences;
        }

        public abstract object Clone();

        public abstract void WriteString(StateImpl state, BinaryWriter bw);

        protected abstract short TypeId { get; }

        private StateImpl application;
        public StateImpl Application
        {
            get { return application; }
            set { application = value; }
        }

        private Pointer ptr;
        public Pointer Pointer
        {
            get { return ptr; }
            set { ptr = value; }
        }

        private bool dirty;
        
        public void SetDirty()
        {
            if (!dirty)
            {
                HeapElement elem = (HeapElement) Clone();
                version = application.Nonce;
                dirty = true;
                next = elem;
                application.AddToDirtyHeapPointers(ptr);
            } 
        }

        public bool IsDirty
        {
            get { return dirty; }
            set { dirty = value; }
        }

        public abstract void ToXml(XmlElement containerElement);
        
        public abstract void TraverseFields(FieldTraverser ft);

        #region Methods for reading and writing fields to avoid reflection
        public abstract object GetValue(int fieldIndex);
        public abstract void SetValue(int fieldIndex, object value);
        #endregion

        // Fields required for incremental fingerprinting
        internal int canonId;
        internal Fingerprint fingerprint;

        internal Pointer[] childReferences; // a cache for child references 
#if DEBUG_INC_FINGERPRINTS
        public byte[] fingerprintedBuffer; // needed only for debugging
        public int fingerprintedOffset;   // needed only for debugging
#endif
    }
    #endregion

    #region Receipt
    public class Receipt 
    {
        // private DeltaManager manager;
        private Int64 id;

        internal Int64 Id { get { return id; } }

        internal Receipt(Int64 rid)
        {
            id = rid;
        }
    }
    #endregion
}
