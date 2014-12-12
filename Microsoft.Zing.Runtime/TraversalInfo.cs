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

    
    public interface IFingerprintableState
    {
        Fingerprint Fingerprint { get; }
        ProgramCounterTuple ProgramCounters();
    }

    
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
        protected const ProcessStatus RUNNABLE = ProcessStatus.Runnable;
        internal Receipt receipt;

        /// <summary>
        /// Type of Current State
        /// </summary>
        public StateType stateType;

        /// <summary>
        /// Current State Impl
        /// </summary>
        protected StateImpl stateImpl;
        
        /// <summary>
        /// Used to store the transition used to enter the current state
        /// </summary>
        public readonly Via Via;
        
        /// <summary>
        /// Predecessor and Successor information for replaying the stack trace from initial state
        /// </summary>
        public TraversalInfo Predecessor;
        internal TraversalInfo Successor;
        
       
        /// <summary>
        /// Search bounds at the current state
        /// </summary>
        public ZingerBounds zBounds;

        /// <summary>
        /// Depth of the current state from the initial state
        /// </summary>
        public long CurrentDepth = 0;

        /// <summary>
        /// Magic bit used during NDFS exploration 
        ///
        /// </summary>
        private bool magicBit = false;
        public bool MagicBit
        {
            get { return magicBit; }
            set { magicBit = value; }
        }

        /// <summary>
        /// Is the current state an accepting state, the but is set after executing the accepting transition
        /// </summary>
        public bool IsAcceptingState = false;

        /// <summary>
        /// Should we delay or explore the deterministic schedule.
        /// When true, the schedule is delayed.
        /// </summary>
        protected bool doDelay;
        
        /// <summary>
        /// The delaying scheduler Info for the current state.
        /// </summary>
        public ZingerDelayingScheduler ZingDBScheduler;
        public ZingerSchedulerState ZingDBSchedState;

        protected ZingEvent[] events;
        protected Exception exception;
       
        /// <summary>
        /// Is the current state fingerprinted, state may not be fingerprinted if it has single successor
        /// </summary>
        public bool IsFingerPrinted = false;
        
        /// <summary>
        /// If the state has multiple successors
        /// </summary>
        protected bool hasMultipleSuccessors;
        public bool HasMultipleSuccessors
        {
            get { return hasMultipleSuccessors; }
        }

        
        public enum StateType : byte
        {
            ChooseState, ExecutionState, TerminalState
        }

        /// <summary>
        /// Generate Noncompact trace for Error Reporting
        /// </summary>
        /// <returns></returns>
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

        /// <summary>
        /// Set the magic bit for NDFS
        /// </summary>
        /// <returns></returns>
        public TraversalInfo SetMagicbit ()
        {
            var currStateImp = this.reclaimState();
            //set the magic bit
            var newTi = MakeTraversalInfo(currStateImp, Predecessor, Via);
            newTi.MagicBit = true;
            return newTi;
        }

        public Trace GenerateTrace()
        {
            Trace t = new Trace();

            for (TraversalInfo ti = this; ti.Predecessor != null; ti = ti.Predecessor)
            {
                if (ZingerConfiguration.CompactTraces && !ti.Predecessor.HasMultipleSuccessors)
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

        public ZingerResult ErrorCode { get { return stateImpl.ErrorCode; } }

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

        /// <summary>
        /// Total number of processes (blocked or enabled) 
        /// </summary>
        public readonly int NumProcesses;

        /// <summary>
        /// Fingerprint of the current state
        /// </summary>
        public Fingerprint fingerprint;
        public Fingerprint Fingerprint
        {
            get
            {
                if (fingerprint == null)
                {
                    // First check if this state is "in favor" by ensuring that our successor state is null
                    // is yes, then compute the fingerprint and return it. Else throw an exception
                    if (Successor == null)
                    {
                        fingerprint = stateImpl.Fingerprint;
                    }
                    else
                    {
                        throw new ZingUnhandledExceptionException("Attempt to access the fingerprint of a single " +
                            "transition state while fingerprinting of single transition states has been disabled " +
                            "with the -co option");
                    }
                }
                return fingerprint;
            }
        }


        public readonly ProcessInfo[] ProcessInfo;

        protected TraversalInfo(StateImpl s, StateType st,
            TraversalInfo pred, Via bt)
        {

            stateType = st;
            if (pred != null)
            {
                Predecessor = pred;
                CurrentDepth = pred.CurrentDepth + 1;
                zBounds = new ZingerBounds(pred.zBounds.ExecutionCost, pred.zBounds.ChoiceCost);
                zBounds.IncrementDepthCost();

                doDelay = false;
                if (ZingerConfiguration.DoDelayBounding)
                {
                    ZingDBSchedState = s.ZingDBSchedState;
                    ZingDBScheduler =  s.ZingDBScheduler;
                    
                }
                pred.Successor = this;
                MagicBit = pred.MagicBit;
            }
            else
            {
                zBounds = new ZingerBounds();
                MagicBit = false;
                CurrentDepth = 0;
                if (ZingerConfiguration.DoDelayBounding)
                {
                    ZingDBSchedState = s.ZingDBSchedState.Clone(false);
                    ZingDBScheduler = s.ZingDBScheduler;

                }
            }
            // Initialize the number of delays to 0
            Via = bt;
            
            NumProcesses = s.NumProcesses;
            ProcessInfo = s.GetProcessInfo();
            events = s.GetEvents();
            exception = s.Exception;
            IsAcceptingState = s.IsAcceptingState;

        }

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
        public abstract TraversalInfo GetNextSuccessor();

        #region RandomWalk
        public abstract TraversalInfo GetNextSuccessorUniformRandomly();
        public abstract TraversalInfo GetNextSuccessorUnderDelayZeroForRW();
        #endregion
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
            if (Predecessor != null && Predecessor.Successor != this)
                Predecessor.Replay(this, Via);

            Debug.Assert(receipt != null);
            stateImpl.Rollback(receipt);
            if (ZingerConfiguration.DoDelayBounding)
            {
                stateImpl.ZingDBSchedState = ZingDBSchedState.Clone(false);
            }

            // let's get ready for a new child by orphanizing all
            // current descendents
            for (TraversalInfo ti = Successor; ti != null; ti = ti.Successor)
                ti.Predecessor.Successor = null;

            //if (this.fingerprint != null)
            //{
            //    Debug.Assert(this.fingerprint.Equals(stateImpl.Fingerprint));
            //}
            if (!ZingerConfiguration.DoMaceliveness)
            {
                stateImpl.IsAcceptingState = false;
            }
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

            return (o1.Fingerprint.Equals(o2.Fingerprint));
        }

        public override int GetHashCode()
        {
            return reclaimState().GetHashCode();
        }
    }

}
