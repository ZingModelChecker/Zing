using System;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace Microsoft.Zing
{
    /// <exclude/>
    /// <summary>
    /// A process has a name, an entry point, input parameters, and a stack.
    /// The name is just a convenience here.
    /// </summary>
    [Serializable]
    [CLSCompliant(false)]
    public class Process
    {
        // <summary>
        // Constructor
        // </summary>
        // <param name="entryPoint"></param>
        // <param name="name"></param>
        // <param name="id"></param>
        public Process(StateImpl stateObj, ZingMethod entryPoint, string name, uint id)
        {
            this.StateImpl = stateObj;
            this.entryPoint = entryPoint;
            this.name = name;
            this.id = id;

            this.Call(entryPoint);
        }

        public Process(StateImpl stateObj, ZingMethod entryPoint, string name, uint id, int threadID)
            : this(stateObj, entryPoint, name, id)
        {
            this.MyThreadId = threadID;
        }

        // <summary>
        // Private constructor for cloning only
        // </summary>
        private Process(StateImpl stateObj, string myName, uint myId)
        {
            StateImpl = stateObj;
            name = myName;
            id = myId;
        }

        /// <summary>
        /// Check if the vaiable is declared in this process
        /// </summary>
        /// <param name="variableName"></param>
        /// <returns></returns>
        public bool ContainsVariable(string variableName)
        {
            if (topOfStack == null)
                return false;

            return topOfStack.ContainsVariable(variableName);
        }

        public object LookupValueByName(string variableName)
        {
            if (topOfStack == null)
                return null;

            return topOfStack.LookupValueByName(variableName);
        }

        /// <summary>
        /// Field to store the last zing process executed by each thread during parallel exploration
        /// </summary>
        private static Process[] lastProcess = new Process[ZingerConfiguration.DegreeOfParallelism];

        public static Process[] LastProcess
        {
            get { return lastProcess; }
        }

        /// <summary>
        /// Field to store the assertion failure context
        /// </summary>
        public static ZingSourceContext[] AssertionFailureCtx = new ZingSourceContext[ZingerConfiguration.DegreeOfParallelism];

        /// <summary>
        /// Field to store the
        /// </summary>
        private static Process[] currentProcess = new Process[ZingerConfiguration.DegreeOfParallelism];

        public static Process CurrentProcess
        {
            get { return currentProcess[0]; }
            set { currentProcess[0] = value; }
        }

        public static Process GetCurrentProcess(int threadId)
        {
            return currentProcess[threadId];
        }

        public static void ClearCurrentProcesses()
        {
            for (int i = 0; i < ZingerConfiguration.DegreeOfParallelism; i++)
            {
                lastProcess[i] = null;
                currentProcess[i] = null;
            }
        }

        [NonSerialized]
        internal readonly StateImpl StateImpl;

        // <summary>
        // The friendly name of the process.
        // </summary>
        private string name;

        public string Name { get { return name; } }

        // <summary>
        // Identifier of the process.
        // </summary>
        private uint id;

        public uint Id { get { return id; } }

        // <summary>
        // The initial point of execution for the process.
        // </summary>
        // <remarks>
        // This marks the base of the stack and may be useful for fast
        // comparisons between processes since the entry points will often
        // be unique. It also helps to identify the process for debugging
        // and tracing purposes.
        // </remarks>
        [NonSerialized]
        private ZingMethod entryPoint;

        public ZingMethod EntryPoint
        {
            get { return entryPoint; }
            set { entryPoint = value; }
        }

        public enum Status
        {
            Runnable,       // runnable, but in a "stable" state
            Blocked,        // blocked in "receive" (or select)
            Completed,      // process has terminated
        };

        // atomicity level records the current dynamic depth of
        // nested atomic blocks we have entered
        private int atomicityLevel;

        private bool middleOfTransition;

        public int AtomicityLevel
        {
            get { return atomicityLevel; }
            set { atomicityLevel = value; }
        }

        public bool MiddleOfTransition
        {
            get { return middleOfTransition; }
            set { middleOfTransition = value; }
        }

        private bool backTransitionEncountered;

        public bool BackTransitionEncountered
        {
            get { return backTransitionEncountered; }
            set { backTransitionEncountered = value; }
        }

        // IsPreemptible tells RunProcess whether interleaving is allowed
        public bool IsPreemptible
        {
            get { return ((atomicityLevel == 0) && !middleOfTransition); }
        }

        internal bool choicePending;

        public ProcessStatus CurrentStatus
        {
            get
            {
                ProcessStatus returnValue;
                try
                {
                    if (this.topOfStack == null)
                        returnValue = ProcessStatus.Completed;
                    else if (this.topOfStack.IsRunnable(this))
                        returnValue = ProcessStatus.Runnable;
                    else if (this.topOfStack.ValidEndState)
                        returnValue = ProcessStatus.BlockedInEndState;
                    else
                        returnValue = ProcessStatus.Blocked;
                    return returnValue;
                }
                catch (ZingException)
                {
                    // A process whose join conditions throw an exception is
                    // runnable until we actually run the process and let it
                    // throw the exception.
                    return ProcessStatus.Runnable;
                }
            }
        }

        public ZingSourceContext Context
        {
            get
            {
                if (this.topOfStack == null)
                    return null;

                return this.topOfStack.Context;
            }
        }

        public ZingAttribute ContextAttribute
        {
            get
            {
                if (this.topOfStack == null)
                    return null;

                ZingAttribute context = this.topOfStack.ContextAttribute;

                if (context != null)
                    return context;

                this.topOfStack.IsRunnable(this);
                return null;
            }
        }

        public string ProgramCounter
        {
            get
            {
                if (topOfStack == null)
                    return String.Empty;

                return topOfStack.ProgramCounter;
            }
        }

        public string MethodName
        {
            get
            {
                if (topOfStack == null)
                    return String.Empty;

                return topOfStack.MethodName;
            }
        }

        [NonSerialized]
        private ZingMethod topOfStack;

        public ZingMethod TopOfStack { get { return topOfStack; } }

        [NonSerialized]
        private ZingMethod savedTopOfStack;

        private void doPush(ZingMethod method)
        {
            method.Caller = topOfStack;
            topOfStack = method;
            if (stackULEs == null)
                return;
            stackULEs.Push(new UndoPush(this));
        }

        private ZingMethod doPop()
        {
            if (stackULEs != null)
            {
                if (stackULEs.Count > 0 && stackULEs.Peek() is UndoPush)
                    stackULEs.Pop();
                else
                {
                    Debug.Assert(topOfStack == savedTopOfStack);
                    stackULEs.Push(new UndoPop(this, topOfStack));
                    savedTopOfStack = topOfStack.Caller;
                }
            }
            ZingMethod oldTop = topOfStack;
            topOfStack = topOfStack.Caller;
            return oldTop;
        }

        public void Call(ZingMethod method)
        {
            // method.StateImpl = this.StateImpl;
            method.SavedAtomicityLevel = this.atomicityLevel;
            doPush(method);
        }

        public void Return(ZingSourceContext context, ZingAttribute contextAttribute)
        {
            ZingMethod returningMethod = doPop();

            this.atomicityLevel = returningMethod.SavedAtomicityLevel;

            // Keep a ref to the completed function so the caller can access
            // the return value and output parameters.

            if (topOfStack != null)
                lastFunctionCompleted = returningMethod;
            else
            {
                lastFunctionCompleted = null;
                middleOfTransition = false;
            }

            if (this.topOfStack == null && ZingerConfiguration.ExecuteTraceStatements && (this.name != null && this.name.Length != 0))
            {
                if (ZingerConfiguration.DegreeOfParallelism == 1)
                {
                    this.StateImpl.ReportEvent(new TerminateProcessEvent(context, contextAttribute));
                }
                else
                {
                    this.StateImpl.ReportEvent(new TerminateProcessEvent(context, contextAttribute, this.MyThreadId));
                }
            }
        }

        #region some predicate nonsense

        private static bool[] runningPredicateMethod = new bool[ZingerConfiguration.DegreeOfParallelism];

        internal static bool[] RunningPredicateMethod
        {
            get { return runningPredicateMethod; }
        }

        public class PredicateContextIndexer
        {
            public ZingAttribute[] predicateContext;

            public PredicateContextIndexer(int num)
            {
                this.predicateContext = new ZingAttribute[num];
            }

            public ZingAttribute this[int index]
            {
                get { return this.predicateContext[index]; }
                set { this.predicateContext[index] = value; }
            }
        }

        public static PredicateContextIndexer PredicateContext = new PredicateContextIndexer(ZingerConfiguration.DegreeOfParallelism);

        /*
        public static ZingAttribute PredicateContext
        {
            get { return predicateContext; }
            set { predicateContext = value; }
        }
        */

        public bool CallPredicateMethod(ZingMethod predicateMethod)
        {
            if (runningPredicateMethod[MyThreadId])
            {
                Debugger.Break();
                throw new Exception("Predicate !");
            }

            Process dummyProc = new Process(this.StateImpl, predicateMethod, string.Empty, 0);

            Exception savedException = this.StateImpl.Exception;
            this.StateImpl.Exception = null;

            while (dummyProc.TopOfStack != null)
            {
                runningPredicateMethod[MyThreadId] = true;
                this.StateImpl.RunBlocks(dummyProc);
                runningPredicateMethod[MyThreadId] = false;

                if (this.StateImpl.Exception != null)
                {
                    if (savedException == null)
                    {
                        this.StateImpl.Exception = new Exception("Predicate");
                        throw this.StateImpl.Exception;
                    }
                    else
                    {
                        // If we already have a pending exception on the state, just
                        // return false and restore the original exception.
                        this.StateImpl.Exception = savedException;
                        return false;
                    }
                }

                if (dummyProc.choicePending)
                    throw new Exception("Predicate");
            }
            this.StateImpl.Exception = savedException;
            return predicateMethod.BooleanReturnValue;
        }

        #endregion some predicate nonsense

        //
        // Find a stack frame capable of handling the exception, peeling off
        // stack frames as necessary to find someone. If nobody has a handler
        // in place, then report an unhandled exception as a Zing exception.
        //
        [SuppressMessage("Microsoft.Design", "CA1030:UseEventsWhereAppropriate")]
        public void RaiseZingException(int exception)
        {
            this.lastFunctionCompleted = null;

            doPop();

            while (topOfStack != null)
            {
                this.atomicityLevel = this.topOfStack.SavedAtomicityLevel;
                // If we find a handler, we're done
                if (this.topOfStack.RaiseZingException(exception))
                    return;

                doPop();
            }

            throw new ZingUnhandledExceptionException(exception);
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        public void RunNextBlock()
        {
            try
            {
                if (ZingerConfiguration.ExecuteTraceStatements)
                    Process.currentProcess[MyThreadId] = this;
                // Save the source context. This seems to be off by
                // one block if a ZingAssertionFailureException occurs
                if (Process.AssertionFailureCtx[MyThreadId] == null)
                {
                    Process.AssertionFailureCtx[MyThreadId] = new ZingSourceContext();
                }

                topOfStack.Context.CopyTo(Process.AssertionFailureCtx[MyThreadId]);
                Process.LastProcess[MyThreadId] = this;
                topOfStack.Dispatch(this);
            }
            catch (ZingException e)
            {
                this.StateImpl.Exception = e;
                (this.StateImpl.Exception as ZingException).myThreadId = MyThreadId;
            }
            catch (DivideByZeroException)
            {
                this.StateImpl.Exception = new ZingDivideByZeroException();
                (this.StateImpl.Exception as ZingException).myThreadId = MyThreadId;
            }
            catch (OverflowException)
            {
                this.StateImpl.Exception = new ZingOverflowException();
                (this.StateImpl.Exception as ZingException).myThreadId = MyThreadId;
            }
            catch (IndexOutOfRangeException)
            {
                this.StateImpl.Exception = new ZingIndexOutOfRangeException();
                (this.StateImpl.Exception as ZingException).myThreadId = MyThreadId;
            }
            catch (Exception e)
            {
                if (Debugger.IsAttached)
                    Debugger.Break();

                this.StateImpl.Exception =
                    new ZingUnexpectedFailureException("Unhandled exception in the Zing runtime", e);
                (this.StateImpl.Exception as ZingException).myThreadId = MyThreadId;
            }
            finally
            {
                // Add check for other ZingExceptions which are not thrown and append the serial number
                if (this.StateImpl.Exception != null && (this.StateImpl.Exception is ZingException))
                {
                    (this.StateImpl.Exception as ZingException).myThreadId = MyThreadId;
                }
                Process.currentProcess[MyThreadId] = null;
            }
        }

        [NonSerialized]
        private ZingMethod lastFunctionCompleted;

        //the following is used during application of effects
        public void UpdateDirectLastFunctionCompleted(ZingMethod method)
        {
            lastFunctionCompleted = method;
        }

        public ZingMethod LastFunctionCompleted
        {
            get { return lastFunctionCompleted; }
            set
            {
                Debug.Assert(value == null);
                if ((stackULEs != null) && (stackULEs.Count == 0))
                {
                    stackULEs.Push(new UndoResetLastFunctionCompleted(this, lastFunctionCompleted));
                }
                //value has to be null here!
                lastFunctionCompleted = value;
            }
        }

        /// <summary>
        /// Field indicating the thread Id when used during parallel exploration
        /// </summary>
        private int myThreadId = 0;

        public int MyThreadId
        {
            get { return myThreadId; }
            set { myThreadId = value; }
        }

        internal object Clone(StateImpl myState, bool shallowCopy)
        {
            Process clone = new Process(myState, Name, Id);

            clone.atomicityLevel = this.atomicityLevel;
            clone.middleOfTransition = this.middleOfTransition;
            clone.backTransitionEncountered = this.backTransitionEncountered;
            clone.choicePending = this.choicePending;
            // For parallel exploration
            clone.myThreadId = myState.MySerialNum;

            // Recursively clone the entire stack
            if (this.topOfStack != null)
                clone.topOfStack = this.topOfStack.Clone(myState, clone, shallowCopy);

            if (this.lastFunctionCompleted != null)
            {
                Debug.Fail("cannot happen anymore (xyc)");
                clone.lastFunctionCompleted = this.lastFunctionCompleted.Clone(myState, clone, true);
            }

            return clone;
        }

        #region Process delta data structures

        private class ProcessULE
        {
            // cloned components
            public int atomicityLevel;

            public bool middleOfTransition;
            public bool choicePending;

            // the following field added because of summarization
            public ZingMethod lastFunctionCompleted;

            // undoable components
            public Stack stackULEs;
        }

        private abstract class StackULE
        {
            protected Process process;

            protected StackULE(Process p)
            {
                process = p;
            }

            public void Undo()
            {
                doUndo();
            }

            protected abstract void doUndo();
        }

        private class UndoPush : StackULE
        {
            public UndoPush(Process p)
                : base(p) { }

            protected override void doUndo()
            {
                // to undo a push, we pop
                process.topOfStack = process.topOfStack.Caller;
            }
        }

        private class UndoPop : StackULE
        {
            private ZingMethod savedStackFrame;

            public UndoPop(Process p, ZingMethod theFrame)
                : base(p)
            {
                savedStackFrame = theFrame;
            }

            protected override void doUndo()
            {
                savedStackFrame.Caller = process.topOfStack;
                process.topOfStack = process.savedTopOfStack = savedStackFrame;
                savedStackFrame = null;
                process.topOfStack.DoRevert();
            }
        }

        private class UndoResetLastFunctionCompleted : StackULE
        {
            private ZingMethod savedLastFunctionCompleted;

            public UndoResetLastFunctionCompleted(Process p, ZingMethod l)
                : base(p)
            {
                savedLastFunctionCompleted = l;
            }

            protected override void doUndo()
            {
                process.lastFunctionCompleted = savedLastFunctionCompleted;
            }
        }

        private class UndoUpdate : StackULE
        {
            private object zingMethodULE;

            public UndoUpdate(Process p, object ule)
                : base(p)
            {
                zingMethodULE = ule;
            }

            protected override void doUndo()
            {
                object[] ules = new object[] { zingMethodULE };
                process.topOfStack.DoRollback(ules);
                zingMethodULE = null;
            }
        }

        private Stack stackULEs;

        #endregion Process delta data structures

        #region Private process delta methods

        private Stack checkInStackFrames()
        {
            // this is the first time we checked in
            if (stackULEs == null)
            {
                stackULEs = new Stack();
                for (savedTopOfStack = topOfStack;
                     savedTopOfStack != null;
                     savedTopOfStack = savedTopOfStack.Caller)
                    savedTopOfStack.DoCheckIn();

                savedTopOfStack = topOfStack;
                return null;
            }

            object zmULE = null;

            if (savedTopOfStack != null)
                zmULE = savedTopOfStack.DoCheckIn();

            // small optimization when no changes was made in the
            // current transition
            if (stackULEs.Count == 0 && zmULE == null)
            {
                Debug.Assert(savedTopOfStack == topOfStack);
                return null;
            }

            // the result
            Stack resStack = stackULEs;

            stackULEs = new Stack();

            ZingMethod stackFrame = topOfStack;

            // move newly pushed frames away from the result, save
            // them temporarily in stackULEs; while doing that, we
            // checkIn every newly pushed node
            while (resStack.Count > 0 && resStack.Peek() is UndoPush)
            {
                //object sfULE =
                // this would be the first time we check in these
                // freshly pushed nodes. so we discard their undo log
                // entries
                stackFrame.DoCheckIn();
                stackULEs.Push(resStack.Pop());
                stackFrame = stackFrame.Caller;
            }

            // everything below should be UndoPop's or UndoResetLastFunctionCompleted,
            // and if anything is to be saved, it should be right there at stackFrame
            Debug.Assert(resStack.Count == 0 || resStack.Peek() is UndoPop
                         || resStack.Peek() is UndoResetLastFunctionCompleted);
            Debug.Assert(savedTopOfStack == stackFrame);

            // insert zmULE between UndoPop objects and UndoPush
            // objects
            if (zmULE != null)
                resStack.Push(new UndoUpdate(this, zmULE));

            // move undoPush objects back into the result
            while (stackULEs.Count > 0)
                resStack.Push(stackULEs.Pop());

            savedTopOfStack = topOfStack;
            return resStack;
        }

        private void revertStackFrames()
        {
            Debug.Assert(stackULEs != null);

            if (savedTopOfStack != null)
                savedTopOfStack.DoRevert();
            while (stackULEs.Count > 0)
            {
                StackULE ule = (StackULE)stackULEs.Pop();
                ule.Undo();
            }
            Debug.Assert(savedTopOfStack == topOfStack);
        }

        private void rollbackStackFrames(Stack sules)
        {
            StackULE ule;

            Debug.Assert(stackULEs != null);
            Debug.Assert(stackULEs.Count == 0);

            if (sules == null)
            {
                savedTopOfStack = topOfStack;
                return;
            }
            while (sules.Count > 0)
            {
                ule = (StackULE)sules.Pop();
                ule.Undo();
            }
            savedTopOfStack = topOfStack;
        }

        #endregion Private process delta methods

        #region Public process delta methods

        public object DoCheckIn()
        {
            ProcessULE pULE = new ProcessULE();

            // cloned components
            pULE.atomicityLevel = atomicityLevel;
            pULE.middleOfTransition = middleOfTransition;
            pULE.choicePending = choicePending;
            pULE.lastFunctionCompleted = null;
            if (lastFunctionCompleted != null)
            {
                pULE.lastFunctionCompleted = lastFunctionCompleted.Clone(StateImpl, this, false);
            }

            // undoable ones
            pULE.stackULEs = checkInStackFrames();
            return pULE;
        }

        public void DoCheckout(object currentUle)
        {
            ProcessULE pULE = (ProcessULE)currentUle;

            // cloned components
            atomicityLevel = pULE.atomicityLevel;
            middleOfTransition = pULE.middleOfTransition;
            choicePending = pULE.choicePending;
            lastFunctionCompleted = pULE.lastFunctionCompleted;

            // undoable ones -- do nothing
        }

        public void DoRevert()
        {
            // cloned components -- do nothing

            // undoable ones
            revertStackFrames();
        }

        public void DoRollback(object[] uleList)
        {
            // cloned components -- do nothing

            // undoable ones
            int n = uleList.Length, i;

            for (i = 0; i < n; i++)
                rollbackStackFrames(((ProcessULE)uleList[i]).stackULEs);
        }

        #endregion Public process delta methods

        #region Fingerprinting

        private MemoryStream memStream;
        private BinaryWriter binWriter;

        /// <summary>
        ///  Compute the fingerprint of a process.
        ///      The current implementation computes this fingerprint nonincrementally.
        ///      But in the future this can be made incremental
        /// </summary>
        /// <param name="state"></param>
        /// <returns>Fingerprint of a process</returns>
        public Fingerprint ComputeFingerprint(StateImpl state)
        {
            if (memStream == null)
            {
                memStream = new MemoryStream();
                binWriter = new BinaryWriter(memStream);
            }
            binWriter.Seek(0, SeekOrigin.Begin);
            this.WriteString(state, binWriter);
            Fingerprint procPrint = StateImpl.FingerprintNonHeapBuffer(memStream.GetBuffer(), (int)memStream.Position);
            return procPrint;
            //return Fingerprint.ComputeFingerprint(memStream.GetBuffer(), (int) memStream.Position, 0);
        }

        internal void WriteString(StateImpl state, BinaryWriter bw)
        {
            for (ZingMethod m = topOfStack; m != null; m = m.Caller)
                m.WriteString(state, bw);

            if (lastFunctionCompleted != null)
            {
                Debug.Assert(state.Exception != null);
                bw.Write((ushort)0xcafe);
                lastFunctionCompleted.WriteOutputsString(state, bw);
            }

            // We write a unique delimiter at the end of each process to remove any
            // potential ambiguity from our generated string. We guarantee that the
            // type id of a stack frame will never be "0xface". Without this delimiter
            // it's at least theoretically possible that two distinct states could
            // yield the same string.

            bw.Write((ushort)0xface);
        }

        #endregion Fingerprinting
    }
}