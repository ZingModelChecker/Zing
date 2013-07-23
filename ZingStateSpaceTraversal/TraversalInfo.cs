using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Reflection;
using System.Diagnostics;

namespace Microsoft.Zing
{
    /// <exclude/>

    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IFingerprintableState
    {
        Fingerprint Fingerprint { get; }
        ProgramCounterTuple ProgramCounters();
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class TraversalInfo : IFingerprintableState
    {
        public ProgramCounterTuple ProgramCounters()
        {
            ProgramCounter[] pcs = new ProgramCounter[stateImpl.NumProcesses];
            for (int i = 0; i < stateImpl.NumProcesses; i++)
            {
                ZingMethod topOfStack = stateImpl.GetProcess(i).TopOfStack;

                if (topOfStack == null)
                    pcs[i] = new ProgramCounter();
                else
                    pcs[i] = new ProgramCounter(topOfStack);
            }
            return new ProgramCounterTuple(pcs);
        }

        public static System.Random randGen;
        protected const ProcessStatus RUNNABLE = ProcessStatus.Runnable;
        internal Receipt receipt;
        public StateType stateType;
        protected StateImpl stateImpl;
        public readonly Via Via;
        public TraversalInfo Predecessor;
        internal TraversalInfo successor;
        public ZingBounds Bounds;

        private bool magicBit = false;

        public bool MagicBit
        {
            get { return magicBit; }
            set { magicBit = value; }
        }
        public bool IsAcceptingState = false;

        protected bool doDelay;
        

        public IZingDelayingScheduler ZingDBScheduler;
        public IZingSchedulerState ZingDBSchedState;
        public Int16 numOfTimesCurrStateDelayed = 0;

        protected ZingEvent[] events;
        protected ExternalEvent[] externalEvents;
        protected Exception exception;
        public bool IsFingerPrinted = false;
        public bool IsFrontier = false;
        protected bool hasMultipleSuccessors;

        public bool HasMultipleSuccessors
        {
            get { return hasMultipleSuccessors; }
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public enum StateType : byte
        {
            ChooseState, ExecutionState, TerminalState
        }

        public Trace GenerateNonCompactTrace()
        {
            Trace t = new Trace();

            for (TraversalInfo ti = this; ti.Predecessor != null; ti = ti.Predecessor)
            {

                ViaChoose vc = ti.Via as ViaChoose;
                ViaExecute ve = ti.Via as ViaExecute;

                if (vc != null)
                    t.InsertStep(new TraceStep(false, vc.ChoiceNumber));
                else if (ve != null)
                    t.InsertStep(new TraceStep(true, ve.ProcessExecuted));
                else
                    throw new NotImplementedException("Summaries not yet supported in traces");
            }

            return t;
        }


        public TraversalInfo SetMagicbit ()
        {
            var currStateImp = this.reclaimState();
            //set the magic bit
            var newTi = MakeTraversalInfo(currStateImp, Predecessor, Via);
            newTi.MagicBit = true;
            return newTi;
        }

        public TraversalInfo ThrowStackOverFlowException ()
        {
            var S = this.reclaimState();
            //throw exception
            S.ThrowStackOverFlowException();

            var newTI = MakeTraversalInfo(S, Predecessor, Via);
            return newTI;
        }

        public Trace GenerateTrace()
        {
            Trace t = new Trace();

            for (TraversalInfo ti = this; ti.Predecessor != null; ti = ti.Predecessor)
            {
                if (Options.CompactTraces && !ti.Predecessor.HasMultipleSuccessors)
                {
                    // Its obvious how we got here, don't waste space
                    // storing this trace step
                    continue;
                }
                ViaChoose vc = ti.Via as ViaChoose;
                ViaExecute ve = ti.Via as ViaExecute;

                if (vc != null)
                    t.InsertStep(new TraceStep(false, vc.ChoiceNumber));
                else if (ve != null)
                    t.InsertStep(new TraceStep(true, ve.ProcessExecuted));
                else
                    throw new NotImplementedException("Summaries not yet supported in traces");
            }

            return t;
        }

        //precondition: parent needs to be a parent of the current traversal info
        public Trace Frontier_GenerateTraceToParent(TraversalInfo parent)
        {

            Trace t = new Trace();

            for (TraversalInfo ti = this; !ti.Equals(parent); ti = ti.Predecessor)
            {

                if (Options.CompactTraces && !ti.Predecessor.HasMultipleSuccessors)
                {
                    // Its obvious how we got here, don't waste space
                    // storing this trace step
                    continue;
                }

                ViaChoose vc = ti.Via as ViaChoose;
                ViaExecute ve = ti.Via as ViaExecute;

                if (vc != null)
                    t.InsertStep(new TraceStep(false, vc.ChoiceNumber));
                else if (ve != null)
                    t.InsertStep(new TraceStep(true, ve.ProcessExecuted));
                else
                    throw new NotImplementedException("Summaries not yet supported in traces");
            }
            return t;
        }

        public Trace GenerateTraceToParent(TraversalInfo parent)
        {

            Trace t = new Trace();
            parent.IsFingerPrinted = true;
            for (TraversalInfo ti = this; !(ti.IsFingerPrinted && ti.Fingerprint.Equals(parent.Fingerprint)) && ti.Predecessor != null; ti = ti.Predecessor)
            {

                if (Options.CompactTraces && !ti.Predecessor.HasMultipleSuccessors)
                {
                    // Its obvious how we got here, don't waste space
                    // storing this trace step
                    continue;
                }

                ViaChoose vc = ti.Via as ViaChoose;
                ViaExecute ve = ti.Via as ViaExecute;

                if (vc != null)
                    t.InsertStep(new TraceStep(false, vc.ChoiceNumber));
                else if (ve != null)
                    t.InsertStep(new TraceStep(true, ve.ProcessExecuted));
                else
                    throw new NotImplementedException("Summaries not yet supported in traces");
            }
            return t;
        }

        // Generates a trace up until a state of depth is reached
        public Trace GenerateTraceUptoDepth(int Depth)
        {

            Trace t = new Trace();

            for (TraversalInfo ti = this; ti.Bounds.Depth >= Depth; ti = ti.Predecessor)
            {
                if (Options.CompactTraces && !ti.Predecessor.HasMultipleSuccessors)
                {
                    // Its obvious how we got here, don't waste space
                    // storing this trace step
                    continue;
                }
                ViaChoose vc = ti.Via as ViaChoose;
                ViaExecute ve = ti.Via as ViaExecute;

                if (vc != null)
                    t.InsertStep(new TraceStep(false, vc.ChoiceNumber));
                else if (ve != null)
                    t.InsertStep(new TraceStep(true, ve.ProcessExecuted));
                else
                    throw new NotImplementedException("Summaries not yet supported in traces");
            }

            return t;
        }

        public CheckerResult ErrorCode { get { return stateImpl.ErrorCode; } }

        public Exception Exception
        {
            get { return exception; }
        }
        public bool IsErroneousTI
        {
            get { return Exception != null && !(Exception is ZingAssumeFailureException); }
        }

        public bool IsFailedAssumptionTI
        {
            get { return Exception != null && Exception is ZingAssumeFailureException; }
        }

        public CheckerResult CheckerResult
        {
            get
            {
                if (!this.IsErroneousTI)
                    return CheckerResult.Success;

                Exception e = this.Exception;

                if (e is ZingAssertionFailureException)
                    return CheckerResult.Assertion;
                else if (e is ZingInvalidEndStateException)
                    return CheckerResult.Deadlock;
                else if (e is ZingUnexpectedFailureException)
                    return CheckerResult.ZingRuntimeError;
                else
                    return CheckerResult.ModelRuntimeError;
            }
        }

#if UNREACHABLE
        public Via[] GenerateVia()
        {
            Console.WriteLine(" Generating error trace depth = {0}", Depth);
            
            Via[] result = new Via[Depth];
            TraversalInfo ti = this;
            
            while (ti.Predecessor != null) 
            {
                result[ti.Depth - 1] = ti.Via;
                ti = ti.Predecessor;
            }
            return result;
        }

        public ZingEvent[] Events { get { return events; } }
#endif
        public ExternalEvent[] ExternalEvents { get { return externalEvents; } }

        public readonly int NumProcesses;
        public Fingerprint fingerprint;
        public Fingerprint Fingerprint
        {
            get
            {
                if (fingerprint == null)
                {
                    // First check if this state is "in favor" by ensuring that our successor state is null
                    // is yes, then compute the fingerprint and return it. Else throw an exception
                    if (successor == null)
                    {
                        fingerprint = stateImpl.Fingerprint;
                    }
                    else
                    {
                        throw new ZingUnsupportedFeatureException("Attempt to access the fingerprint of a single " +
                            "transition state while fingerprinting of single transition states has been disabled " +
                            "with the -co option");
                    }
                }
                return fingerprint;
            }
        }

        public readonly ProcessInfo[] ProcessInfo;

        private string[] sources;
#if UNREACHABLE
        public string[] Sources 
        {
            get 
            {
                TraversalInfo ti = this;

                while (ti.Predecessor != null)
                    ti = ti.Predecessor;

                return ti.sources;
            }
        }
#endif

        private string[] sourceFiles;
#if UNREACHABLE
        public string[] SourceFiles
        {
            get 
            {
                TraversalInfo ti = this;

                while (ti.Predecessor != null)
                    ti = ti.Predecessor;

                return ti.sourceFiles;
            }
        }
#endif
        protected TraversalInfo(StateImpl s, StateType st,
            TraversalInfo pred, Via bt)
        {
            
            stateType = st;
            if (pred != null)
            {
                Predecessor = pred;
                Bounds = new ZingBounds(pred.Bounds.Depth + 1, pred.Bounds.Delay);
                doDelay = false;
                if (Options.IsSchedulerDecl)
                {
                    ZingDBSchedState = s.ZingDBSchedState;
                    ZingDBScheduler =  s.ZingDBScheduler;
                    
                }
                pred.successor = this;
                MagicBit = pred.MagicBit;
            }
            else if (Options.IsSchedulerDecl)
            {
                ZingDBSchedState = s.ZingDBSchedState.Clone();
                ZingDBScheduler = s.ZingDBScheduler;
                
            }
            // Initialize the number of delays to 0
            numOfTimesCurrStateDelayed = 0;
            Via = bt;
            if (pred == null)
            {
                Bounds = new ZingBounds();
                sources = s.GetSources();
                sourceFiles = s.GetSourceFiles();
                MagicBit = false;
            }

            NumProcesses = s.NumProcesses;
            /*
             * @Abhishek: Fingerprinting before checkin seems to be flaky
             * so all fingerprinting is done in the derived classes viz: ChooseState,
             * ExecutionState and TerminalState
             */

#if false
            if (Options.FingerprintSingleTransitionStates)
            {
                if (this is ChooseState)
                {
                    if (s.NumChoices > 1)
                    {
                        this.fingerprint = s.Fingerprint;
                    }
                    else
                    {
                        this.fingerprint = null;
                    }
                }
                else if (this is ExecutionState)
                {
                    if (s.NumProcesses > 1)
                    {
                        this.fingerprint = s.Fingerprint;
                    }
                    else
                    {
                        this.fingerprint = null;
                    }
                }
                else
                {
                    // TerminalState
                    this.fingerprint = null;
                }
            }
            else
            {
                this.fingerprint = s.Fingerprint;
            }      
#endif
            ProcessInfo = s.GetProcessInfo();
            events = s.GetEvents();
            externalEvents = s.GetExternalEvents();
            exception = s.Exception;
            IsAcceptingState = s.IsAcceptingState;
            
            if(Options.IsRandomSearch)
            {
                randGen = new Random(DateTime.Now.Millisecond);
            }
        }

#if UNREACHABLE
        public static TraversalInfo Load(string filename)
        {
            StateImpl initialState = StateImpl.Load(filename);
            TraversalInfo ti;

            ti = new ExecutionState(initialState, null, null);
            if (ti == null)
                throw new ArgumentException("initial state must be a normal execution node");
            return ti;
        }
#endif

        public static TraversalInfo Load(Assembly asm)
        {
            StateImpl initialState = StateImpl.Load(asm);
            TraversalInfo ti;

            ti = new ExecutionState(initialState, null, null);
            if (ti == null)
                throw new ArgumentException("initial state must be a normal execution node");
            return ti;
        }

        protected static TraversalInfo
            MakeTraversalInfo(StateImpl s, TraversalInfo pred, Via bt)
        {
            if (s.IsTerminalState)
                return new TerminalState(s, pred, bt);
            if (s.IsChoicePending)
                return new ChooseState(s, pred, bt);
            if (s.IsNormalState)
                return new ExecutionState(s, pred, bt);

            Debug.Fail("unexpected state type");
            return null;
        }

        protected static TraversalInfo MakeTraversalInfo(StateImpl s, TraversalInfo pred,
            Via bt, bool MustFingerprint)
        {
            if (s.IsTerminalState)
                return new TerminalState(s, pred, bt, MustFingerprint);
            if (s.IsChoicePending)
                return new ChooseState(s, pred, bt, MustFingerprint);
            if (s.IsNormalState)
                return new ExecutionState(s, pred, bt, MustFingerprint);

            Debug.Fail("unexpected state type");
            return null;
        }

        /// <summary>
        /// Used to obtain a traversalinfo when the entire state is saved 
        /// at a depth cut off
        /// </summary>
        /// <param name="s"> The StateImpl object of the checkpointed state</param>
        /// <returns></returns>
        public static TraversalInfo MakeTraversalInfo(StateImpl s)
        {
            TraversalInfo retval = null;
            if (s.IsTerminalState)
            {
                retval = new TerminalState(s, null, null, true);
            }
            else if (s.IsChoicePending)
            {
                retval = new ChooseState(s, null, null, true);
            }
            else
            {
                // Normal state (ExecutionState)
                retval = new ExecutionState(s, null, null, true);
            }
            return (retval);
        }

        /// <summary>
        /// A Clone method
        /// </summary>
        /// <returns></returns>
        public TraversalInfo Clone(int SerialNum)
        {
            StateImpl ClonedStateImpl = (StateImpl)this.stateImpl.Clone(SerialNum);
            return (MakeTraversalInfo(ClonedStateImpl));
        }

        public TraversalInfo Clone()
        {
            StateImpl ClonedStateImpl = (StateImpl)this.stateImpl.Clone();
            return (MakeTraversalInfo(ClonedStateImpl));
        }

        protected abstract void Replay(TraversalInfo succ, Via bt);
        internal abstract void deOrphanize(StateImpl s);
        public abstract TraversalInfo GetNextSuccessor(ZingBoundedSearch zbs);

        public abstract void Reset();

        public void DiscardStateImpl()
        {
            stateImpl = null;
        }

        public StateImpl reclaimState()
        {
            // test if we are orphanized, if so we (recursively)
            // rollback to the most recent ancestor that is still "in
            // favor" and replay from there
            if (Predecessor != null && Predecessor.successor != this)
                Predecessor.Replay(this, Via);

            Debug.Assert(receipt != null);
            stateImpl.Rollback(receipt);
            if (Options.IsSchedulerDecl)
            {
                stateImpl.ZingDBSchedState = ZingDBSchedState.Clone();
            }

            // let's get ready for a new child by orphanizing all
            // current descendents
            for (TraversalInfo ti = successor; ti != null; ti = ti.successor)
                ti.Predecessor.successor = null;

            //if (this.fingerprint != null)
            //{
            //    Debug.Assert(this.fingerprint.Equals(stateImpl.Fingerprint));
            //}
            stateImpl.IsAcceptingState = false;

            return stateImpl;
        }

        public abstract ushort NumSuccessors();
        public abstract TraversalInfo GetSuccessorN(int n);

        public abstract TraversalInfo GetSuccessorNForReplay(int n, bool MustFingerprint);

        //#if UNUSED_CODE
        public StateImpl GetStateImpl()
        {
            StateImpl s = reclaimState();
            StateImpl res = (StateImpl)s.Clone();
            receipt = s.CheckIn();
            return res;
        }
        //#endif

        private string DumpEvents()
        {
            StringBuilder sb = new StringBuilder();
            if (this.events != null && this.events.Length > 0)
            {
                sb.Append("  Events:\r\n");
                foreach (ZingEvent e in this.events)
                    sb.AppendFormat("    {0}", e);

                sb.Append("\r\n");
            }

            if (this.externalEvents != null && this.externalEvents.Length > 0)
            {
                sb.Append("  External Events:\r\n");
                sb.Append("    ");
                foreach (ExternalEvent e in this.externalEvents)
                    sb.Append(e.ToString());

                sb.Append("\r\n\r\n");
            }
            return sb.ToString();

        }

        public bool IsInvalidEndState()
        {
            StateImpl s = reclaimState();
            if (s.IsInvalidEndState())
            {
                s.Exception = new ZingInvalidEndStateException();
                this.exception = s.Exception;
                return true;
            }
            else
                return false;
        }

        public override string ToString()
        {
            //TODO: I tried this first with GetStateImpl and the clone was not
            // copying the heap properly. Need to investigate StateImpl cloning

            StateImpl s = reclaimState();
            if (s != null)
            {
                string str = s.ToString();
                str = str + DumpEvents();
                receipt = s.CheckIn();
                return str;
            }
            else
                return "s == null";
        }

        public override bool Equals(object obj)
        {
            return TraversalInfo.Equals(this, obj); // delegate to the static method
        }

        static new public bool Equals(object obj1, object obj2)
        {
            TraversalInfo o1 = obj1 as TraversalInfo;
            TraversalInfo o2 = obj2 as TraversalInfo;


            // Do the compare and only if both states are fingerprinted
            if (!o1.IsFrontier || !o2.IsFrontier)
            {
                return false;
            }
            return (o1.Fingerprint.Equals(o2.Fingerprint));
        }

        public override int GetHashCode()
        {
            return reclaimState().GetHashCode();
        }
    }

}
