using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Collections.Concurrent;
using System.Threading;
using System.Runtime;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;


namespace Microsoft.Zing
{


    /// <summary>
    /// Callback interface allows for user feedback or cancellation during state-space searches.
    /// </summary>
    /// <remarks>
    /// This interface is used by callers of Cruncher.Crunch who wish to permit interactive
    /// cancellation of a search and/or display statistics during the search.
    /// </remarks>
    public interface ICruncherCallback
    {
        /// <summary>
        /// This method is called periodically during a state-space search to permit the
        /// searching application to either cancel the search or display information
        /// regarding its progress.
        /// </summary>
        /// <returns>Return true to request cancellation of the search.</returns>
        bool CheckCancel ();
    }

    #region IDBDFS helper classes

    public class PLINQErrorEncounteredException : System.Exception
    {
        public override string ToString ()
        {
            return ("Error State Encountered in at least one PLINQ task");
        }
    }



    public class SerialNumberGenerator
    {
        static long CurrentSerialNum = 0;

        // Assumption: We'll never call this method
        // long enough that this counter wraps around
        // 64 bits is a lot!
        public static uint GetSerialNum ()
        {
            long retval = Interlocked.Increment(ref CurrentSerialNum);
            return ((uint)(retval % Options.DegreeOfParallelism));
        }
    }


    #endregion

    #region helper class and functions for Frontier To Disk
    sealed public class FrontierWorkItem
    {
        //block size is right now fixed to 64
        public List<TraceFrontier> FrontierBlock;

        public FrontierWorkItem ()
        {
            FrontierBlock = new List<TraceFrontier>();
        }

        public void Add (FrontierNode fNode)
        {
            FrontierBlock.Add((TraceFrontier)fNode);
        }

        public int Count ()
        {
            return FrontierBlock.Count;
        }
    }
    #endregion

    sealed public class ParallelExplorer : MarshalByRefObject
    {
        #region Variables for Stats
        /// <summary>
        /// The number of distinct states encountered during the search.
        /// </summary>
        private Int64 numDistinctStates;

        public Int64 NumDistinctStates
        {
            get { return numDistinctStates; }
            set { numDistinctStates = value; }
        }
        /// <summary>
        /// The total number of state transitions considered.
        /// </summary>
        private Int64 numTotalStates;

        public Int64 NumTotalStates
        {
            get { return numTotalStates; }
            set { numTotalStates = value; }
        }

        private int numStatesRevisited = 0;

        private TimeSpan[] totalTraversalTime = new TimeSpan[Options.DegreeOfParallelism];

        private int maxDepth;

        public int MaxDepth
        {
            get { return maxDepth; }
            set { maxDepth = value; }
        }
        #endregion

        #region Hash Table for States
        /// <summary>
        /// The 2^(TableSize) is the upper bound on the number of buckets in the hash table.
        /// The actual number is the first prime number that is slightly smaller than 2^(TableSize).
        /// </summary>
        private int tablesize = 19;
        private ConcurrentDictionary<Fingerprint, DFSLiveState> LiveStates;
        private DeadStateTable DeadStates;
        #endregion

        #region Error Trace
        private ArrayList AcceptingCycles;
        private ArrayList SafetyErrors;
        public bool DFSStackOverFlowError = false;
        private CheckerResult lastErrorFound = CheckerResult.Success;
        #endregion

        #region Thread Control
        private ICruncherCallback callbackObj;
        CancellationTokenSource cs;
        private TCancel tc;

        public TCancel Tc
        {
            [EditorBrowsable(EditorBrowsableState.Never)]
            get { return tc; }

        }
        #endregion

        #region Start State
        private TraversalInfo TStartState;

        /// <summary>
        /// Returns the State object representing the initial state of the model.
        /// </summary>
        /// <remarks>
        /// This is helpful to callers wishing to recreate an execution trace.
        /// </remarks>
        public State InitialState
        {
            get
            {
                return new State(initialState);
            }
        }
        internal StateImpl initialState;

        #endregion

        #region Zing Plugin
        public Dictionary<string, IZingPlugin> ZingPlugin;
        #endregion

        #region Frontiers
        // In memory Frontiers
        private ConcurrentDictionary<Fingerprint, FrontierNode> globalFrontier = new ConcurrentDictionary<Fingerprint, FrontierNode>();

        // Frontiers to Disk
        // Block of Frontiers (workItem)
        private int numberWorker;
        private BlockingCollection<FrontierWorkItem> newFrontier;
        private BlockingCollection<FrontierWorkItem> currFrontier;
        private HashSet<Fingerprint> frontierSet;
        #endregion

        #region Constructors
        public ParallelExplorer (string modelFile, Dictionary<string, IZingPlugin> ZingP)
        {
            initialState = StateImpl.Load(modelFile);
            ZingPlugin = ZingP;
            initialState.ZingPlugin = ZingPlugin;
            Init();
        }

        public ParallelExplorer (string fileName)
        {
            initialState = StateImpl.Load(fileName);
            Init();
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ParallelExplorer ()
        {
        }

        // NOTE: 'fp' is unused because we must compute the fingerprint but don't need the result
        [SuppressMessage("Microsoft.Performance", "CA1804:RemoveUnusedLocals")]
        private void Init ()
        {
            // make sure that the heap is GC'ed
            numberWorker = Options.DegreeOfParallelism;
            newFrontier = new BlockingCollection<FrontierWorkItem>(2 * numberWorker);
            currFrontier = new BlockingCollection<FrontierWorkItem>(2 * numberWorker);

            Fingerprint fp = initialState.Fingerprint;
            LiveStates = new ConcurrentDictionary<Fingerprint, DFSLiveState>();

            DeadStates = new DeadStateTable(tablesize);

            tc = new TCancel();
            SafetyErrors = new ArrayList();
            AcceptingCycles = new ArrayList();
            cs = new CancellationTokenSource();
        }

        #endregion

        #region Properties for controlling search behavior
        public int MaxDFSStackLength = int.MaxValue;
        public ZingBoundedSearch BoundedSearch;

        public bool SaveChoiceStatesOnly
        {
            get { return this.saveChoiceStatesOnly; }
            set { this.saveChoiceStatesOnly = value; }
        }
        private bool saveChoiceStatesOnly = false;

        /// <summary>
        /// If true, the search is stopped when the first error is encountered.
        /// </summary>
        public bool StopOnError
        {
            get { return this.stopOnError; }
            set { this.stopOnError = value; }
        }
        private bool stopOnError;

        # endregion

        #region NDFS Liveness

        private TraversalInfo SetMagicBit (TraversalInfo Ti)
        {
            var fp = Ti.Fingerprint;
            if (DeadStates.Contains(fp))
            {
                DeadStates.Update(fp, DeadStates.LookupValue(fp).ExploreIfDepthLowerThan, true);
            }
            else if (LiveStates.ContainsKey(fp))
            {
                DFSLiveState dstate;

                LiveStates.TryGetValue(fp, out dstate);
                DFSLiveState newdstate = new DFSLiveState(dstate.ExploreIfDepthLowerThan, dstate.CompletelyExplored, true);
                LiveStates.TryUpdate(fp, newdstate, dstate);
            }
            return Ti.SetMagicbit();
        }

        #endregion

        #region In Memory Frontier

        bool DoDFSFrontierInMemory (TCancel cancel)
        {

            DeadStates.Init();

            IEnumerable<KeyValuePair<Fingerprint, FrontierNode>> CurrentFrontier;
            numDistinctStates = numTotalStates = 0;
            if (Options.UseHierarchicalFrontiers)
            {
                FrontierTree.Initialize(TStartState);
            }

            CurrentFrontier = new LinkedList<KeyValuePair<Fingerprint, FrontierNode>>();

            if (!Options.UseHierarchicalFrontiers)
            {
                globalFrontier.TryAdd(TStartState.Fingerprint, FrontierRepresentation.MakeFrontierRepresentation(TStartState, 0));
            }
            else
            {
                globalFrontier.TryAdd(TStartState.Fingerprint, FrontierTree.InitialFrontier);
            }

            

            DateTime loopEntryTime = DateTime.Now;
            
            // Now go parallel
            do
            {

                BoundedSearch.IncrementIterativeBound();

                //Console.WriteLine();
                DateTime queryStartTime = DateTime.Now;

                if (globalFrontier.Count() == 0)
                {
                    Console.Error.WriteLine("Skipping iteration. No frontier states to explore.");
                    //dfsResults = new List<KeyValuePair<int, IList<KeyValuePair<Fingerprint, PLINQResultEntry>>>>();
                }
                else
                {
                    //convert arraylist to linked list, so that we can do O(1) deletion as we explore the frontier

                    var Partitions = PartitionFrontierSet();
                    try
                    {
                        //dfsResults will be a list of key-value pairs, where each element in the list is a return value from
                        //ExploreFuncPLINQ
                        int count = Partitions.AsParallel().WithCancellation(cs.Token).WithDegreeOfParallelism(
                            Options.DegreeOfParallelism).Select(p => PExploreFrontierInMemory(p, Partitions)).Max();

                        //Push the Live states to dead states

                        LiveStates.Select((liveState) =>
                        {
                            if (liveState.Value.CompletelyExplored)
                            {
                                DeadStates.Insert(liveState.Key, 0);
                                //Interlocked.Add(ref numDistinctStates, 1);
                            }
                            else
                            {
                                DeadStates.Insert(liveState.Key, liveState.Value.ExploreIfDepthLowerThan);
                                //Interlocked.Add(ref numDistinctStates, 1);
                            }
                            return true;
                        }).ToList();
                        LiveStates.Clear();


                    }
                    catch (AggregateException e)
                    {
                        foreach (var ex in e.InnerExceptions)
                        {
                            if (!(ex is PLINQErrorEncounteredException))
                            {
                                throw (e);
                            }
                            else
                            {
                                continue;
                            }
                        }
                        var prevColor = Console.ForegroundColor;
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("##################");
                        Console.WriteLine("Check Failed");
                        Console.WriteLine("##################");
                        Console.ForegroundColor = prevColor;
                        
                        //Console.WriteLine("Total Time: {0}", DateTime.Now - loopEntryTime);
                    }
                    catch (PLINQErrorEncounteredException)
                    {
                        var prevColor = Console.ForegroundColor;
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("##################");
                        Console.WriteLine("Check Failed");
                        Console.WriteLine("##################");
                        Console.ForegroundColor = prevColor;
                        //Console.WriteLine("Total Time: {0}", DateTime.Now - loopEntryTime);
                    }

                    if ((SafetyErrors.ToArray().Length != 0 || AcceptingCycles.ToArray().Length != 0) && this.stopOnError)
                    {
                        // We have errors, and we have to stop on an error proceed no further
                        return false;
                    }
                }
                //Console.Error.WriteLine("Result Count {0}", dfsResults.Count);
                DateTime queryEndTime = DateTime.Now;



                TimeSpan delta = queryEndTime.Subtract(queryStartTime);

                if (Options.PrintStats)
                {
                    Console.WriteLine();
                    if (Options.IsSchedulerDecl)
                    {
                        Console.WriteLine("Delay Bound {0}", BoundedSearch.iterativeDelayCutOff);
                    }
                    else
                    {
                        Console.WriteLine("Depth Bound {0}", BoundedSearch.iterativeDepthCutoff);
                    }
                    Console.WriteLine("No. of Frontier {0}", globalFrontier.LongCount());
                    Console.WriteLine("No. of Distinct States {0}", numDistinctStates);
                    Console.WriteLine("Total Transitions: {0}", numTotalStates);
                    Console.WriteLine("Total Exploration time so far = " + queryEndTime.Subtract(loopEntryTime));
                    Console.WriteLine("Peak / Current Paged Mem Usage : {0} M/{1} M", System.Diagnostics.Process.GetCurrentProcess().PeakPagedMemorySize64 / (1 << 20), System.Diagnostics.Process.GetCurrentProcess().PagedMemorySize64 / (1 << 20));
                    Console.WriteLine("Peak / Current working set size: {0} M/{1} M", System.Diagnostics.Process.GetCurrentProcess().PeakWorkingSet64 / (1 << 20), System.Diagnostics.Process.GetCurrentProcess().WorkingSet64 / (1 << 20));

                }


            } while ((globalFrontier.LongCount() > 0) && !(BoundedSearch.checkIfFinalCutOffReached()));

            if (Options.Maceliveness)
            {
                Console.WriteLine("*******************************************************************");
                Console.WriteLine("Mace Liveness -- Finished Exhaustive Search");
                Console.WriteLine("Start Random Walk ......... ");
                Console.WriteLine("*******************************************************************");

                var frontierForRandomWalk = PartitionFrontierSet();
                try
                {
                    //dfsResults will be a list of key-value pairs, where each element in the list is a return value from
                    //ExploreFuncPLINQ
                    Options.IsRandomSearch = true;
                    int count = frontierForRandomWalk.AsParallel().WithCancellation(cs.Token).WithDegreeOfParallelism(
                        Options.DegreeOfParallelism).Select(p => RandomWalkMaceLiveness(p, frontierForRandomWalk)).Max();

                }
                catch (AggregateException e)
                {
                    foreach (var ex in e.InnerExceptions)
                    {
                        if (!(ex is PLINQErrorEncounteredException))
                        {
                            throw (e);
                        }
                        else
                        {
                            continue;
                        }
                    }
                    var prevColor = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("##################");
                    Console.WriteLine("Check Failed");
                    Console.WriteLine("##################");
                    Console.ForegroundColor = prevColor;
                    //Console.WriteLine("Total Time: {0}", DateTime.Now - loopEntryTime);
                }
                catch (PLINQErrorEncounteredException)
                {
                    var prevColor = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("##################");
                    Console.WriteLine("Check Failed");
                    Console.WriteLine("##################");
                    Console.ForegroundColor = prevColor;
                    //Console.WriteLine("Total Time: {0}", DateTime.Now - loopEntryTime);
                }
                if ((SafetyErrors.ToArray().Length != 0 || AcceptingCycles.ToArray().Length != 0) && this.stopOnError)
                {
                    // We have errors, and we have to stop on an error proceed no further
                    return false;
                }
            }
            return true;
        }

        private IList<KeyValuePair<IEnumerable<KeyValuePair<Fingerprint, FrontierNode>>, int>>
           PartitionFrontierSet ()
        {

            IList<KeyValuePair<IEnumerable<KeyValuePair<Fingerprint, FrontierNode>>, int>> retval =
                new List<KeyValuePair<IEnumerable<KeyValuePair<Fingerprint, FrontierNode>>, int>>();
            int TotalFrontiers = globalFrontier.Count();
            int NumFrontiersPerTask;
            if (TotalFrontiers <= Options.DegreeOfParallelism)
            {
                NumFrontiersPerTask = 1;
            }
            else
            {
                NumFrontiersPerTask = TotalFrontiers / Options.DegreeOfParallelism;
            }
            int RemFrontiers = TotalFrontiers % Options.DegreeOfParallelism;
            LinkedList<KeyValuePair<Fingerprint, FrontierNode>>[] CurrentList = new LinkedList<KeyValuePair<Fingerprint, FrontierNode>>[Options.DegreeOfParallelism];
            for (int i = 0; i < Options.DegreeOfParallelism; i++)
            {
                CurrentList[i] = new LinkedList<KeyValuePair<Fingerprint, FrontierNode>>();
            }

            var sorted_frontierset = globalFrontier.AsParallel().WithDegreeOfParallelism(Options.DegreeOfParallelism).OrderBy((n) => { return n.Value.MyNumber; });
            var grouped_sortedFrontier = sorted_frontierset.GroupBy((n) => { return n.Value.threadID; });


            int count = 0;

            foreach (var frontier in grouped_sortedFrontier)
            {
                foreach (var val in frontier)
                {

                    CurrentList[(count / NumFrontiersPerTask) % Options.DegreeOfParallelism].AddLast(val);
                    count++;
                }
            }
            for (int i = 0; i < Options.DegreeOfParallelism; i++)
            {
                retval.Add(new KeyValuePair<IEnumerable<KeyValuePair<Fingerprint, FrontierNode>>, int>(CurrentList[i], i));
                //CurrentList[i].Clear();
            }

            globalFrontier.Clear();
            System.GC.Collect();
            FrontierTree.countFrontier = 0;
            return (retval);
        }



        private int PExploreFrontierInMemory (KeyValuePair<IEnumerable<KeyValuePair<Fingerprint, FrontierNode>>, int> FrontierSetNum,
            IList<KeyValuePair<IEnumerable<KeyValuePair<Fingerprint, FrontierNode>>, int>> CompleteFrontierSetList)
        {

            Fingerprint currAcceptingState = null;
            TimeSpan GetTraversalTime = TimeSpan.Zero;
            int MySerialNum = FrontierSetNum.Value;
            int ExploreIfDepthLowerThanValue;
            bool CompletelyExploredValue;
            LinkedList<KeyValuePair<Fingerprint, FrontierNode>> FrontierSet = FrontierSetNum.Key
                as LinkedList<KeyValuePair<Fingerprint, FrontierNode>>;
            DateTime StartTime, EndTime;
            KeyValuePair<Fingerprint, FrontierNode> ResEntry;
            LinkedList<KeyValuePair<Fingerprint, FrontierNode>> OtherList = null;
            List<KeyValuePair<Fingerprint, FrontierNode>> StolenList = null;


            while (true)
            {
                lock (FrontierSet)
                {
                    if (FrontierSet.Count == 0)
                    {
                        // this means I have to steal work from other frontiers
                        ResEntry = new KeyValuePair<Fingerprint, FrontierNode>(null, null);
                    }
                    else
                    {
                        ResEntry = FrontierSet.First.Value;
                        FrontierSet.RemoveFirst();
                    }
                }
                if (ResEntry.Key == null)
                {
                    // My work list is empty, do work stealing if the option is enabled
                    if (Options.WorkStealAmount == -1 || Options.WorkStealAmount == 0)
                    {
                        // Work stealing disabled
                        break;
                    }
                    else
                    {
                        OtherList = null;
                        // Steal work from someone else
                        for (int i = 0; i < CompleteFrontierSetList.Count; ++i)
                        {
                            if (i == MySerialNum)
                            {
                                continue;
                            }
                            else
                            {
                                if ((CompleteFrontierSetList[i].Key as LinkedList<KeyValuePair<Fingerprint, FrontierNode>>).Count > 0)
                                {
                                    OtherList = CompleteFrontierSetList[i].Key as LinkedList<KeyValuePair<Fingerprint, FrontierNode>>;
                                    break;
                                }
                            }
                        }

                        if (OtherList == null)
                        {
                            // No one to steal work from, we're done!
                            break;
                        }
                        else
                        {
                            // Try stealing work from the other list
                            // Insert into a private list first to avoid races
                            // caused by someone else trying to steal work from me!
                            StolenList = new List<KeyValuePair<Fingerprint, FrontierNode>>();
                            lock (OtherList)
                            {
                                for (int i = 0; i < Options.WorkStealAmount; ++i)
                                {
                                    if (OtherList.Count == 0)
                                    {
                                        break;
                                    }
                                    StolenList.Add(OtherList.First.Value);
                                    OtherList.RemoveFirst();
                                }
                            }
                            // We've (hopefully) stolen some work, continue now.
                            // If at all this list has become empty between the time
                            // we checked that it was not empty to the time we locked it,
                            // then we'll try to steal again from some other list in the 
                            // next iteration. No harm, no foul
                            // Insert the stolen work into the main worklist

                            lock (FrontierSet)
                            {
                                foreach (KeyValuePair<Fingerprint, FrontierNode> kvp in StolenList)
                                {
                                    FrontierSet.AddLast(kvp);
                                }
                            }

                            continue;
                        }
                    }
                }

                //System.GC.Collect();
                FrontierRepresentation FrontEntry = (ResEntry.Value as FrontierRepresentation);
                Fingerprint fp = ResEntry.Key;


                if (!Options.IsSchedulerDecl && !MustExplore(fp, FrontEntry.Bounds,
                    out ExploreIfDepthLowerThanValue, out CompletelyExploredValue, false))
                {
                    // Don't need to explore further
                    continue;
                }
                // Check if this frontier state is past the cut-off for this iteration.
                // If so, don't explore, but simply pass the frontier as-is into the 
                // results. This will ensure that it stays in the right position in the 
                // postorder ordering of nodes which we maintain

                if (BoundedSearch.checkIfIterativeCutOffReached(FrontEntry.Bounds))
                {
                    //no need of adding it to the frontier if choice bound is reached
                    if(!(Options.BoundChoices && FrontEntry.Bounds.ChoiceCost >= BoundedSearch.choiceCutOff))
                        AddToFrontier(FrontEntry, fp, MySerialNum);
                    
                    continue;
                }
                System.Diagnostics.Stopwatch time3 = new System.Diagnostics.Stopwatch();
                time3.Start();
                StartTime = DateTime.Now;
                TraversalInfo startState;
                //If its the Initial state return it directly
                if (ResEntry.Key.Equals(InitialState.Fingerprint))
                {
                    startState = new ExecutionState((StateImpl)this.InitialState.SI.Clone(MySerialNum), null, null);
                }
                else
                {
                    startState = FrontEntry.GetTraversalInfo(this.InitialState.SI, MySerialNum);
                }
                EndTime = DateTime.Now;
                time3.Stop();
                GetTraversalTime = time3.Elapsed;
                totalTraversalTime[MySerialNum] += GetTraversalTime;
                //Cruncher.ReplayTime = Cruncher.ReplayTime.Add(EndTime - StartTime);

                //Console.WriteLine("Thread {2} Exploring with StartState: {0} and Serial Number {1}", startState.Fingerprint, startState.GetStateImpl().MySerialNum,
                //    System.Threading.Tasks.Task.CurrentId);

                VisitState(startState.Fingerprint, startState.Bounds.Depth, false, false);
                Stack<IDBDFSStackEntry> LocalStack = new Stack<IDBDFSStackEntry>();
                LocalStack.Push(new IDBDFSStackEntry(startState));

                //do bounded depth DFS with local stack
                while (LocalStack.Count > 0)
                {

                    // We might be unwinding, check if we really need to explore this state or just pop it!
                    if (LocalStack.Peek().DescendentsLeftToCover == 0)
                    {
                        IDBDFSStackEntry MyStackEntry = LocalStack.Pop();
                        // Also, this state and its descendents are completely explored
                        // mark it as such in the state table
                        VisitState(MyStackEntry.ti, MyStackEntry.ExploreIfDepthLowerThan, true);
                        // Propagate the fact that this subtree is completely done upward
                        if (LocalStack.Count > 0)
                        {
                            LocalStack.Peek().DescendentsLeftToCover--;
                            // Set the ExploreIfDepthLowerThan only if its not already set
                            if (LocalStack.Peek().ExploreIfDepthLowerThan == -1)
                            {
                                LocalStack.Peek().ExploreIfDepthLowerThan = 0;
                            }
                        }
                        continue;
                    }

                    // Okay, we have more descendents left to cover
                    TraversalInfo I = (TraversalInfo)LocalStack.Peek().ti;

                    //Console.Error.WriteLine("Exploring State: {0}, Depth {1}", I.Fingerprint.ToString(), I.Depth);


                    MaxDepth = Math.Max(MaxDepth, I.Bounds.Depth);

                    // Add to the frontier only if we're greater than the cutoff and we're fingerprinted.

                    if (BoundedSearch.checkIfIterativeCutOffReached(I.Bounds) && I.IsFingerPrinted)
                    {
                        // We've reached the depth cut-off. Save this to the frontier
                        // and continue
                        //no need of adding it to the frontier if the final choice cutoff has reached
                        if (!(Options.BoundChoices && FrontEntry.Bounds.ChoiceCost >= BoundedSearch.choiceCutOff))
                            AddToFrontier(I, MySerialNum);
                        //I.DiscardStateImpl();
                        LocalStack.Pop();

                        // Update my parent's explore if depth lower than value
                        if (LocalStack.Count > 0)
                            LocalStack.Peek().ExploreIfDepthLowerThan = I.Bounds.Depth - 1;
                        continue;
                    }

                    // Okay, this state is not at the depth cutoff yet.
                    // So continue exploration;
                    TraversalInfo newI = I.GetNextSuccessor(BoundedSearch);
                    if (newI == null)
                    {
                        //Start the red search
                        if (Options.CheckLiveNess)
                        {
                            if (!I.MagicBit && I.IsAcceptingState)
                            {
                                LocalStack.Pop();
                                //set the magic bit of that state to true
                                var newRedI = SetMagicBit(I);
                                LocalStack.Push(new IDBDFSStackEntry(newRedI));
                                currAcceptingState = I.Fingerprint;
                                continue;
                            }
                        }
                        IDBDFSStackEntry MyStackEntry;
                        //I.DiscardStateImpl();
                        MyStackEntry = LocalStack.Pop();
                        // We can't have DescendentsLeftToCover == 0, since it would 
                        // have been caught right at the beginning, hence, this 
                        // must be because we haven't gone down the entire way, i.e, we've
                        // reached the cutoff at least on one path starting from this node
                        // Before throwing it away however, update the ExploreIfDepthLessThan for this
                        // state

                        VisitState(MyStackEntry.ti, MyStackEntry.ExploreIfDepthLowerThan, false);


                        // Also update my parent's ExploreIfDepthLowerThan if required. This needs to
                        // be the max of all the descendents of that state. So update only if what we have
                        // is greater than what already exists. i.e., we don't want to REDUCE ExploreIfDepthLowerThan
                        if (!Options.IsSchedulerDecl && LocalStack.Count() > 0)
                        {
                            // Take care of the case where the parent's ExploreIfDepthLowerThan has NEVER been set
                            // and we're at ExploreIFDepthLowerThan = 0
                            if (LocalStack.Peek().ExploreIfDepthLowerThan == -1 && MyStackEntry.ExploreIfDepthLowerThan == 0)
                            {
                                LocalStack.Peek().ExploreIfDepthLowerThan = 0;
                            }
                            else if (LocalStack.Peek().ExploreIfDepthLowerThan < MyStackEntry.ExploreIfDepthLowerThan - 1)
                            {
                                if (MyStackEntry.ExploreIfDepthLowerThan == 0)
                                {
                                    LocalStack.Peek().ExploreIfDepthLowerThan = 0;
                                }
                                else
                                {
                                    LocalStack.Peek().ExploreIfDepthLowerThan = MyStackEntry.ExploreIfDepthLowerThan - 1;
                                }
                            }
                        }

                        continue;
                    }


                    //check if we found a cycle
                    if (Options.CheckLiveNess)
                    {
                        if (newI.MagicBit && newI.Fingerprint.Equals(currAcceptingState) && newI.IsAcceptingState)
                        {
                            lock (this)
                            {
                                AcceptingCycles.Add(newI.GenerateNonCompactTrace());
                                this.lastErrorFound = CheckerResult.AcceptanceCyleFound;
                            }

                            if (StopOnError)
                            {
                                cs.Cancel(true);
                                throw new PLINQErrorEncounteredException();
                            }
                        }
                    }

                    #region Check DFS Stack Length Exception
                    if (Options.CheckDFSStackLength && LocalStack.Count > MaxDFSStackLength)
                    {
                        newI = newI.ThrowStackOverFlowException();
                        DFSStackOverFlowError = true;
                    }
                    #endregion

                    TerminalState ts = newI as TerminalState;
                    if ((ts != null))
                    {
                        if (ts.IsErroneous)
                        {
                            lock (this)
                            {
                                //                                bool OldCT = Options.CompactTraces;
                                //                                Options.CompactTraces = false;
                                SafetyErrors.Add(newI.GenerateNonCompactTrace());
                                //                                Options.CompactTraces = OldCT;
                                this.lastErrorFound = newI.ErrorCode;
                                /*Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine(newI.Exception.ToString());
                                Console.ResetColor();
                                Environment.Exit(0);*/
                            }

                            if (stopOnError)
                            {
                                cs.Cancel(true);
                                throw new PLINQErrorEncounteredException();
                                //return new KeyValuePair<int, IList<KeyValuePair<Fingerprint, PLINQResultEntry>>>(MySerialNum, DFSResults);
                            }
                        }
                        if (!Options.IsSchedulerDecl)
                        {
                            // Update that the state at the top of stack has one
                            // less descendent to cover, since we are fully covered
                            LocalStack.Peek().DescendentsLeftToCover--;
                            // Also update the ExploreIfLessThan for the parent
                            // but only if it has never been set before. This
                            // ensures that IF the parent has only one successor
                            // i.e. ts right now, it still will have explore if
                            // ExploreIfDepthLowerThan set to something other than
                            // -1
                            if (LocalStack.Peek().ExploreIfDepthLowerThan == -1)
                            {
                                LocalStack.Peek().ExploreIfDepthLowerThan = 0;
                            }
                        }
                        continue;
                    }

                    if (MustExplore(newI, out ExploreIfDepthLowerThanValue,
                                    out CompletelyExploredValue))
                    {
                        // Ensure that states are depthcutoff are not 
                        // added to the statetable, since they will be 
                        // added to the frontiertable in the next iteration
                        // anyway
                        if (!BoundedSearch.checkIfIterativeCutOffReached(newI.Bounds))
                        {
                            VisitState(newI, newI.Bounds.Depth, false);
                        }

                        LocalStack.Push(new IDBDFSStackEntry(newI));


                        continue;
                    }
                    else
                    {
                        if (!Options.IsSchedulerDecl)
                        {
                            // Check the reason why we don't have to explore this state at this depth
                            if (CompletelyExploredValue)
                            {
                                // The subtree from this state has already been completely explored
                                // Indicate that this is the case to the parent, by decrementing the 
                                // number of its descendents that are yet to be explored
                                LocalStack.Peek().DescendentsLeftToCover--;
                            }
                            else // if (ExploreIfDepthLessThanValue < newI.Depth)
                            {
                                // Propagate that we need to explore only if we encounter the 
                                // parent state one state below the ExploreIfDepthLowerThanValue for this state
                                int StackExploreIfLowerThan = LocalStack.Peek().ExploreIfDepthLowerThan;

                                if (ExploreIfDepthLowerThanValue <= 0)
                                {
                                    System.Diagnostics.Debug.Assert(ExploreIfDepthLowerThanValue == 0);
                                    ExploreIfDepthLowerThanValue = 1;
                                }

                                if ((ExploreIfDepthLowerThanValue - 1) > StackExploreIfLowerThan)
                                {
                                    LocalStack.Peek().ExploreIfDepthLowerThan = (ExploreIfDepthLowerThanValue - 1);
                                }
                            }
                        }
                        continue;
                    }
                }
            }
            // Return a composite result entry
            return globalFrontier.Count();
        }

        #endregion

        #region RandomWalk MaceMC
        private int RandomWalkMaceLiveness(KeyValuePair<IEnumerable<KeyValuePair<Fingerprint, FrontierNode>>, int> FrontierSetNum,
            IList<KeyValuePair<IEnumerable<KeyValuePair<Fingerprint, FrontierNode>>, int>> CompleteFrontierSetList)
        {
            TimeSpan GetTraversalTime = TimeSpan.Zero;
            int MySerialNum = FrontierSetNum.Value;
            LinkedList<KeyValuePair<Fingerprint, FrontierNode>> FrontierSet = FrontierSetNum.Key
                as LinkedList<KeyValuePair<Fingerprint, FrontierNode>>;
            DateTime StartTime, EndTime;
            KeyValuePair<Fingerprint, FrontierNode> ResEntry;
            LinkedList<KeyValuePair<Fingerprint, FrontierNode>> OtherList = null;
            List<KeyValuePair<Fingerprint, FrontierNode>> StolenList = null;
            int lastSeenStableState = 0;

            while (true)
            {
                lock (FrontierSet)
                {
                    if (FrontierSet.Count == 0)
                    {
                        // this means I have to steal work from other frontiers
                        ResEntry = new KeyValuePair<Fingerprint, FrontierNode>(null, null);
                    }
                    else
                    {
                        ResEntry = FrontierSet.First.Value;
                        FrontierSet.RemoveFirst();
                    }
                }
                if (ResEntry.Key == null)
                {
                    // My work list is empty, do work stealing if the option is enabled
                    if (Options.WorkStealAmount == -1 || Options.WorkStealAmount == 0)
                    {
                        // Work stealing disabled
                        break;
                    }
                    else
                    {
                        OtherList = null;
                        // Steal work from someone else
                        for (int i = 0; i < CompleteFrontierSetList.Count; ++i)
                        {
                            if (i == MySerialNum)
                            {
                                continue;
                            }
                            else
                            {
                                if ((CompleteFrontierSetList[i].Key as LinkedList<KeyValuePair<Fingerprint, FrontierNode>>).Count > 0)
                                {
                                    OtherList = CompleteFrontierSetList[i].Key as LinkedList<KeyValuePair<Fingerprint, FrontierNode>>;
                                    break;
                                }
                            }
                        }

                        if (OtherList == null)
                        {
                            // No one to steal work from, we're done!
                            break;
                        }
                        else
                        {
                            // Try stealing work from the other list
                            // Insert into a private list first to avoid races
                            // caused by someone else trying to steal work from me!
                            StolenList = new List<KeyValuePair<Fingerprint, FrontierNode>>();
                            lock (OtherList)
                            {
                                for (int i = 0; i < Options.WorkStealAmount; ++i)
                                {
                                    if (OtherList.Count == 0)
                                    {
                                        break;
                                    }
                                    StolenList.Add(OtherList.First.Value);
                                    OtherList.RemoveFirst();
                                }
                            }
                            // We've (hopefully) stolen some work, continue now.
                            // If at all this list has become empty between the time
                            // we checked that it was not empty to the time we locked it,
                            // then we'll try to steal again from some other list in the 
                            // next iteration. No harm, no foul
                            // Insert the stolen work into the main worklist

                            lock (FrontierSet)
                            {
                                foreach (KeyValuePair<Fingerprint, FrontierNode> kvp in StolenList)
                                {
                                    FrontierSet.AddLast(kvp);
                                }
                            }

                            continue;
                        }
                    }
                }

                //System.GC.Collect();
                FrontierRepresentation FrontEntry = (ResEntry.Value as FrontierRepresentation);
                Fingerprint fp = ResEntry.Key;

                
                StartTime = DateTime.Now;
                TraversalInfo startState;
                //If its the Initial state return it directly
                if (ResEntry.Key.Equals(InitialState.Fingerprint))
                {
                    startState = new ExecutionState((StateImpl)this.InitialState.SI.Clone(MySerialNum), null, null);
                }
                else
                {
                    startState = FrontEntry.GetTraversalInfo(this.InitialState.SI, MySerialNum);
                }
                
                Stack<IDBDFSStackEntry> LocalStack = new Stack<IDBDFSStackEntry>();
                LocalStack.Push(new IDBDFSStackEntry(startState));

                //do bounded depth DFS with local stack
                while (LocalStack.Count > 0)
                {
                    TraversalInfo I = (TraversalInfo)LocalStack.Peek().ti;
                    TraversalInfo newI = I.RandomSuccessor();
                    if(newI == null)
                    {
                        LocalStack.Pop();
                        continue;
                    }
                    if(I.IsAcceptingState)
                    {
                        lastSeenStableState = 0;
                    }
                    //check if we found a cycle
                    if (lastSeenStableState > MaceLiveness.RandomWalkBound)
                    {
                        lock (this)
                        {
                            AcceptingCycles.Add(newI.GenerateNonCompactTrace());
                            this.lastErrorFound = CheckerResult.AcceptanceCyleFound;
                        }

                        if (StopOnError)
                        {
                            cs.Cancel(true);
                            throw new PLINQErrorEncounteredException();
                        }
                       
                    }

                    TerminalState ts = newI as TerminalState;
                    if ((ts != null))
                    {
                        if (ts.IsErroneous)
                        {
                            lock (this)
                            {
                             
                                SafetyErrors.Add(newI.GenerateNonCompactTrace());
                                this.lastErrorFound = newI.ErrorCode;
                             
                            }

                            if (stopOnError)
                            {
                                cs.Cancel(true);
                                throw new PLINQErrorEncounteredException();
                            }
                        }
                        continue;
                    }
                    if (MaceLiveness.FinalBound < newI.Bounds.Delay || MaceLiveness.FinalBound < newI.Bounds.Depth)
                    {
                        continue;
                    }
                    else
                    {
                        LocalStack.Push(new IDBDFSStackEntry(newI));
                        lastSeenStableState++;
                        continue;
                    }
                }
            }
            return globalFrontier.Count();
        }
        #endregion

        #region Frontier To Disk
        System.Threading.Tasks.Task[] z_Workers;
        System.Threading.Tasks.Task[] readerWorkers;
        System.Threading.Tasks.Task[] writerWorkers;

        bool readFrontierFromMemory = true;
        int frontierCounter;
        int BlockSize = 5120;

        public void PExplorer (object obj)
        {
            Fingerprint currAcceptingState = null;
            TimeSpan GetTraversalTime = TimeSpan.Zero;
            int MySerialNum = (int)obj;
            int ExploreIfDepthLowerThanValue;
            bool CompletelyExploredValue;

            FrontierWorkItem currFrontierBlock = new FrontierWorkItem();



            foreach (var FrontierB in currFrontier.GetConsumingEnumerable())
            {
                foreach (var ResEntry in FrontierB.FrontierBlock)
                {
                    //System.GC.Collect();
                    FrontierRepresentation FrontEntry = (ResEntry as FrontierRepresentation);

                    TraversalInfo startState;
                    //If its the Initial state return it directly

                    /*if (ResEntry.Key.Equals(InitialState.Fingerprint))
                    {
                        startState = new ExecutionState((StateImpl)this.InitialState.SI.Clone(MySerialNum), null, null);
                    }
                    else*/
                    {
                        startState = FrontEntry.GetTraversalInfo(this.InitialState.SI, MySerialNum);
                    }

                    Fingerprint fp = startState.Fingerprint;

                    if (!Options.IsSchedulerDecl && !MustExplore(fp, FrontEntry.Bounds,
                        out ExploreIfDepthLowerThanValue, out CompletelyExploredValue, false))
                    {
                        // Don't need to explore further
                        continue;
                    }
                    // Check if this frontier state is past the cut-off for this iteration.
                    // If so, don't explore, but simply pass the frontier as-is into the 
                    // results. This will ensure that it stays in the right position in the 
                    // postorder ordering of nodes which we maintain

                    if (BoundedSearch.checkIfIterativeCutOffReached(FrontEntry.Bounds))
                    {
                        if (currFrontierBlock.Count() < BlockSize)
                        {
                            AddToFrontierBlock(FrontEntry, fp, currFrontierBlock);
                        }
                        else
                        {
                            newFrontier.Add(currFrontierBlock);
                            currFrontierBlock = new FrontierWorkItem();
                            AddToFrontierBlock(FrontEntry, fp, currFrontierBlock);
                        }
                        continue;
                    }



                    //Cruncher.ReplayTime = Cruncher.ReplayTime.Add(EndTime - StartTime);

                    //Console.WriteLine("Thread {2} Exploring with StartState: {0} and Serial Number {1}", startState.Fingerprint, startState.GetStateImpl().MySerialNum,
                    //    System.Threading.Tasks.Task.CurrentId);

                    VisitState(startState.Fingerprint, startState.Bounds.Depth, false, false);
                    Stack<IDBDFSStackEntry> LocalStack = new Stack<IDBDFSStackEntry>();
                    LocalStack.Push(new IDBDFSStackEntry(startState));

                    //do bounded depth DFS with local stack
                    while (LocalStack.Count > 0)
                    {

                        // We might be unwinding, check if we really need to explore this state or just pop it!
                        if (LocalStack.Peek().DescendentsLeftToCover == 0)
                        {
                            IDBDFSStackEntry MyStackEntry = LocalStack.Pop();
                            // Also, this state and its descendents are completely explored
                            // mark it as such in the state table
                            VisitState(MyStackEntry.ti, MyStackEntry.ExploreIfDepthLowerThan, true);
                            // Propagate the fact that this subtree is completely done upward
                            if (LocalStack.Count > 0)
                            {
                                LocalStack.Peek().DescendentsLeftToCover--;
                                // Set the ExploreIfDepthLowerThan only if its not already set
                                if (LocalStack.Peek().ExploreIfDepthLowerThan == -1)
                                {
                                    LocalStack.Peek().ExploreIfDepthLowerThan = 0;
                                }
                            }
                            continue;
                        }

                        // Okay, we have more descendents left to cover
                        TraversalInfo I = (TraversalInfo)LocalStack.Peek().ti;

                        //Console.Error.WriteLine("Exploring State: {0}, Depth {1}", I.Fingerprint.ToString(), I.Depth);


                        MaxDepth = Math.Max(MaxDepth, I.Bounds.Depth);

                        // Add to the frontier only if we're greater than the cutoff and we're fingerprinted.

                        if (BoundedSearch.checkIfIterativeCutOffReached(I.Bounds) && I.IsFingerPrinted && (!Options.FingerprintSingleTransitionStates || (Options.FingerprintSingleTransitionStates && I.HasMultipleSuccessors)))
                        {
                            // We've reached the depth cut-off. Save this to the frontier
                            // and continue
                            if (currFrontierBlock.Count() < BlockSize)
                            {
                                AddToFrontierBlock(I, currFrontierBlock);
                            }
                            else
                            {
                                newFrontier.Add(currFrontierBlock);
                                currFrontierBlock = new FrontierWorkItem();
                                AddToFrontierBlock(I, currFrontierBlock);
                            }
                            //I.DiscardStateImpl();
                            LocalStack.Pop();

                            // Update my parent's explore if depth lower than value
                            if (LocalStack.Count > 0)
                                LocalStack.Peek().ExploreIfDepthLowerThan = I.Bounds.Depth - 1;
                            continue;
                        }

                        // Okay, this state is not at the depth cutoff yet.
                        // So continue exploration;
                        TraversalInfo newI = I.GetNextSuccessor(BoundedSearch);
                        if (newI == null)
                        {
                            //Start the red search
                            if (Options.CheckLiveNess)
                            {
                                if (!I.MagicBit && I.IsAcceptingState)
                                {
                                    LocalStack.Pop();
                                    //set the magic bit of that state to true
                                    var newRedI = SetMagicBit(I);
                                    LocalStack.Push(new IDBDFSStackEntry(newRedI));
                                    currAcceptingState = I.Fingerprint;
                                    continue;
                                }
                            }
                            IDBDFSStackEntry MyStackEntry;
                            //I.DiscardStateImpl();
                            MyStackEntry = LocalStack.Pop();
                            // We can't have DescendentsLeftToCover == 0, since it would 
                            // have been caught right at the beginning, hence, this 
                            // must be because we haven't gone down the entire way, i.e, we've
                            // reached the cutoff at least on one path starting from this node
                            // Before throwing it away however, update the ExploreIfDepthLessThan for this
                            // state

                            VisitState(MyStackEntry.ti, MyStackEntry.ExploreIfDepthLowerThan, false);


                            // Also update my parent's ExploreIfDepthLowerThan if required. This needs to
                            // be the max of all the descendents of that state. So update only if what we have
                            // is greater than what already exists. i.e., we don't want to REDUCE ExploreIfDepthLowerThan
                            if (!Options.IsSchedulerDecl && LocalStack.Count() > 0)
                            {
                                // Take care of the case where the parent's ExploreIfDepthLowerThan has NEVER been set
                                // and we're at ExploreIFDepthLowerThan = 0
                                if (LocalStack.Peek().ExploreIfDepthLowerThan == -1 && MyStackEntry.ExploreIfDepthLowerThan == 0)
                                {
                                    LocalStack.Peek().ExploreIfDepthLowerThan = 0;
                                }
                                else if (LocalStack.Peek().ExploreIfDepthLowerThan < MyStackEntry.ExploreIfDepthLowerThan - 1)
                                {
                                    if (MyStackEntry.ExploreIfDepthLowerThan == 0)
                                    {
                                        LocalStack.Peek().ExploreIfDepthLowerThan = 0;
                                    }
                                    else
                                    {
                                        LocalStack.Peek().ExploreIfDepthLowerThan = MyStackEntry.ExploreIfDepthLowerThan - 1;
                                    }
                                }
                            }

                            continue;
                        }


                        //check if we found a cycle
                        if (Options.CheckLiveNess)
                        {
                            if (newI.MagicBit && newI.Fingerprint.Equals(currAcceptingState) && newI.IsAcceptingState)
                            {
                                lock (this)
                                {
                                    AcceptingCycles.Add(newI.GenerateNonCompactTrace());
                                    this.lastErrorFound = CheckerResult.AcceptanceCyleFound;
                                }

                                if (StopOnError)
                                {
                                    cs.Cancel(true);
                                    throw new PLINQErrorEncounteredException();
                                }
                            }
                        }

                        #region Check DFS Stack Length Exception
                        if (Options.CheckDFSStackLength && LocalStack.Count > MaxDFSStackLength)
                        {
                            newI = newI.ThrowStackOverFlowException();
                            DFSStackOverFlowError = true;
                        }
                        #endregion

                        TerminalState ts = newI as TerminalState;
                        if ((ts != null))
                        {
                            if (ts.IsErroneous)
                            {
                                lock (this)
                                {
                                    //                                bool OldCT = Options.CompactTraces;
                                    //                                Options.CompactTraces = false;
                                    SafetyErrors.Add(newI.GenerateNonCompactTrace());
                                    //                                Options.CompactTraces = OldCT;
                                    this.lastErrorFound = newI.ErrorCode;
                                    /*Console.ForegroundColor = ConsoleColor.Red;
                                    Console.WriteLine(newI.Exception.ToString());
                                    Console.ResetColor();
                                    Environment.Exit(0);*/
                                }

                                if (stopOnError)
                                {
                                    cs.Cancel(true);
                                    throw new PLINQErrorEncounteredException();
                                    //return new KeyValuePair<int, IList<KeyValuePair<Fingerprint, PLINQResultEntry>>>(MySerialNum, DFSResults);
                                }
                            }
                            if (!Options.IsSchedulerDecl)
                            {
                                // Update that the state at the top of stack has one
                                // less descendent to cover, since we are fully covered
                                LocalStack.Peek().DescendentsLeftToCover--;
                                // Also update the ExploreIfLessThan for the parent
                                // but only if it has never been set before. This
                                // ensures that IF the parent has only one successor
                                // i.e. ts right now, it still will have explore if
                                // ExploreIfDepthLowerThan set to something other than
                                // -1
                                if (LocalStack.Peek().ExploreIfDepthLowerThan == -1)
                                {
                                    LocalStack.Peek().ExploreIfDepthLowerThan = 0;
                                }
                            }
                            continue;
                        }

                        if (MustExplore(newI, out ExploreIfDepthLowerThanValue,
                                        out CompletelyExploredValue))
                        {
                            // Ensure that states are depthcutoff are not 
                            // added to the statetable, since they will be 
                            // added to the frontiertable in the next iteration
                            // anyway
                            if (!BoundedSearch.checkIfIterativeCutOffReached(newI.Bounds))
                            {
                                VisitState(newI, newI.Bounds.Depth, false);
                            }

                            LocalStack.Push(new IDBDFSStackEntry(newI));


                            continue;
                        }
                        else
                        {
                            if (!Options.IsSchedulerDecl)
                            {
                                // Check the reason why we don't have to explore this state at this depth
                                if (CompletelyExploredValue)
                                {
                                    // The subtree from this state has already been completely explored
                                    // Indicate that this is the case to the parent, by decrementing the 
                                    // number of its descendents that are yet to be explored
                                    LocalStack.Peek().DescendentsLeftToCover--;
                                }
                                else // if (ExploreIfDepthLessThanValue < newI.Depth)
                                {
                                    // Propagate that we need to explore only if we encounter the 
                                    // parent state one state below the ExploreIfDepthLowerThanValue for this state
                                    int StackExploreIfLowerThan = LocalStack.Peek().ExploreIfDepthLowerThan;

                                    if (ExploreIfDepthLowerThanValue <= 0)
                                    {
                                        System.Diagnostics.Debug.Assert(ExploreIfDepthLowerThanValue == 0);
                                        ExploreIfDepthLowerThanValue = 1;
                                    }

                                    if ((ExploreIfDepthLowerThanValue - 1) > StackExploreIfLowerThan)
                                    {
                                        LocalStack.Peek().ExploreIfDepthLowerThan = (ExploreIfDepthLowerThanValue - 1);
                                    }
                                }
                            }
                            continue;
                        }
                    }
                }



            }
            newFrontier.Add(currFrontierBlock);
        }

        public void FrontierReader (object obj)
        {
            string fileName = (string)obj;
            if (readFrontierFromMemory)
            {
                int counter = 0;
                FrontierWorkItem fw = new FrontierWorkItem();
                foreach (var frontier in globalFrontier)
                {
                    if (counter < BlockSize)
                    {
                        fw.Add((TraceFrontier)frontier.Value);
                    }
                    else
                    {
                        currFrontier.Add(fw);
                        fw = new FrontierWorkItem();
                        fw.Add((TraceFrontier)frontier.Value);
                        counter = 0;
                    }
                }

                if (fw.Count() > 0)
                {
                    currFrontier.Add(fw);
                }
                readFrontierFromMemory = false;
                globalFrontier.Clear();

            }
            else
            {
                int counter = 0;
                FrontierWorkItem workItem = new FrontierWorkItem();

                //read from file currFrontier.z
                if (File.Exists(fileName))
                {
                    //read from the file
                    Stream sreader = File.Open(fileName, FileMode.Open);
                    BinaryReader bReader = new BinaryReader(sreader);

                    while (sreader.Position < sreader.Length)
                    {
                        while (counter < BlockSize && sreader.Position < sreader.Length)
                        {
                            TraceFrontier frontier = new TraceFrontier();
                            frontier.Deserialize(sreader);
                            workItem.Add(frontier);
                            counter++;
                        }
                        if (counter == BlockSize)
                        {
                            currFrontier.Add(workItem);
                            counter = 0;
                            workItem = new FrontierWorkItem();
                        }
                    }
                    if (workItem.Count() > 0)
                    {
                        currFrontier.Add(workItem);
                    }
                    sreader.Close();
                }
            }

        }

        public void FrontierWriter (object obj)
        {
            string fileName = (string)obj;

            Stream writeStream = File.Open(fileName, FileMode.Create);
            BinaryWriter bWriter = new BinaryWriter(writeStream);
            foreach (var FrontierB in newFrontier.GetConsumingEnumerable())
            {
                foreach (var f in FrontierB.FrontierBlock)
                {
                    f.Serialize(writeStream);
                    Interlocked.Increment(ref frontierCounter);
                }
            }
            writeStream.Close();

        }


        public void InitializeWorkForce ()
        {
            //start all worker threads
            frontierCounter = 0;
            currFrontier = new BlockingCollection<FrontierWorkItem>(4* numberWorker);
            newFrontier = new BlockingCollection<FrontierWorkItem>(40 * numberWorker);
            frontierSet = new HashSet<Fingerprint>();
            z_Workers = new System.Threading.Tasks.Task[Options.DegreeOfParallelism];
            for (int i = 0; i < z_Workers.Length; i++)
            {
                object obj = (object)i;
                z_Workers[i] = System.Threading.Tasks.Task.Factory.StartNew(PExplorer, obj);
                System.Threading.Thread.Sleep(10);
            }

            readerWorkers = new System.Threading.Tasks.Task[Options.DegreeOfParallelism];
            writerWorkers = new System.Threading.Tasks.Task[Options.DegreeOfParallelism];
            for (int i = 0; i < readerWorkers.Length; i++)
            {
                readerWorkers[i] = System.Threading.Tasks.Task.Factory.StartNew(FrontierReader, "i_" + i.ToString());
                writerWorkers[i] = System.Threading.Tasks.Task.Factory.StartNew(FrontierWriter, "o_" + i.ToString());
                System.Threading.Thread.Sleep(10);
            }

            System.Threading.Tasks.Task.WaitAll(readerWorkers);
            currFrontier.CompleteAdding();
            //wait for all worker threads to finish
            System.Threading.Tasks.Task.WaitAll(z_Workers);
            //Finished adding all frontiers
            newFrontier.CompleteAdding();

            System.Threading.Tasks.Task.WaitAll(writerWorkers);
        }
        public bool DoDFSFrontierToDisk (TCancel cancel)
        {
            DeadStates.Init();

            IEnumerable<KeyValuePair<Fingerprint, FrontierNode>> CurrentFrontier;
            numDistinctStates = numTotalStates = 0;
            CurrentFrontier = new LinkedList<KeyValuePair<Fingerprint, FrontierNode>>();

            (CurrentFrontier as LinkedList<KeyValuePair<Fingerprint, FrontierNode>>).AddLast(
                    new KeyValuePair<Fingerprint, FrontierNode>(TStartState.Fingerprint, FrontierRepresentation.MakeFrontierRepresentation(TStartState, 0)));

            // Explore a bit before going parallel

            DateTime loopEntryTime = DateTime.Now;
            try
            {
                // Wrap it in a key value pair for the sequential exploration
                KeyValuePair<IEnumerable<KeyValuePair<Fingerprint, FrontierNode>>, int> InitSeqFrontier =
                    new KeyValuePair<IEnumerable<KeyValuePair<Fingerprint, FrontierNode>>, int>(CurrentFrontier, 0);
                // Wrap this in another list for the second param
                IList<KeyValuePair<IEnumerable<KeyValuePair<Fingerprint, FrontierNode>>, int>> CompleteFrontierSet =
                    new List<KeyValuePair<IEnumerable<KeyValuePair<Fingerprint, FrontierNode>>, int>>();
                CompleteFrontierSet.Add(InitSeqFrontier);

                int count = PExploreFrontierInMemory(InitSeqFrontier, CompleteFrontierSet);
                LiveStates.Select((liveState) =>
                {
                    if (liveState.Value.CompletelyExplored)
                    {
                        DeadStates.Insert(liveState.Key, 0);
                    }
                    else
                    {
                        DeadStates.Insert(liveState.Key, liveState.Value.ExploreIfDepthLowerThan);
                    }
                    return true;
                }).ToList();
                LiveStates.Clear();

                if (Options.PrintStats)
                {
                    Console.WriteLine();
                    if (Options.IsSchedulerDecl)
                    {
                        Console.WriteLine("Delay Bound {0}", BoundedSearch.iterativeDelayCutOff);
                    }
                    else
                    {
                        Console.WriteLine("Depth Bound {0}", BoundedSearch.iterativeDepthCutoff);
                    }
                    Console.WriteLine("No. of Frontier {0}", globalFrontier.LongCount());
                    Console.WriteLine("No. of Distinct States {0}", numDistinctStates);
                    Console.WriteLine("Total Transitions: {0}", numTotalStates);
                    Console.WriteLine("Peak / Current Paged Mem Usage : {0} M/{1} M", System.Diagnostics.Process.GetCurrentProcess().PeakPagedMemorySize64 / (1 << 20), System.Diagnostics.Process.GetCurrentProcess().PagedMemorySize64 / (1 << 20));
                    Console.WriteLine("Peak / Current working set size: {0} M/{1} M", System.Diagnostics.Process.GetCurrentProcess().PeakWorkingSet64 / (1 << 20), System.Diagnostics.Process.GetCurrentProcess().WorkingSet64 / (1 << 20));

                }
            }
            catch (AggregateException e)
            {
                foreach (var ex in e.InnerExceptions)
                {
                    if (!(ex is PLINQErrorEncounteredException))
                    {
                        throw (e);
                    }
                    else
                    {
                        continue;
                    }
                }
                var prevColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("##################");
                Console.WriteLine("Check Failed");
                Console.WriteLine("##################");
                Console.ForegroundColor = prevColor;
                //Console.WriteLine("Total Time: {0}", DateTime.Now - loopEntryTime);
                if (this.StopOnError)
                {
                    return false;
                }

            }
            catch (PLINQErrorEncounteredException)
            {
                var prevColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("##################");
                Console.WriteLine("Check Failed");
                Console.WriteLine("##################");
                Console.ForegroundColor = prevColor;
                //Console.WriteLine("Total Time: {0}", DateTime.Now - loopEntryTime);
                if (this.StopOnError)
                {
                    return false;
                }
            }

            // Now go parallel and push frontiers to disk

            //before going parallel 

            /*File.Delete(input_0);
            File.Delete(input_1);
            File.Delete(output_0);
            File.Delete(output_1);*/
            for (int i = 0; i < 2 * Options.DegreeOfParallelism; i++)
            {
                File.Delete("i_" + i.ToString());
                File.Delete("o_" + i.ToString());
            }
            do
            {


                BoundedSearch.IncrementIterativeBound();

                //Console.WriteLine();
                DateTime queryStartTime = DateTime.Now;

                if (globalFrontier.Count() == 0 && frontierCounter == 0)
                {
                    Console.Error.WriteLine("Skipping iteration. No frontier states to explore.");
                    //dfsResults = new List<KeyValuePair<int, IList<KeyValuePair<Fingerprint, PLINQResultEntry>>>>();
                }
                else
                {
                    //convert arraylist to linked list, so that we can do O(1) deletion as we explore the frontier


                    try
                    {
                        // move the new frontier to current frontier
                        for (int i = 0; i < 2 * Options.DegreeOfParallelism; i++)
                        {
                            File.Delete("i_" + i.ToString());
                            if (File.Exists("o_" + i.ToString()))
                                File.Move("o_" + i.ToString(), "i_" + i.ToString());
                        }




                        InitializeWorkForce();
                        //Push the Live states to dead states

                        LiveStates.Select((liveState) =>
                        {
                            if (liveState.Value.CompletelyExplored)
                            {
                                DeadStates.Insert(liveState.Key, 0);
                                //Interlocked.Add(ref numDistinctStates, 1);
                            }
                            else
                            {
                                DeadStates.Insert(liveState.Key, liveState.Value.ExploreIfDepthLowerThan);
                                //Interlocked.Add(ref numDistinctStates, 1);
                            }
                            return true;
                        }).ToList();
                        LiveStates.Clear();

                        //Finish the writer thread

                    }
                    catch (AggregateException e)
                    {
                        foreach (var ex in e.InnerExceptions)
                        {
                            if (!(ex is PLINQErrorEncounteredException))
                            {
                                throw (e);
                            }
                            else
                            {
                                continue;
                            }
                        }
                        var prevColor = Console.ForegroundColor;
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("##################");
                        Console.WriteLine("Check Failed");
                        Console.WriteLine("##################");
                        Console.ForegroundColor = prevColor;
                        //Console.WriteLine("Total Time: {0}", DateTime.Now - loopEntryTime);
                    }
                    catch (PLINQErrorEncounteredException)
                    {
                        var prevColor = Console.ForegroundColor;
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("##################");
                        Console.WriteLine("Check Failed");
                        Console.WriteLine("##################");
                        Console.ForegroundColor = prevColor;
                        //Console.WriteLine("Total Time: {0}", DateTime.Now - loopEntryTime);
                    }

                    if ((SafetyErrors.ToArray().Length != 0 || AcceptingCycles.ToArray().Length != 0) && this.stopOnError)
                    {
                        // We have errors, and we have to stop on an error proceed no further
                        return false;
                    }
                }
                //Console.Error.WriteLine("Result Count {0}", dfsResults.Count);
                DateTime queryEndTime = DateTime.Now;



                TimeSpan delta = queryEndTime.Subtract(queryStartTime);

                if (Options.PrintStats)
                {
                    Console.WriteLine();
                    if (Options.IsSchedulerDecl)
                    {
                        Console.WriteLine("Delay Bound {0}", BoundedSearch.iterativeDelayCutOff);
                    }
                    else
                    {
                        Console.WriteLine("Depth Bound {0}", BoundedSearch.iterativeDepthCutoff);
                    }
                    Console.WriteLine("No. of Frontier {0}", frontierCounter);
                    Console.WriteLine("No. of Distinct States {0}", numDistinctStates);
                    Console.WriteLine("Total Transitions: {0}", numTotalStates);
                    Console.WriteLine("Total Exploration time so far = " + queryEndTime.Subtract(loopEntryTime));
                    Console.WriteLine("Peak / Current Paged Mem Usage : {0} M/{1} M", System.Diagnostics.Process.GetCurrentProcess().PeakPagedMemorySize64 / (1 << 20), System.Diagnostics.Process.GetCurrentProcess().PagedMemorySize64 / (1 << 20));
                    Console.WriteLine("Peak / Current working set size: {0} M/{1} M", System.Diagnostics.Process.GetCurrentProcess().PeakWorkingSet64 / (1 << 20), System.Diagnostics.Process.GetCurrentProcess().WorkingSet64 / (1 << 20));

                }

            } while ((globalFrontier.LongCount() > 0 || frontierCounter > 0) && !(BoundedSearch.checkIfFinalCutOffReached()));

            return true;
        }
        #endregion

        #region Common Functions
        private void VisitState (TraversalInfo ti,
                                        int ExploreIfDepthLowerThan, bool CompletelyExplored)
        {
            if (!ti.IsFingerPrinted)
                return;

            VisitState(ti.Fingerprint, ExploreIfDepthLowerThan, CompletelyExplored, ti.MagicBit);
        }

        private void VisitState (Fingerprint fp, int ExploreIfDepthLowerThan,
            bool CompletelyExplored, bool magicBit)
        {
            DFSLiveState StateData;
            FrontierNode ResultEntry;
            globalFrontier.TryGetValue(fp, out ResultEntry);
            if (ResultEntry != null && !Options.IsSchedulerDecl)
            {
                // Remove from frontier set
                //CombinedTable.Remove(fp);
                FrontierNode temp;
                globalFrontier.TryRemove(fp, out temp);
                if (Options.UseHierarchicalFrontiers)
                {
                    FrontierTree Tree = ResultEntry as FrontierTree;
                    Tree.Dispose();
                }

            }

            lock (DeadStates)
            {
                if (DeadStates.LookupValue(fp) == null)
                {
                    if ((LiveStates.TryGetValue(fp, out StateData) == false))
                    {
                        Interlocked.Add(ref numDistinctStates, 1);
                        StateData = new DFSLiveState(ExploreIfDepthLowerThan, CompletelyExplored, magicBit);
                        LiveStates.TryAdd(fp, StateData);
                    }
                    else
                    {
                        // State already exists
                        StateData.CompletelyExplored = CompletelyExplored;
                        StateData.ExploreIfDepthLowerThan = ExploreIfDepthLowerThan;
                        StateData.MagicBit = magicBit;
                        LiveStates.AddOrUpdate(fp, StateData, (f, val) => (StateData));
                    }
                }
                else
                {
                    DeadStates.Update(fp, ExploreIfDepthLowerThan, magicBit);
                }
            }




        }



        private bool MustExplore (Fingerprint fp, ZingBounds currBounds, out int ExploreIfDepthLowerThan, out bool CompletelyExplored, bool magicBit)
        {
            FrontierNode ResultEntry;
            DFSLiveState StateData;
            if (Options.IsSchedulerDecl)
            {
                ExploreIfDepthLowerThan = currBounds.Depth;
                CompletelyExplored = false;


                //check if this is there in the frontier set
                if (globalFrontier.TryGetValue(fp, out ResultEntry))
                {
                    return false;
                }
                else
                {
                    DeadState DState;
                    // Everything else is in the global state table.
                    if ((DState = DeadStates.LookupValue(fp)) == null)
                    {
                        if ((LiveStates.TryGetValue(fp, out StateData) == false))
                        {
                            // this is a new state
                            return true;
                        }
                        else
                        {
                            if (StateData.MagicBit == magicBit)
                            {
                                return false;
                            }
                            else
                            {
                                return true;
                            }
                        }
                    }
                    else
                    {
                        if (DState.MagicBit == magicBit)
                        {
                            return false;
                        }
                        else
                        {
                            return true;
                        }
                    }
                }
            }
            else
            {
                ExploreIfDepthLowerThan = currBounds.Depth;
                CompletelyExplored = false;


                // Check if this is a frontier
                if (globalFrontier.TryGetValue(fp, out ResultEntry))
                {
                    // This is definitely a frontier since we'll not add
                    // non-frontier entries into the combined state when 
                    // Options.NoPrivatization is true
                    if (currBounds.Depth < (ResultEntry as FrontierRepresentation).Bounds.Depth)
                    {
                        // Make sure we dispose the tree!
                        if (Options.UseHierarchicalFrontiers)
                        {
                            FrontierTree Tree = ResultEntry as FrontierTree;
                            Tree.Dispose();
                        }
                        //CombinedTable.Remove(fp);
                        FrontierNode temp;
                        globalFrontier.TryRemove(fp, out temp);
                        Interlocked.Increment(ref numStatesRevisited);
                        return true;
                    }
                    else
                    {
                        ExploreIfDepthLowerThan = (ResultEntry as FrontierRepresentation).Bounds.Depth;
                        return false;
                    }
                }

                #region deadState

                DeadState DState;
                // Everything else is in the global state table.
                if ((DState = DeadStates.LookupValue(fp)) == null)
                {
                    if ((LiveStates.TryGetValue(fp, out StateData) == false))
                    {
                        // this is a new state
                        return true;
                    }
                    else
                    {
                        if (StateData.MagicBit == magicBit)
                        {
                            // This state exists in the state table
                            if (StateData.CompletelyExplored)
                            {
                                CompletelyExplored = true;
                                return false;
                            }
                            else if (currBounds.Depth < StateData.ExploreIfDepthLowerThan)
                            {
                                Interlocked.Increment(ref numStatesRevisited);
                                return true;
                            }
                            else
                            {
                                ExploreIfDepthLowerThan = StateData.ExploreIfDepthLowerThan;
                                return false;
                            }
                        }
                        else
                        {
                            return true;
                        }
                    }
                }
                else
                {
                    if (currBounds.Depth < DState.ExploreIfDepthLowerThan || magicBit != DState.MagicBit)
                    {
                        Interlocked.Increment(ref numStatesRevisited);
                        lock (DeadStates)
                        {
                            DeadStates.Remove(fp);
                            StateData = new DFSLiveState(ExploreIfDepthLowerThan, CompletelyExplored, magicBit);
                            LiveStates.TryAdd(fp, StateData);
                        }

                        return true;
                    }
                    else
                    {
                        ExploreIfDepthLowerThan = DState.ExploreIfDepthLowerThan;
                        return false;
                    }
                }
                #endregion

            }



        }

        private bool MustExplore (TraversalInfo ti,
                                      out int ExploreIfDepthLowerThan, out bool CompletelyExplored)
        {
            ExploreIfDepthLowerThan = ti.Bounds.Depth;
            CompletelyExplored = false;
            Interlocked.Add(ref numTotalStates, 1);

            if (ti.IsFingerPrinted)
            {
                return (MustExplore(ti.Fingerprint, ti.Bounds,
                    out ExploreIfDepthLowerThan, out CompletelyExplored, ti.MagicBit));
            }
            else
            {
                return true;
            }
        }

        private void AddToFrontier (TraversalInfo ti, int SerialNum)
        {
            Fingerprint fp = ti.Fingerprint;
            ti.IsFingerPrinted = true;

            if (Options.IsSchedulerDecl)
            {
                #region Delay Bounding

                if (!globalFrontier.ContainsKey(fp))
                {
                    FrontierNode rep = FrontierRepresentation.MakeFrontierRepresentation(ti, SerialNum);
                    rep.MyNumber = FrontierTree.countFrontier;
                    FrontierTree.countFrontier++;
                    rep.threadID = SerialNum;
                    globalFrontier.TryAdd(fp, rep);
                }
                #endregion
            }
            else
            {
                #region depth bounding
                if (!globalFrontier.ContainsKey(fp))
                {
                    FrontierNode rep = FrontierRepresentation.MakeFrontierRepresentation(ti, SerialNum);
                    rep.MyNumber = FrontierTree.countFrontier;
                    FrontierTree.countFrontier++;
                    rep.threadID = SerialNum;
                    globalFrontier.AddOrUpdate(fp, rep, (key, value) =>
                    {
                        if (value.Bounds.Depth > rep.Bounds.Depth)
                        {
                            return rep;
                        }
                        else
                            return value;
                    });
                }
                #endregion
            }

        }

        private void AddToFrontier (FrontierRepresentation FrontierRep, Fingerprint fp, int MyserialNum)
        {
            if (Options.IsSchedulerDecl)
            {
                #region Delay Bounding
                if (!globalFrontier.ContainsKey(fp))
                {
                    FrontierRep.MyNumber = FrontierTree.countFrontier;
                    FrontierTree.countFrontier++;
                    FrontierRep.threadID = MyserialNum;
                    globalFrontier.TryAdd(fp, FrontierRep);
                }
                else
                {
                    if (Options.UseHierarchicalFrontiers)
                    {
                        FrontierTree Tree = FrontierRep as FrontierTree;
                        Tree.Dispose();
                    }
                }
                #endregion
            }
            else
            {
                #region Depth Bounding
                if (!globalFrontier.ContainsKey(fp))
                {
                    FrontierRep.MyNumber = FrontierTree.countFrontier;
                    FrontierTree.countFrontier++;
                    FrontierRep.threadID = MyserialNum;
                    globalFrontier.AddOrUpdate(fp, FrontierRep, (key, value) =>
                    {
                        if (value.Bounds.Depth > FrontierRep.Bounds.Depth)
                        {

                            return FrontierRep;
                        }
                        else
                            return value;
                    });
                }
                else
                {

                    if (Options.UseHierarchicalFrontiers)
                    {
                        FrontierTree Tree = FrontierRep as FrontierTree;
                        Tree.Dispose();
                    }

                }
                #endregion
            }
        }

        private void AddToFrontierBlock (TraversalInfo ti, FrontierWorkItem fBlock)
        {
            Fingerprint fp = ti.Fingerprint;
            ti.IsFingerPrinted = true;
            System.Diagnostics.Debug.Assert(Options.FrontierToDisk);
            if (Options.FrontierToDisk)
            {

                if (!frontierSet.Contains(fp))
                {
                    FrontierNode rep = FrontierRepresentation.MakeFrontierRepresentation(ti);
                    frontierSet.Add(fp);
                    fBlock.Add(rep);
                }
            }
        }

        private void AddToFrontierBlock (FrontierRepresentation FrontierRep, Fingerprint fp, FrontierWorkItem fBlock)
        {
            System.Diagnostics.Debug.Assert(Options.FrontierToDisk);
            if (Options.FrontierToDisk)
            {
                if (!frontierSet.Contains(fp))
                {
                    frontierSet.Add(fp);
                    fBlock.Add(FrontierRep);
                }
            }
        }
        #endregion

        #region Search Functions

        /// <summary>
        /// Performs a full search of the state space using the selected algorithm. When this
        /// method returns, the various search statistics properties may be queried.
        /// </summary>
        /// <param name="traces">An array of error traces, or null if no errors were found.</param>
        /// <returns>Returns an array of Trace objects. If no errors were found, the array will contain zero elements.</returns>
        public CheckerResult Crunch (out Trace[] safetytraces, out Trace[] acceptingtraces)
        {
            return this.Crunch(null, out safetytraces, out acceptingtraces);
        }

        /// <summary>
        /// Executes the model checker from the model initial state to the state represented by the given trace.
        /// Sets the initial state for algorithms not using reduction.
        /// </summary>
        /// <param name="trace">The trace that the model checker follows to set the initial TraversalInfo state. Can be null.</param>
        /// <returns>The transition depth of the resultant state.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        internal int SetTraversalInfoStartState (Trace trace)
        {
            TraversalInfo ti = new ExecutionState((StateImpl)initialState.Clone(), null, null);
            TraversalInfo newTi = null;
            //System.Diagnostics.Debugger.Break();
            if (trace != null)
            {
                if (trace.Count > 0)
                {
                    for (int i = 0; i < trace.Count; ++i)
                    {
                        //newTi = ti.GetSuccessorNForReplay((int)trace[i].Selection, trace[i].IsFingerprinted);
                        newTi = ti.GetSuccessorN((int)trace[i].Selection);
                        System.Diagnostics.Debug.Assert(newTi != null);
                        //newTi = ti.GetSuccessorNForReplay((int)trace[i].Selection, trace[i].IsFingerprinted);
                        //Console.WriteLine("TraceStep = " + (trace[i].IsChoice ? "C" : "E") + trace[i].Selection);
                        ti = newTi;
                    }
                }
            }
            TStartState = ti;

            return TStartState.Bounds.Depth;
        }

        /// <summary>
        /// Performs a full search of the state space with periodic callbacks to a given interface
        /// to permit interactive cancellation and statistical feedback.
        /// </summary>
        /// <param name="callback">An interface reference to be used for callbacks during the search. If the CheckCancel()
        /// method returns true, the search is cancelled. During the callback method, the statistical properties
        /// will accurately reflect the current state of the search.</param>
        /// <param name="traces">An array of error traces, or null if no errors were found.</param>
        /// <returns>Returns an enum describing the outcome of the search.</returns>
        public CheckerResult Crunch (ICruncherCallback callback, out Trace[] safetytraces, out Trace[] acceptancetraces)
        {
            this.callbackObj = callback;

            bool savedEventState = Options.EnableEvents;
            Options.EnableEvents = false;

            this.SetTraversalInfoStartState(null);
            if (TStartState.IsInvalidEndState())
            {
                bool OldCT = Options.CompactTraces;
                Options.CompactTraces = false;
                SafetyErrors.Add(TStartState.GenerateTrace());
                Options.CompactTraces = OldCT;
                this.lastErrorFound = TStartState.ErrorCode;
            }

            if (Options.FrontierToDisk)
            {
                this.DoDFSFrontierToDisk(tc);
            }
            else
            {
                this.DoDFSFrontierInMemory(tc);
            }
            Options.EnableEvents = savedEventState;

            if (callback != null)
                callback.CheckCancel();     // to send final stats

            safetytraces = (Trace[])SafetyErrors.ToArray(typeof(Trace));
            acceptancetraces = (Trace[])AcceptingCycles.ToArray(typeof(Trace));

            if (tc.Cancel)
                return CheckerResult.Canceled;
            else if (SafetyErrors.Count == 0 && AcceptingCycles.Count == 0)
                return CheckerResult.Success;
            else
                return this.lastErrorFound;
        }

        #endregion
    }
}
