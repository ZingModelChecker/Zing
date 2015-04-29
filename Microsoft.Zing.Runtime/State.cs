using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Xml;

namespace Microsoft.Zing
{
    #region Types related to getting information from a Zing state

    /// <summary>
    /// This enumeration denotes the type of a Zing state.
    /// </summary>
    /// <remarks>
    /// Of the five state types here, three are terminal states from which no
    /// transitions are possible (Error, NormalTermination, FailedAssumption)
    /// and two are non-terminal state types (Execution and Choice).
    /// </remarks>
    public enum StateType
    {
        /// <summary>
        /// A non-terminal node in which forward progress is possible by executing one of
        /// the runnable processes.
        /// </summary>
        Execution,

        /// <summary>
        /// A non-terminal node in which forward progress is possible by
        /// selecting from a set of available non-deterministic choices.
        /// </summary>
        Choice,

        /// <summary>
        /// A terminal node in which some error has occurred. Use the
        /// <see cref="Error"/> property to retrieve specific information
        /// about the error.
        /// </summary>
        Error,

        /// <summary>
        /// A terminal node in which all processes have either terminated
        /// normally or are in a valid end-state.
        /// </summary>
        NormalTermination,

        /// <summary>
        /// A terminal node in which an "assume" statement has failed. This
        /// condition halts further execution from the state, but is not
        /// considered an error - it simply prunes the search tree.
        /// </summary>
        FailedAssumption
    }

    /// <summary>
    /// This class denotes a contiguous fragment of Zing source code.
    /// </summary>
    /// <remarks>
    /// The Zing object model exposes the source code from which it was compiled. As
    /// a Zing model is executed, the current source context for each process may be
    /// obtained in the form of a ZingSourceContext. The readonly properties return
    /// the index of the source file and an offset range within it.
    /// </remarks>
    public class ZingSourceContext
    {
        public ZingSourceContext()
            : this(0, 0, 0)
        {
        }

        /// <summary>
        /// Construct a ZingSourceContext for the given docIndex, startColumn, and endColumn.
        /// </summary>
        /// <param name="docIndex">The zero-based index of the source file referenced.</param>
        /// <param name="startColumn">The starting column number.</param>
        /// <param name="endColumn">The ending column number (not inclusive)</param>
        public ZingSourceContext(int docIndex, int startColumn, int endColumn)
        {
            this.docIndex = docIndex;
            this.startColumn = startColumn;
            this.endColumn = endColumn;
        }

        /// <summary>
        /// Returns the index of the source file referenced.
        /// </summary>
        public int DocIndex { get { return docIndex; } }

        private int docIndex;

        /// <summary>
        /// Returns the starting column number of the source fragment.
        /// </summary>
        public int StartColumn { get { return startColumn; } }

        private int startColumn;

        /// <summary>
        /// Returns the ending column number of the source fragment.
        /// </summary>
        public int EndColumn { get { return endColumn; } }

        private int endColumn;

        public void CopyTo(ZingSourceContext other)
        {
            other.docIndex = this.docIndex;
            other.endColumn = this.endColumn;
            other.startColumn = this.startColumn;
        }
    }

    /// <summary>
    /// This enumeration provides the status of a Zing process
    /// </summary>
    public enum ProcessStatus
    {
        /// <summary>
        /// The process is currently runnable.
        /// </summary>
        Runnable,

        /// <summary>
        /// The process is blocked in a select statement which is *not* marked with the
        /// "end" keyword and thus is not a suitable endstate for the process.
        /// </summary>
        Blocked,

        /// <summary>
        /// The process is blocked in a select statement marked with the "end" keyword.
        /// If no other processes are runnable, the model is in a valid end state.
        /// </summary>
        BlockedInEndState,

        /// <summary>
        /// The process completed normally through a return out of it's entry point.
        /// </summary>
        Completed
    }

    /// <summary>
    /// This structure returns information about a Zing process. It is obtained by calling
    /// State.GetProcessInfo().
    /// </summary>
    public struct ProcessInfo
    {
        /// <summary>
        /// Returns the current status of the process
        /// </summary>
        public ProcessStatus Status { get { return status; } }

        private ProcessStatus status;

        /// <summary>
        /// Returns the name of the process (i.e. its entry point)
        /// </summary>
        public string Name { get { return name; } }

        private string name;

        /// <summary>
        /// Returns the name of the method on the top of the stack.
        /// </summary>
        public string MethodName
        {
            get
            {
                return topOfStack.MethodName;
            }
        }

        /// <summary>
        /// Returns the source context of the active method.
        /// </summary>
        public ZingSourceContext Context { get { return context; } }

        private ZingSourceContext context;

        /// <summary>
        /// Returns the context attribute found on the current statement of
        /// the active method, or null if no attribute was present.
        /// </summary>
        public ZingAttribute ContextAttribute { get { return contextAttribute; } }

        private ZingAttribute contextAttribute;

        /// <summary>
        /// Returns a string representation of the logical program counter for the active method.
        /// </summary>
        public string ProgramCounter
        {
            get
            {
                return topOfStack.ProgramCounter;
            }
        }

        /// <summary>
        /// Returns true if a "backward" transition was encountered in the last execution.
        /// </summary>
        public bool BackTransitionEncountered
        {
            get
            {
                return this.backTransitionEncountered;
            }
        }

        private bool backTransitionEncountered;

        // We keep the top of stack around so we can lazily compute the ProgramCounter and
        // MethodName later. This is a big win since these operations use reflection and are
        // not required in performance-critical paths.
        private ZingMethod topOfStack;

        internal ProcessInfo(Process p)
        {
            topOfStack = p.TopOfStack;
            backTransitionEncountered = p.BackTransitionEncountered;

            status = p.CurrentStatus;
            name = Utils.Unmangle(p.Name);
            context = p.Context;
            contextAttribute = p.ContextAttribute;
        }

        public override bool Equals(object obj)
        {
            ProcessInfo other = (ProcessInfo)obj;

            return
                this.backTransitionEncountered == other.backTransitionEncountered &&
                this.context == other.context &&
                this.contextAttribute == other.contextAttribute &&
                this.name == other.name &&
                this.status == other.status &&
                this.topOfStack == other.topOfStack;
        }

        public override int GetHashCode()
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }

        public static bool operator ==(ProcessInfo p1, ProcessInfo p2)
        {
            return p1.Equals(p2);
        }

        public static bool operator !=(ProcessInfo p1, ProcessInfo p2)
        {
            return p1.Equals(p2);
        }
    }

    #endregion Types related to getting information from a Zing state

    #region Execution traces & related types

    /// <summary>
    /// This enum lists the possible results of running the model-checker or
    /// refinement checker.
    /// </summary>
    public enum ZingerResult
    {
        /// <summary>
        /// No errors were found within the specified limits of the search (if any).
        /// </summary>
        Success = 0,

        /// <summary>
        /// The state-space search was cancelled.
        /// </summary>
        Canceled = 1,

        /// <summary>
        /// A state was found in which one or more processes were stuck, but not at
        /// valid "end" states.
        /// </summary>
        Deadlock = 2,

        /// <summary>
        /// An assertion failure was encountered in the Zing model.
        /// </summary>
        Assertion = 3,

        /// <summary>
        /// This code is returned for a number of different runtime errors in the user's
        /// Zing model (null reference, divide by zero, integer overflow, unhandled
        /// Zing exception, and index out of range, invalid blocking select, invalid
        /// choose).
        /// </summary>
        ProgramRuntimeError = 4,

        /// <summary>
        /// This error is returned when an unexpected runtime error is encountered. This
        /// typically indicates a bug in the Zing compiler or runtime.
        /// </summary>
        ZingRuntimeError = 5,

        /// <summary>
        /// Liveness Acceptance Cycle
        /// </summary>
        AcceptanceCyleFound = 6,

        /// <summary>
        /// Invalid Parameters Passed
        /// </summary>
        InvalidParameters = 7,

        /// <summary>
        /// DFS search stack size exceeded the maximum size.
        /// </summary>
        DFSStackOverFlowError = 8
    }

    /// <summary>
    /// A step structure represents one edge in a Zing state-transition graph.
    /// </summary>
    /// <remarks>
    /// A step is normally part of an execution trace. It represents
    /// either the execution of a particular process or the selection of a
    /// particular non-deterministic choice.
    /// </remarks>
    [Serializable]
    public struct TraceStep
    {
        [CLSCompliant(false)]
        public TraceStep(uint data)
        {
            stepData = (ushort)data;
        }

#if UNUSED

        internal TraceStep(bool isExecutionStep, uint selection)
        {
            stepData = (selection << 1) | (isExecutionStep ? 0u : 1u);
        }
#endif

        public TraceStep(bool isExecutionStep, int selection)
        {
            stepData = (ushort)selection;
            stepData <<= 1;
            stepData |= isExecutionStep ? (ushort)0 : (ushort)1;
        }

        private ushort stepData;

        [CLSCompliant(false)]
        public UInt32 StepData { get { return stepData; } }

        public override bool Equals(object obj)
        {
            TraceStep other = (TraceStep)obj;
            return (other.stepData == this.stepData);
        }

        public override int GetHashCode()
        {
            return stepData.GetHashCode();
        }

        public static bool operator ==(TraceStep step1, TraceStep step2)
        {
            return step1.Equals(step2);
        }

        public static bool operator !=(TraceStep step1, TraceStep step2)
        {
            return !step1.Equals(step2);
        }

        /// <summary>
        /// Does the Step represent a process execution?
        /// </summary>
        public bool IsExecution
        {
            get
            {
                return (stepData & 1) == 0;
            }
        }

        /// <summary>
        /// Does the Step represent a non-deterministic choice?
        /// </summary>
        public bool IsChoice
        {
            get
            {
                return !this.IsExecution;
            }
        }

        /// <summary>
        /// Return the process or choice number associated with the Step.
        /// </summary>
        public int Selection
        {
            get
            {
                return (int)(stepData >> 1);
            }
        }
    }

    /// <summary>
    /// A trace object contains an array of Step structures representing some
    /// execution of a Zing model.
    /// </summary>
    /// <remarks>
    /// A trace object represents a series of transitions from the initial state
    /// of a Zing model to some ending state (terminal or non-terminal). Traces
    /// may represent any execution of a Zing model and do not necessarily end
    /// in a terminal state or an error state. Because we implement IEnumerable,
    /// it's possible to use "foreach" to iterate over the steps in a trace. An
    /// indexer is also provided for accessing the Steps with a numeric index.
    /// </remarks>
    [SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix")]
    [Serializable]
    public class Trace : IEnumerable, IEnumerable<TraceStep>
    {
        /// <summary>
        /// Construct an empty trace object.
        /// </summary>
        public Trace()
        {
            this.steps = new List<TraceStep>();
        }

        /// <summary>
        /// Construct a trace object and populate it with the given step array.
        /// </summary>
        /// <param name="steps">An array of Step structs containing the trace data</param>
        public Trace(TraceStep[] steps)
        {
            this.steps = new List<TraceStep>(steps.Length);
            this.steps.AddRange(steps);
        }

        //
        // This constructor is only needed for the distributed checker
        //
        [CLSCompliant(false)]
        public Trace(uint[] stepList)
        {
            this.steps = new List<TraceStep>(stepList.Length);
            for (int i = 0; i < stepList.Length; ++i)
            {
                steps.Add(new TraceStep(stepList[i]));
            }
        }

        private List<TraceStep> steps;

        /// <summary>
        /// Add a Step to the end of the current trace.
        /// </summary>
        /// <param name="traceStep">The step to be added to the trace.</param>
        public void AddStep(TraceStep traceStep)
        {
            steps.Add(traceStep);
        }

        /// <summary>
        /// Insert a Step at the beginning of the current trace.
        /// </summary>
        /// <param name="traceStep">The step to be inserted in the trace.</param>
        public void InsertStep(TraceStep traceStep)
        {
            steps.Insert(0, traceStep);
        }

        /// <summary>
        /// Return the number of steps in the trace.
        /// </summary>
        public int Count
        {
            get
            {
                return this.steps.Count;
            }
        }

        /// <summary>
        /// This indexer allows the steps to be set or retrieved by their index.
        /// </summary>
        /// <param name="stepNumber">Index of the step to be retrieved.</param>
        public TraceStep this[int stepNumber]
        {
            get
            {
                return this.steps[stepNumber];
            }
            set
            {
                this.steps[stepNumber] = value;
            }
        }

        /// <summary>
        /// Return an enumerator for the trace.
        /// </summary>
        /// <returns>Returns an enumerator over the steps in the trace.</returns>
        public IEnumerator GetEnumerator()
        {
            return this.steps.GetEnumerator();
        }

        IEnumerator<TraceStep> IEnumerable<TraceStep>.GetEnumerator()
        {
            return this.steps.GetEnumerator();
        }

        /// <summary>
        /// Replays the execution trace against the given initial state returning the
        /// resulting array of State objects.
        /// </summary>
        /// <param name="initialState">The initial state of the model.</param>
        /// <returns>An array of State objects corresponding to the trace.</returns>
        public State[] GetStates(State initialState)
        {
            //during trace generation don't canonicalize symbolic ids
            State[] stateList = new State[this.Count + 1];

            stateList[0] = initialState;

            State currentState = initialState;
            bool isDeadLock = currentState.SI.IsInvalidEndState();

            for (int i = 0; i < this.Count; i++)
            {
                /*
                if (i == 0)
                    Console.Write ("#### State {0} : \r\n {1}", i, currentState);
                else
                {
                    if (this[i-1].IsExecution)
                        Console.Write("#### State {0} (ran process {1}) :\r\n{2}", i, this[i-1].Selection, currentState);
                    else
                        Console.Write("#### State {0} (took choice {1}) :\r\n{2}", i, this[i-1].Selection, currentState);
                }

                if (currentState != null)
                    Console.Write ("\r\nError in state:\r\n{0}\r\n", currentState.Error);
                */

                currentState = currentState.Run(this[i]);
                stateList[i + 1] = currentState;
            }

            return stateList;
        }

        public State GetLastState(State initialState)
        {
            StateImpl stateImpl = initialState.SI;

            for (int i = 0; i < this.Count; i++)
            {
                TraceStep step = this[i];
                if (step.IsChoice)
                    stateImpl.RunChoice((int)step.Selection);
                else
                    stateImpl.RunProcess((int)step.Selection);
            }
            return initialState;
        }

        public override string ToString()
        {
            string retval = "";
            for (int i = 0; i < this.Count; i++)
            {
                if (i > 0)
                {
                    retval += ",";
                }
                TraceStep step = this[i];
                if (step.IsChoice)
                {
                    retval += "C" + step.Selection;
                }
                else
                {
                    retval += "E" + step.Selection;
                }
            }
            return retval;
        }
    }

    #endregion Execution traces & related types

    /// <summary>
    /// This class represents one state in the state-space of a Zing model.
    /// </summary>
    /// <remarks>
    /// A Zing state is created by referencing a Zing assembly (either as an
    /// Assembly object or by a pathname). From an initial state, successor
    /// states can be obtained by calling RunProcess, RunChoice, or Run.
    /// </remarks>
    public class State
    {
        public bool IsAcceptanceState
        {
            get
            {
                return si.IsAcceptingState;
            }
            set
            {
                si.IsAcceptingState = value;
            }
        }

        #region Constructors

        // Users never get State objects by construction, so the constructor is private.
        public State(StateImpl si)
        {
            this.si = si;
        }

        private State(StateImpl si, State lastState)
        {
            this.si = si;
            this.lastState = lastState;
        }

        #endregion Constructors

        #region Fields

        // A reference to our associated state detail.
        private StateImpl si;

        public StateImpl SI
        {
            get { return si; }
        }

        // A reference to our predecessor state or null for the initial state.
        private State lastState;

        // The step by which the current State was reached.
        private TraceStep lastStep;

        #endregion Fields

        #region State creation (for end users)

        /// <summary>
        /// Creates an initial Zing state from the given assembly reference.
        /// </summary>
        /// <param name="zingAssembly">An assembly object referencing a valid Zing assembly.</param>
        /// <returns>Returns the initial state of the referenced Zing model.</returns>
        public static State Load(Assembly zingAssembly)
        {
            return new State(StateImpl.Load(zingAssembly));
        }

        /// <summary>
        /// Creates an initial Zing state from the given assembly reference.
        /// </summary>
        /// <param name="zingAssemblyPath">A pathname pointing to a valid Zing assembly.</param>
        /// <returns>Returns the initial state of the referenced Zing model.</returns>
        public static State Load(string zingAssemblyPath)
        {
            return new State(StateImpl.Load(zingAssemblyPath));
        }

        #endregion State creation (for end users)

        #region Helpful overrides

        /// <summary>
        /// Checks the equality of this state with another by comparing their fingerprints.
        /// This is correct with high probability.
        /// </summary>
        /// <param name="obj">The state object to be compared.</param>
        /// <returns>Returns true if the states are (very likely) equal, false otherwise.</returns>
        public override bool Equals(object obj)
        {
            return this.si.Equals(((State)obj).si);
        }

        /// <summary>
        /// Returns a hash code based on the fingerprint of our underlying state implementation.
        /// The hash codes of "equivalent" states are guaranteed to be equal.
        /// </summary>
        /// <returns>Returns a hash code for the State object.</returns>
        public override int GetHashCode()
        {
            return this.si.GetHashCode();
        }

        /// <summary>
        /// Compares two states using their fingerprints. This is correct with high
        /// probability.
        /// </summary>
        /// <param name="obj1">The first state to be compared.</param>
        /// <param name="obj2">The second state to be compared.</param>
        /// <returns>True, if the states are equal, false otherwise.</returns>
        static public new bool Equals(object obj1, object obj2)
        {
            Debug.Assert(obj1 is State && obj2 is State);

            return obj1.Equals(obj2);
        }

        /// <summary>
        /// Returns a string containing all of the state details in a reasonably readable format.
        /// </summary>
        /// <returns>Returns a string containing our state details.</returns>
        public override string ToString()
        {
            return si.ToString();
        }

        #endregion Helpful overrides

        #region Basic queries

        /// <summary>
        /// Returns a <see cref="Fingerprint"/> object which uniquely (with high probablity)
        /// this state. If two states have the same fingerprint, then they are equivalent
        /// although their details may differ in unimportant respects.
        /// </summary>
        public Fingerprint Fingerprint
        {
            get
            {
                return si.Fingerprint;
            }
        }

        /// <summary>
        /// Returns the <see cref="StateType"/> enumeration value which describes the current state.
        /// </summary>
        public StateType Type
        {
            get
            {
                return si.Type;
            }
        }

        /// <summary>
        /// Returns true on the initial state of the model, false otherwise.
        /// </summary>
        public bool IsInitial
        {
            get
            {
                return this.lastState == null;
            }
        }

        /// <summary>
        /// Returns true if no transitions may be made from the current state.
        /// </summary>
        public bool IsTerminal
        {
            get
            {
                switch (this.Type)
                {
                    case StateType.Execution:
                    case StateType.Choice:
                        return false;

                    default:
                        return true;
                }
            }
        }

        /// <summary>
        /// Returns the <see cref="TraceStep"/> by which the current state was reached. Throws
        /// InvalidOperationException if referenced from the initial state.
        /// I
        /// </summary>
        public TraceStep LastStep
        {
            get
            {
                if (this.lastState == null)
                    throw new InvalidOperationException("Invalid reference to LastStep on the initial state");

                return this.lastStep;
            }
        }

        /// <summary>
        /// Returns a reference to the predecessor state, or null on the initial state.
        /// </summary>
        public State LastState
        {
            get
            {
                return this.lastState;
            }
        }

        /// <summary>
        /// Returns the number of processes present in the current state.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly")]
        public int NumProcesses
        {
            get
            {
                return si.NumProcesses;
            }
        }

        /// <summary>
        /// Returns the number of non-deterministic choices available in the current state or zero
        /// if the current state is not of type StateType.Choice.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly")]
        public int NumChoices
        {
            get
            {
                return si.NumChoices;
            }
        }

        /// <summary>
        /// In a Choice state, this property returns the number of the process currently executing.
        /// </summary>
        public int ChoiceProcess
        {
            get
            {
                return si.choiceProcessNumber;
            }
        }

        /// <summary>
        /// Returns an array of strings containing the pathnames of the files used to compile
        /// the Zing model.
        /// </summary>
        /// <returns>Array of source code pathnames</returns>
        public string[] GetSourceFiles()
        {
            return si.GetSourceFiles();
        }

        /// <summary>
        /// Returns an array of strings containing the Zing source code which corresponds to
        /// the pathnames in the <see cref="GetSourceFiles"/> method.
        /// </summary>
        /// <returns>Array of source code strings</returns>
        public string[] GetSourceText()
        {
            return si.GetSources();
        }

        /// <summary>
        /// For states of type StateType.Error or StateType.FailedAssumption, this property returns
        /// a reference to an exception describing the state's failure. Otherwise, the property
        /// returns null.
        /// </summary>
        public Exception Error
        {
            get
            {
                return si.Error;
            }
        }

        /// <summary>
        /// Returns an array (possibly empty) of event objects describing the actions of the model
        /// during the transition from the prior state to the current one. Event objects all derive
        /// from <see cref="ZingEvent"/> and contain relevant information about process creation,
        /// process termination, messaging, and "trace" statements that were encountered.
        /// </summary>
        /// <returns>Array of event objects</returns>
        public ZingEvent[] GetEvents()
        {
            return si.GetEvents();
        }

        public ZingEvent[] GetTraceLog()
        {
            return si.GetTraceLog();
        }

        #endregion Basic queries

        #region Basic execution model

        /// <summary>
        /// This method is designed for use with execution traces. The caller can
        /// simply enumerate the Steps of the Trace and call Run() to generate the
        /// successor State.
        /// </summary>
        /// <param name="step">A step object describing the transition to be made</param>
        /// <returns>Returns a <see cref="State"/> object representing the new state.</returns>
        public State Run(TraceStep step)
        {
            if (step.IsChoice)
                return this.RunChoice((int)step.Selection);
            else
                return this.RunProcess((int)step.Selection);
        }

        /// <summary>
        /// Returns a new state representing the execution of process <c>p</c> in the current state.
        /// </summary>
        /// <param name="processNumber">The number of the process to be executed.</param>
        /// <returns>Returns a <see cref="State"/> object representing the new state.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the current state is not of type <c>StateType.Execution</c>.</exception>
        public State RunProcess(int processNumber)
        {
            if (this.Type != StateType.Execution)
                throw new InvalidOperationException("RunProcess must be called on an execution state.");

            StateImpl siNew = (StateImpl)si.Clone();
            siNew.RunProcess(processNumber);
            State sNew = new State(siNew, this);
            sNew.lastStep = new TraceStep(true, processNumber);
            return sNew;
        }

        /// <summary>
        /// Returns a new state representing the selection of choice <c>c</c> in the current state.
        /// </summary>
        /// <param name="choice">The number of the choice to be selected.</param>
        /// <returns>Returns a <see cref="State"/> object representing the new state.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the current state is not of type <c>StateType.Choice</c>.</exception>
        public State RunChoice(int choice)
        {
            if (this.Type != StateType.Choice)
                throw new InvalidOperationException("RunChoice must be called on a choice state.");

            StateImpl siNew = (StateImpl)si.Clone();
            siNew.RunChoice(choice);
            State sNew = new State(siNew, this);
            sNew.lastStep = new TraceStep(false, choice);
            return sNew;
        }

        /// <summary>
        /// Returns a new state representing the execution of process <c>processNumber</c> in the current state.
        /// </summary>
        /// <param name="processNumber">The number of the process to be executed.</param>
        /// <param name="steps">The number of steps for which the process should be executed.</param>
        /// <returns>Returns a <see cref="State"/> object representing the new state.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the current state is not of type <c>StateType.Execution</c>.</exception>
        public State RunProcess(int processNumber, int steps)
        {
            if (this.Type != StateType.Execution)
                throw new InvalidOperationException("RunProcess must be called on an execution state.");

            StateImpl siNew = (StateImpl)si.Clone();
            for (int i = 0; i < steps; i++)
                siNew.RunProcess(processNumber);
            State sNew = new State(siNew, this);
            sNew.lastStep = new TraceStep(true, processNumber);
            return sNew;
        }

        /// <summary>
        /// Returns a <see cref="Trace"/> object representing the execution path to the current state.
        /// </summary>
        /// <returns>A Trace object for the current state.</returns>
        public Trace BuildTrace()
        {
            if (this.IsInitial)
            {
                return new Trace();
            }
            else
            {
                Trace t = this.lastState.BuildTrace();
                t.AddStep(this.lastStep);
                return t;
            }
        }

        #endregion Basic execution model

        #region Advanced queries

        /// <summary>
        /// Retrieve the ProcessInfo for a given process id.
        /// </summary>
        /// <param name="processId">The process of interest.</param>
        /// <returns>Returns a populated ProcessInfo struct.</returns>
        public ProcessInfo GetProcessInfo(int processId)
        {
            return si.GetProcessInfo(processId);
        }

        /// <summary>
        /// Returns true if there exists a global variable of the given name.
        /// </summary>
        /// <param name="name">The name of the variable of interest.</param>
        /// <returns>Returns true if the variable exists.</returns>
        public bool ContainsGlobalVariable(string name)
        {
            return si.ContainsGlobalVariable(name);
        }

        /// <summary>
        /// Returns the value of a given global variable.
        /// </summary>
        /// <param name="name">The name of the variable of interest.</param>
        /// <returns>Returns the value of the variable, or null if it doesn't exist.</returns>
        public object LookupGlobalVariableByName(string name)
        {
            return si.LookupGlobalVarByName(name);
        }

        /// <summary>
        /// Returns true if there exists a local variable of the given name.
        /// </summary>
        /// <param name="processId">The process whose current method should be checked.</param>
        /// <param name="name">The name of the variable of interest.</param>
        /// <returns>Returns true if the variable exists in the current stack frame of the given process.</returns>
        public bool ContainsLocalVariable(int processId, string name)
        {
            return si.ContainsLocalVariable(processId, name);
        }

        /// <summary>
        /// Returns the value of a given local variable.
        /// </summary>
        /// <param name="processId">The process whose current method should be checked.</param>
        /// <param name="name">The name of the variable of interest.</param>
        /// <returns>Returns the value of the variable, or null if it doesn't exist.</returns>
        public object LookupLocalVariableByName(int processId, string name)
        {
            return si.LookupLocalVarByName(processId, name);
        }

        /// <summary>
        /// Returns an XML representation of the complete state including processes and their
        /// stacks, global variables, events, and the heap.
        /// </summary>
        /// <returns>An XmlDocument object containing a dump of the state details.</returns>
        public XmlDocument ToXml()
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml("<?xml version='1.0'?><ZingState/>");
            //TODO: doc.ReadXmlSchema(...);
            si.ToXml(doc.DocumentElement);

            return doc;
        }

        #endregion Advanced queries
    }
}