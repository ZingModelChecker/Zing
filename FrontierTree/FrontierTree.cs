using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Zing;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Reflection;

namespace Microsoft.Zing
{
    sealed class AllowAllAssemblyVersionsDeserializationBinder : System.Runtime.Serialization.SerializationBinder
    {
        public override Type BindToType (string assemblyName, string typeName)
        {
            Type typeToDeserialize = null;
            try
            {
                string ToAssemblyName = assemblyName.Split(',')[0];
                Assembly[] Assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (Assembly ass in Assemblies)
                {
                    if (ass.FullName.Split(',')[0] == ToAssemblyName)
                    {
                        typeToDeserialize = ass.GetType(typeName);
                        break;
                    }
                }
            }
            catch (System.Exception exception)
            {
                throw exception;
            }
            return typeToDeserialize;


        }
    }

    /// <summary>
    /// This class represents a frontier tree and is designed for use with 
    /// iterative depth bounding DFS.
    /// The idea of the data structure is that one can form a tree with
    /// a set of frontiers encountered at each depth increment, and use it
    /// to make partitioning decisions so that the ENTIRE trace need not be played
    /// back to obtain each frontier state starting from the root state. Instead, we 
    /// can incrementally replay only from the parent of the frontier state from the 
    /// previous depth bounded iteration
    /// 
    /// Each tree node consists of the following fields
    /// 1. A trace to get upto the node in question from the parent node described in 3 below
    /// 2. Depth of the node
    /// 3. A pointer to ITS parent node
    /// 4. A list of "next" frontier nodes reachable from this node
    /// 5. A fingerprint representing the fingerprint of the state that this trace corresponds to
    /// The field 4 above is populated during the "next" depth bounded iteration
    /// 
    /// The parent node for the root node is null and its trace is null as well
    /// 
    /// The static fields provide the state management necessary by storing per process
    /// state stacks
    /// </summary>
    ///
    [Serializable]
    public abstract class FrontierNode
    {
        // if delay bounding is enabled
        public IZingSchedulerState schedulerState;
        public Int16 numOfDelays;
        public ZingBounds Bounds;

        [NonSerialized]
        public int threadID;
        [NonSerialized]
        public int MyNumber;
    }

    public class FrontierTree : FrontierRepresentation
    {
        #region Frontier Data

        public static int countFrontier = 0;

        private Fingerprint fp;
        public Fingerprint Fingerprint
        {
            get { return fp; }
        }

        private Trace traceFromParent;
        public Trace TraceFromParent
        {
            get { return traceFromParent; }
        }

        private int level;
        public int Level
        {
            get { return level; }
        }

        private FrontierTree parent;
        public FrontierTree Parent
        {
            get { return parent; }
        }

        private Dictionary<Fingerprint, FrontierTree> childNodes;
        public Dictionary<Fingerprint, FrontierTree> ChildNodes
        {
            get { return childNodes; }
        }

        #endregion

        #region Per thread state

        private static Stack<KeyValuePair<FrontierTree, TraversalInfo>>[] PerTaskStacks = null;

        private static FrontierTree initialFrontier;

        public static FrontierTree InitialFrontier
        {
            get { return initialFrontier; }
        }

        public static void Initialize(TraversalInfo InitialTI)
        {
            PerTaskStacks = new Stack<KeyValuePair<FrontierTree, TraversalInfo>>[Options.DegreeOfParallelism];
            FrontierTree InitialFT = new FrontierTree();

            for (int i = 0; i < Options.DegreeOfParallelism; ++i)
            {
                PerTaskStacks[i] = new Stack<KeyValuePair<FrontierTree, TraversalInfo>>();
                // Push the initial state onto each of the stacks
                PerTaskStacks[i].Push(new KeyValuePair<FrontierTree, TraversalInfo>(InitialFT, InitialTI.Clone(i)));
            }
            FrontierTree.initialFrontier = InitialFT;
            InitialFT.fp = InitialTI.Fingerprint;
        }

        /// <summary>
        /// Returns a traversal info given a frontier tree object.
        /// Tries to minimize the work done. For best results
        /// each successive frontier tree object passed to successive 
        /// calls to this method by a task must have a common ancestor
        /// 
        /// The API will work even otherwise, but might be slower than
        /// using entire traces instead
        /// </summary>
        /// <param name="frontierTree">The FrontierTree object for which a TraversalInfo object is required</param>
        /// <param name="SerialNum">The serial number of the task</param>
        /// <returns></returns>
        public static TraversalInfo GetTraversalInfoHelper(FrontierTree frontierTree, int SerialNum)
        {
            Stack<KeyValuePair<FrontierTree, TraversalInfo>> MyStack = PerTaskStacks[SerialNum];
            TraversalInfo retval = null;
            Stack<Fingerprint> IndexStack = new Stack<Fingerprint>();
            FrontierTree MatchingFrontierTree = null;
            FrontierTree LowestCommonAncestor = null;
            FrontierTree CurrentStep = null;

            // Check for the corner case where we're requesting the TraversalInfo for
            // the same FrontierTree object as the last time around
            if (frontierTree == MyStack.Peek().Key)
            {
                return (MyStack.Peek().Value);
            }

            // Check if the parent of this frontier tree is the same as the
            // parent of the frontier tree on the top of the stack
            if (frontierTree.Level == MyStack.Peek().Key.Level &&
                frontierTree.Parent == MyStack.Peek().Key.Parent)
            {
                // Pop off the previously issued state
                MyStack.Pop();
                // Replay the trace from the top of the stack
                retval = MyStack.Peek().Key.ReplayToChildN(MyStack.Peek().Value, frontierTree.Fingerprint);
                MyStack.Push(new KeyValuePair<FrontierTree, TraversalInfo>(frontierTree, retval));
                return (retval);
            }

            // Check if we're going one level deeper than we were previously at
            // i.e. we're starting a new iteration. In this case, the level of 
            // the frontierTree that was passed will be one more than whatever
            // was previously doled out and is on top of the stack

            MatchingFrontierTree = frontierTree;
            while (MatchingFrontierTree.Level > MyStack.Peek().Key.Level)
            {
                IndexStack.Push(MatchingFrontierTree.Fingerprint);
                MatchingFrontierTree = MatchingFrontierTree.Parent;
            }

            while (MyStack.Peek().Key.Level > MatchingFrontierTree.Level)
            {
                MyStack.Pop();
            }

            // The level of MatchingFrontierTree is now the same as that of the 
            // FrontierTree object on the top of the stack
            // Now search for the lowest common ancestor

            LowestCommonAncestor = MatchingFrontierTree;

            while (LowestCommonAncestor != MyStack.Peek().Key)
            {
                IndexStack.Push(LowestCommonAncestor.Fingerprint);
                LowestCommonAncestor = LowestCommonAncestor.Parent;
                MyStack.Pop();
            }

            // We're now at the lowest common ancestor. Use the 
            // indices gathered on the index stack to replay the
            // trace to the required frontier tree.
            // Also push an entry for each step of the trace generated onto the 
            // Per task stack

            retval = MyStack.Peek().Value;
            CurrentStep = LowestCommonAncestor;

            while (IndexStack.Count > 0)
            {
                retval = CurrentStep.ReplayToChildN(retval, IndexStack.Peek());
                CurrentStep = CurrentStep.GetChildN(IndexStack.Peek());
                MyStack.Push(new KeyValuePair<FrontierTree, TraversalInfo>(CurrentStep, retval));
                IndexStack.Pop();
            }

            // We're all done
            return (retval);
        }

        #endregion

        #region FrontierTree methods
        public FrontierTree()
        {
            traceFromParent = null;
            parent = null;
            this.level = 0;
            childNodes = new Dictionary<Fingerprint, FrontierTree>();
        }

        private FrontierTree(Trace TraceFromParent, FrontierTree Parent)
        {
            this.traceFromParent = TraceFromParent;
            this.parent = Parent;
            this.level = Parent.Level + 1;
            this.childNodes = new Dictionary<Fingerprint, FrontierTree>();
        }

        /// <summary>
        /// This the publicly visible constructor used by FrontierRepresentation.MakeFrontierRepresentation()
        /// to build up the frontier representation based on the options provided. It also inserts the
        /// new frontier representation to the frontier tree built up.
        /// 
        /// Usage: This constructor is assumed to be called with a ti that is reachable from the ti that 
        /// was previously obtained using GetTraversalInfo() on a FrontierTree object. i.e., the ti passed
        /// must be reachable from the entry on the top of the stack for the task identified by SerialNum.
        /// </summary>
        /// <param name="ti">The TraversalInfo object for which the FrontierTree is to be generated</param>
        /// <param name="SerialNum">The Serial Number of the task</param>
        public FrontierTree(TraversalInfo ti, int SerialNum)
        {
            Stack<KeyValuePair<FrontierTree, TraversalInfo>> MyStack = PerTaskStacks[SerialNum];
            this.Bounds = new ZingBounds(ti.Bounds.Depth, ti.Bounds.Delay);
            this.level = MyStack.Peek().Key.Level + 1;
            this.parent = MyStack.Peek().Key;
            this.fp = ti.Fingerprint;
            this.childNodes = new Dictionary<Fingerprint, FrontierTree>();
            lock (Parent.ChildNodes)
            {
                if (!parent.ChildNodes.ContainsKey(this.Fingerprint))
                    Parent.ChildNodes.Add(this.Fingerprint, this);
            }
            this.traceFromParent = ti.GenerateTraceToParent(MyStack.Peek().Value);
        }

        /// <summary>
        /// Unlinks this frontier representation from the parent
        /// </summary>
        public void Dispose()
        {
            lock (this.Parent.ChildNodes)
            {
                this.Parent.ChildNodes.Remove(this.Fingerprint);
            }
        }

        private TraversalInfo ReplayToChildN(TraversalInfo ti, Fingerprint fp)
        {
            TraversalInfo retval = ti;
            Trace TraceToRun = null;
            TraceToRun = ChildNodes[fp].TraceFromParent;
            int Step = 0;
            while (Step < TraceToRun.Count)
            {
                if (Options.CompactTraces && retval.HasMultipleSuccessors)
                {
                    retval = retval.GetSuccessorNForReplay(TraceToRun[Step++].Selection, false);
                }
                else if (Options.CompactTraces)
                {
                    while (!retval.HasMultipleSuccessors && Options.CompactTraces)
                    {
                        int n = 0;
                        if (retval.stateType.Equals(TraversalInfo.StateType.ExecutionState))
                        {
                            int i = 0;
                            while (i < retval.NumProcesses)
                            {
                                if (retval.ProcessInfo[i].Status.Equals(ProcessStatus.Runnable))
                                {
                                    n = i;
                                    break;
                                }
                                i++;
                            }
                        }
                        retval = retval.GetSuccessorNForReplay(n, false);
                    }
                }
                else
                {
                    retval = retval.GetSuccessorNForReplay(TraceToRun[Step++].Selection, false);
                }
            }

            while (!retval.HasMultipleSuccessors && Options.CompactTraces)
            {
                int n = 0;
                if (retval.stateType.Equals(TraversalInfo.StateType.ExecutionState))
                {
                    int i = 0;
                    while (i < retval.NumProcesses)
                    {
                        if (retval.ProcessInfo[i].Status.Equals(ProcessStatus.Runnable))
                        {
                            n = i;
                            break;
                        }
                        i++;
                    }
                }
                retval = retval.GetSuccessorNForReplay(n, false);
            }
            return (retval);
        }

        private FrontierTree GetChildN(Fingerprint fp)
        {
            FrontierTree LookupRet;

            this.ChildNodes.TryGetValue(fp, out LookupRet);

            return (LookupRet);
        }

        #endregion

        #region Trace management methods
        public override TraversalInfo GetTraversalInfo(StateImpl InitialState, int SerialNum)
        {
            return (GetTraversalInfoHelper(this, SerialNum));
        }
        #endregion
    }

    [Serializable]
    public class TraceFrontier : FrontierRepresentation
    {
        private Trace TheTrace;

        public TraceFrontier(TraversalInfo ti)
            : base(ti)
        {
            this.Bounds = new ZingBounds(ti.Bounds.Depth, ti.Bounds.Delay);
            if(Options.IsSchedulerDecl)
            {
                schedulerState = ti.ZingDBSchedState.CloneF();
                numOfDelays = ti.numOfTimesCurrStateDelayed;
            }
            ti.IsFingerPrinted = true;
            TheTrace = ti.GenerateTrace();

        }

        public TraceFrontier()
            : base()
        {
            this.TheTrace = null;

        }

        public override TraversalInfo GetTraversalInfo(StateImpl InitialState, int SerialNum)
        {
            TraversalInfo retTraversalInfo = GetTraversalInfoForTrace(TheTrace, SerialNum, (StateImpl)InitialState.Clone(SerialNum));
            retTraversalInfo.Bounds = Bounds;
            if (Options.IsSchedulerDecl)
            {
                retTraversalInfo.ZingDBSchedState = schedulerState;
                retTraversalInfo.numOfTimesCurrStateDelayed = numOfDelays;
            }
            retTraversalInfo.IsFingerPrinted = true;
            return retTraversalInfo;
        }

        #region Public helpers

        /// <summary>
        /// Executes the model checker from the model initial state to the state represented by the given trace.
        /// Sets the initial state for algorithms not using reduction.
        /// </summary>
        /// <param name="trace">The trace that the model checker follows to set the initial TraversalInfo state. Can be null.</param>
        /// <returns>The transition depth of the resultant state.</returns>
        TraversalInfo GetTraversalInfoForTrace(Trace trace, int SerialNum, StateImpl iState)
        {
            TraversalInfo ti = new ExecutionState((StateImpl)iState.Clone(SerialNum), null, null);
            int Step = 0;
            if (trace != null)
            {
                #region Trace Count greater than zero
                if (trace.Count > 0)
                {
                    while (Step < trace.Count)
                    {
                        if (Options.CompactTraces && ti.HasMultipleSuccessors)
                        {
                            ti = ti.GetSuccessorNForReplay((int)trace[Step++].Selection, false);
                        }
                        else if (Options.CompactTraces)
                        {
                            while (!ti.HasMultipleSuccessors && Options.CompactTraces)
                            {
                                int n = 0;
                                if (ti.stateType.Equals(TraversalInfo.StateType.ExecutionState))
                                {
                                    int i = 0;
                                    while (i < ti.NumProcesses)
                                    {
                                        if (ti.ProcessInfo[i].Status.Equals(ProcessStatus.Runnable))
                                        {
                                            n = i;
                                            break;
                                        }
                                        i++;
                                    }
                                }
                                ti = ti.GetSuccessorNForReplay(n, false);
                            }
                        }
                        else
                        {
                            ti = ti.GetSuccessorNForReplay((int)trace[Step++].Selection, false);
                        }

                    }
                }
                #endregion

                #region Traversing the tail

                while (!ti.HasMultipleSuccessors && Options.CompactTraces)
                {
                    int n = 0;
                    if (ti.stateType.Equals(TraversalInfo.StateType.ExecutionState))
                    {
                        int i = 0;
                        while (i < ti.NumProcesses)
                        {
                            if (ti.ProcessInfo[i].Status.Equals(ProcessStatus.Runnable))
                            {
                                n = i;
                                break;
                            }
                            i++;
                        }
                    }
                    ti = ti.GetSuccessorNForReplay(n, false);
                }

                #endregion

            }
            return ti;
        }

        #endregion

        #region Serialize the Trace Frontier
        public void Serialize (Stream outputStream)
        {
            var bWriter = new BinaryWriter(outputStream);
            //dump numOfDelays, Delay and Depth
            bWriter.Write(this.numOfDelays);
            bWriter.Write(this.Bounds.Delay);
            bWriter.Write(this.Bounds.Depth);
            //dump scheduler state
            if (Options.IsSchedulerDecl)
            {
                BinaryFormatter bFormat = new BinaryFormatter();
                bFormat.Serialize(outputStream, this.schedulerState);
            }
            //dump trace length
            bWriter.Write((Int32)TheTrace.Count());
            foreach (TraceStep traceStep in TheTrace)
            {
                bWriter.Write(traceStep.StepData);
            }
        }

        public void Deserialize (Stream inputStream)
        {
            BinaryReader bReader = new BinaryReader(inputStream);

            this.numOfDelays = bReader.ReadInt16();
            this.Bounds = new ZingBounds();
            this.Bounds.Delay = bReader.ReadInt32();
            this.Bounds.Depth = bReader.ReadInt32();
            if (Options.IsSchedulerDecl)
            {
                BinaryFormatter bFormat = new BinaryFormatter();
                bFormat.Binder = new AllowAllAssemblyVersionsDeserializationBinder();
                this.schedulerState = (IZingSchedulerState)bFormat.Deserialize(inputStream);
            }
            Int32 traceCount = bReader.ReadInt32();
            UInt32[] steps = new UInt32[traceCount];
            for (int i = 0; i < traceCount; i++)
            {
                steps[i] = bReader.ReadUInt32();
            }
            this.TheTrace = new Trace(steps);

        }
        #endregion
    }

    [Serializable]
    public abstract class FrontierRepresentation : FrontierNode
    {
        public FrontierRepresentation(TraversalInfo ti)
        {

        }

        public FrontierRepresentation()
        {

        }

        public abstract TraversalInfo GetTraversalInfo(StateImpl InitialState, int SerialNum);

        public static FrontierRepresentation MakeFrontierRepresentation(TraversalInfo ti, int SerialNum)
        {
            FrontierRepresentation result;
            if (Options.UseHierarchicalFrontiers)
            {
                result = (new FrontierTree(ti, SerialNum));
            }
            else
            {
                result = (new TraceFrontier(ti));
            }
            return result;
        }


        public static FrontierRepresentation MakeFrontierRepresentation (TraversalInfo ti)
        {
            FrontierRepresentation result;
            result = (new TraceFrontier(ti));
            return result;
        }
    }
}
