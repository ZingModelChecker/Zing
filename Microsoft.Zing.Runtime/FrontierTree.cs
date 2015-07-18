using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Zing
{
    /// <summary>
    /// Used for serialization of frontier nodes
    /// </summary>
    internal sealed class AllowAllAssemblyVersionsDeserializationBinder : System.Runtime.Serialization.SerializationBinder
    {
        public override Type BindToType(string assemblyName, string typeName)
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
    /// This class implements the frontier set used to store frontier states after each iteration.
    /// </summary>
    public class FrontierSet
    {
        #region In Memory Frontier

        /// <summary>
        /// Concurrent Dictionary to store the entire frontier set after each iteration (in memory)
        /// </summary>
        private ConcurrentQueue<FrontierNode> InMemoryCurrentGlobalFrontier;

        private ConcurrentDictionary<Fingerprint, FrontierNode> InMemoryNextGlobalFrontier;

        #endregion In Memory Frontier

        /// <summary>
        /// current iteration frontiers
        /// </summary>
        private BlockingCollection<FrontierNode> currFrontierSet;

        /// <summary>
        /// next iteration frontiers
        /// </summary>
        private BlockingCollection<KeyValuePair<Fingerprint, FrontierNode>> nextFrontierSet;

        /// <summary>
        /// buffer size of the blocking collection
        /// </summary>
        private int bufferSize = 10000;

        private int numOfRWThreads = (ZingerConfiguration.DegreeOfParallelism / 3 + 1);

        /// <summary>
        /// Reader worker threads
        /// </summary>
        private Task[] readerWorkers;

        /// <summary>
        /// Writer Worker threads
        /// </summary>
        private Task[] writerWorkers;

        /// <summary>
        /// Initialize the Frontier to disk work force
        /// </summary>
        private void InitializeFrontierForce()
        {
            if (ZingerConfiguration.FrontierToDisk)
            {
                //convert all output frontiers to input frontiers
                for (int i = 0; i < numOfRWThreads; i++)
                {
                    File.Delete("i_" + i.ToString());
                    if (File.Exists("o_" + i.ToString()))
                    {
                        File.Move("o_" + i.ToString(), "i_" + i.ToString());
                    }
                }
                for (int i = 0; i < numOfRWThreads; i++)
                {
                    readerWorkers[i] = Task.Factory.StartNew(FrontierNodeReader, "i_" + i.ToString());
                    writerWorkers[i] = Task.Factory.StartNew(FrontierNodeWriter, "o_" + i.ToString());
                    System.Threading.Thread.Sleep(10);
                }
            }
        }

        /// <summary>
        /// Wait for the Readers to Finish
        /// </summary>
        public void WaitForAllReaders(CancellationToken cancel)
        {
            if (ZingerConfiguration.FrontierToDisk)
            {
                //Finished Reading all Frontiers
                try
                {
                    System.Threading.Tasks.Task.WaitAll(readerWorkers, cancel);
                }
                catch (OperationCanceledException ex)
                {
                    //Operation was cancelled so all is safe
                    //may be a bug is found
                    
                }
                currFrontierSet.CompleteAdding();
            }
        }

        /// <summary>
        /// Wait for the writers to Finish
        /// </summary>
        public void WaitForAllWriters(CancellationToken cancel)
        {
            if (ZingerConfiguration.FrontierToDisk)
            {
                //Finished adding all frontiers
                nextFrontierSet.CompleteAdding();
                try
                {
                    System.Threading.Tasks.Task.WaitAll(writerWorkers, cancel);
                }
                catch (OperationCanceledException ex)
                {
                    //Operation was cancelled so all is safe
                    //may be a bug is found
                }
            }
        }

        /// <summary>
        /// Reset workers before start of next iteration
        /// </summary>
        public void StartOfIterationReset()
        {
            if (!ZingerConfiguration.FrontierToDisk)
            {
                InMemoryCurrentGlobalFrontier = new ConcurrentQueue<FrontierNode>();

                //Move all the next iteration frontiers to current set.
                if (InMemoryNextGlobalFrontier.Count > 0)
                {
                    if (ZingerConfiguration.DegreeOfParallelism == 1)
                    {
                        var temp = InMemoryNextGlobalFrontier.OrderBy(x => x.Key).AsParallel().WithDegreeOfParallelism(ZingerConfiguration.DegreeOfParallelism).Select(fNode => { InMemoryCurrentGlobalFrontier.Enqueue(fNode.Value); return false; }).Min();
                    }
                    else
                    {
                        var temp = InMemoryNextGlobalFrontier.AsParallel().WithDegreeOfParallelism(ZingerConfiguration.DegreeOfParallelism).Select(fNode => { InMemoryCurrentGlobalFrontier.Enqueue(fNode.Value); return false; }).Min();
                    }
                }

                InMemoryNextGlobalFrontier.Clear();
                System.GC.Collect();
            }

            //check if the memory consumed currently is 70% of the max memory
            var CurrentMem = System.Diagnostics.Process.GetCurrentProcess().NonpagedSystemMemorySize64 / Math.Pow(10, 9);
            if (CurrentMem > 0.75 * ZingerConfiguration.MaxMemoryConsumption && !ZingerConfiguration.FrontierToDisk)
            {
                ZingerConfiguration.FrontierToDisk = true;
                //TODO: Push all the frontiers in memory onto disk.
            }

            // Initialize frontier to Disk
            if (ZingerConfiguration.FrontierToDisk)
            {
                nextFrontierSetHT.Clear();
                counter = 0;
                //reset the blocking collection
                currFrontierSet = new BlockingCollection<FrontierNode>(bufferSize);
                nextFrontierSet = new BlockingCollection<KeyValuePair<Fingerprint, FrontierNode>>(2 * bufferSize);
            }

            InitializeFrontierForce();
        }

        /// <summary>
        /// Number of frontiers pushed on to disk in the current iteration
        /// </summary>
        private long counter;

        /// <summary>
        /// Hash table to store next iteration frontier set to avoid adding the same frontier multiple times
        /// </summary>
        private HashSet<Fingerprint> nextFrontierSetHT;

        /// <summary>
        /// Write function that pushes a frontier to disk
        /// </summary>
        /// <param name="obj"></param>
        private void FrontierNodeWriter(object obj)
        {
            if (ZingerConfiguration.FrontierToDisk)
            {
                string outFileName = obj as string;
                Stream writeStream = File.Open(outFileName, FileMode.Create);
                foreach (var frontier in nextFrontierSet.GetConsumingEnumerable())
                {
                    frontier.Value.Serialize(writeStream);
                }
                writeStream.Close();
            }
        }

        private void FrontierNodeReader(object obj)
        {
            if (ZingerConfiguration.FrontierToDisk)
            {
                string inputFileName = obj as string;
                if (File.Exists(inputFileName))
                {
                    Stream sreader = File.Open(inputFileName, FileMode.Open);
                    while (sreader.Position < sreader.Length)
                    {
                        var fNode = new FrontierNode();
                        fNode.Deserialize(sreader);
                        currFrontierSet.Add(fNode);
                    }

                    sreader.Close();
                }
            }
        }

        public bool IsCompleted()
        {
            if (ZingerConfiguration.FrontierToDisk)
            {
                return currFrontierSet.IsCompleted;
            }
            else
            {
                return InMemoryCurrentGlobalFrontier.Count == 0;
            }
        }

        public FrontierNode GetNextFrontier()
        {
            if (ZingerConfiguration.FrontierToDisk)
            {
                try
                {
                    var frontier = currFrontierSet.Take();
                    return frontier;
                }
                catch (InvalidOperationException) { return null; }
            }
            else
            {
                FrontierNode frontier;
                if (InMemoryCurrentGlobalFrontier.TryDequeue(out frontier))
                {
                    return frontier;
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public FrontierSet(TraversalInfo startState)
        {
            if (!ZingerConfiguration.FrontierToDisk)
            {
                InMemoryCurrentGlobalFrontier = new ConcurrentQueue<FrontierNode>();
                InMemoryNextGlobalFrontier = new ConcurrentDictionary<Fingerprint, FrontierNode>();
                InMemoryNextGlobalFrontier.TryAdd(startState.Fingerprint, new FrontierNode(startState));
            }
            else
            {
                currFrontierSet = new BlockingCollection<FrontierNode>(bufferSize);
                nextFrontierSet = new BlockingCollection<KeyValuePair<Fingerprint, FrontierNode>>(2 * bufferSize);

                nextFrontierSetHT = new HashSet<Fingerprint>();

                readerWorkers = new Task[numOfRWThreads];
                writerWorkers = new Task[numOfRWThreads];
                counter = 0;
                currFrontierSet.Add(new FrontierNode(startState));
                for (int i = 0; i < numOfRWThreads; i++)
                {
                    File.Delete("i_" + i.ToString());
                    File.Delete("o_" + i.ToString());
                }
                //put the start frontier onto disk
                var startFrontier = new FrontierNode(startState);
                Stream writeStream = File.Open("o_0", FileMode.Create);
                startFrontier.Serialize(writeStream);
                writeStream.Close();
            }
        }

        public void Add(TraversalInfo ti)
        {
            //no need of adding to the frontier if the final choice bound is reached
            if (ZingerConfiguration.BoundChoices && ti.zBounds.ChoiceCost >= ZingerConfiguration.zBoundedSearch.FinalChoiceCutOff)
            {
                return;
            }

            Fingerprint fp = ti.Fingerprint;
            ti.IsFingerPrinted = true;

            if (ZingerConfiguration.FrontierToDisk)
            {
                if (!nextFrontierSetHT.Contains(fp))
                {
                    FrontierNode fNode = new FrontierNode(ti);
                    counter++;
                    nextFrontierSetHT.Add(fp);
                    //Add the item into current next frontier blocking collection
                    nextFrontierSet.Add(new KeyValuePair<Fingerprint, FrontierNode>(fp, fNode));
                }
            }
            else
            {
                if (!InMemoryNextGlobalFrontier.ContainsKey(fp))
                {
                    FrontierNode fNode = new FrontierNode(ti);
                    //add the item into in memory next frontier
                    InMemoryNextGlobalFrontier.TryAdd(fp, fNode);
                }
            }
        }

        public void Remove(Fingerprint fp)
        {
            if (ZingerConfiguration.FrontierToDisk)
            {
                return;
            }
            else
            {
                FrontierNode temp;
                InMemoryNextGlobalFrontier.TryRemove(fp, out temp);
            }
        }

        public long Count()
        {
            if (!ZingerConfiguration.FrontierToDisk)
                return InMemoryNextGlobalFrontier.LongCount();
            else
                return counter;
        }

        public bool Contains(Fingerprint fp)
        {
            if (ZingerConfiguration.FrontierToDisk)
            {
                return nextFrontierSetHT.Contains(fp);
            }
            else
            {
                return InMemoryNextGlobalFrontier.ContainsKey(fp);
            }
        }

        //For Debug
        public void PrintAll()
        {
            if (ZingerConfiguration.FrontierToDisk)
            {
                foreach (var item in nextFrontierSetHT.OrderBy(x => x))
                {
                    Console.WriteLine(item);
                }
            }
            else
            {
                foreach (var item in InMemoryNextGlobalFrontier.OrderBy(x => x.Key))
                {
                    Console.WriteLine(item.Key);
                }
            }
        }
    }

    /// <summary>
    /// Represents a node in the frontier
    /// </summary>
    [Serializable]
    public class FrontierNode
    {
        /// <summary>
        /// Store trace from the initial state to the frontier state
        /// </summary>
        private Trace TheTrace;

        /// <summary>
        /// Current Bounds for the Frontier Node
        /// </summary>
        public ZingerBounds Bounds;

        /// <summary>
        /// Used in the case of preemption bounding
        /// </summary>
        private ZingPreemptionBounding preemptionBounding;

        /// <summary>
        /// For storing the scheduler state in the frontier for delay bounding
        /// </summary>
        private ZingerSchedulerState schedulerState;

        public FrontierNode(TraversalInfo ti)
        {
            this.Bounds = new ZingerBounds(ti.zBounds.ExecutionCost, ti.zBounds.ChoiceCost);
            if (ZingerConfiguration.DoDelayBounding)
            {
                schedulerState = ti.ZingDBSchedState.Clone(true);
            }
            else if (ZingerConfiguration.DoPreemptionBounding)
            {
                preemptionBounding = ti.preemptionBounding.Clone();
            }
            ti.IsFingerPrinted = true;
            TheTrace = ti.GenerateTrace();
        }

        public FrontierNode()
        {
        }

        public TraversalInfo GetTraversalInfo(StateImpl InitialState, int threadId)
        {
            TraversalInfo retTraversalInfo = GetTraversalInfoForTrace(TheTrace, threadId, (StateImpl)InitialState.Clone(threadId));
            retTraversalInfo.zBounds = Bounds;
            if (ZingerConfiguration.DoDelayBounding)
            {
                retTraversalInfo.ZingDBSchedState = schedulerState;
            }
            else if (ZingerConfiguration.DoPreemptionBounding)
            {
                retTraversalInfo.preemptionBounding = preemptionBounding.Clone();
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
        private TraversalInfo GetTraversalInfoForTrace(Trace trace, int threadId, StateImpl iState)
        {
            TraversalInfo ti = new ExecutionState((StateImpl)iState.Clone(threadId), null, null);
            int Step = 0;
            if (trace != null)
            {
                #region Trace Count greater than zero

                if (trace.Count > 0)
                {
                    while (Step < trace.Count)
                    {
                        if (ZingerConfiguration.CompactTraces && ti.HasMultipleSuccessors)
                        {
                            ti = ti.GetSuccessorNForReplay((int)trace[Step++].Selection, false);
                        }
                        else if (ZingerConfiguration.CompactTraces)
                        {
                            while (!ti.HasMultipleSuccessors && ZingerConfiguration.CompactTraces)
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

                #endregion Trace Count greater than zero

                #region Traversing the tail

                while (!ti.HasMultipleSuccessors && ZingerConfiguration.CompactTraces)
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

                #endregion Traversing the tail
            }
            return ti;
        }

        #endregion Public helpers

        #region Serialize the FrontierNode

        public void Serialize(Stream outputStream)
        {
            var bWriter = new BinaryWriter(outputStream);
            //dump Depth
            bWriter.Write(this.Bounds.ExecutionCost);
            bWriter.Write(this.Bounds.ChoiceCost);
            //dump scheduler state
            if (ZingerConfiguration.DoDelayBounding)
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

        public void Deserialize(Stream inputStream)
        {
            BinaryReader bReader = new BinaryReader(inputStream);

            this.Bounds = new ZingerBounds();
            this.Bounds.ExecutionCost = bReader.ReadInt32();
            this.Bounds.ChoiceCost = bReader.ReadInt32();
            if (ZingerConfiguration.DoDelayBounding)
            {
                BinaryFormatter bFormat = new BinaryFormatter();
                bFormat.Binder = new AllowAllAssemblyVersionsDeserializationBinder();
                this.schedulerState = (ZingerSchedulerState)bFormat.Deserialize(inputStream);
            }
            Int32 traceCount = bReader.ReadInt32();
            UInt32[] steps = new UInt32[traceCount];
            for (int i = 0; i < traceCount; i++)
            {
                steps[i] = bReader.ReadUInt32();
            }
            this.TheTrace = new Trace(steps);
        }

        #endregion Serialize the FrontierNode
    }
}