using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Reflection;
using System.IO;

namespace Microsoft.Zing
{
    public class ZingExplorerLivenessSampling : ZingExplorer
    {
        /// <summary>
        /// Parallel worker threads for performing search.
        /// </summary>
        private Task[] searchWorkers;

        public ZingExplorerLivenessSampling()
            : base()
        {
            var zingerPath = new Uri(
                System.IO.Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().CodeBase)
                ).LocalPath;
            var schedDllPath = zingerPath + "\\" + "RandomDelayingScheduler.dll";

            if (!File.Exists(schedDllPath))
            {
                ZingerUtilities.PrintErrorMessage(String.Format("Scheduler file {0} not found", schedDllPath));
                ZingerConfiguration.DoDelayBounding = false;
                return;
            }

            var schedAssembly = Assembly.LoadFrom(schedDllPath);
            // get class name
            string schedClassName = schedAssembly.GetTypes().Where(t => (t.BaseType.Name == "ZingerDelayingScheduler")).First().FullName;
            var schedStateClassName = schedAssembly.GetTypes().Where(t => (t.BaseType.Name == "ZingerSchedulerState")).First().FullName;
            var schedClassType = schedAssembly.GetType(schedClassName);
            var schedStateClassType = schedAssembly.GetType(schedStateClassName);
            ZingerConfiguration.ZExternalScheduler.zDelaySched = Activator.CreateInstance(schedClassType) as ZingerDelayingScheduler;
            ZingerConfiguration.ZExternalScheduler.zSchedState = Activator.CreateInstance(schedStateClassType) as ZingerSchedulerState;
        }

        protected override ZingerResult IterativeSearchStateSpace()
        {
            //outer loop to search the state space iteratively
            do
            {
                //Increment the iterative bound
                ZingerConfiguration.zBoundedSearch.IncrementIterativeBound();

                try
                {
                    searchWorkers = new Task[ZingerConfiguration.DegreeOfParallelism];
                    //create parallel search threads
                    for (int i = 0; i < ZingerConfiguration.DegreeOfParallelism; i++)
                    {
                        searchWorkers[i] = Task.Factory.StartNew(SearchStateSpace, i);
                        System.Threading.Thread.Sleep(10);
                    }

                    //Wait for all search workers to finish
                    Task.WaitAll(searchWorkers);
                }
                catch (AggregateException ex)
                {
                    foreach (var inner in ex.InnerExceptions)
                    {
                        if ((inner is ZingException))
                        {
                            return lastErrorFound;
                        }
                        else
                        {
                            ZingerUtilities.PrintErrorMessage("Unknown Exception in Zing:");
                            ZingerUtilities.PrintErrorMessage(inner.ToString());
                            return ZingerResult.ZingRuntimeError;
                        }
                    }
                }

                ZingerStats.NumOfFrontiers = -1;
                ZingerStats.PrintPeriodicStats();
            }
            while (!ZingerConfiguration.zBoundedSearch.checkIfFinalCutOffReached());

            return ZingerResult.Success;
        }

        protected override void SearchStateSpace(object obj)
        {
            int myThreadId = (int)obj;
            int numberOfSchedulesExplored = 0;

            //frontier
            FrontierNode startfN = new FrontierNode(StartStateTraversalInfo);
            TraversalInfo startState = startfN.GetTraversalInfo(StartStateStateImpl, myThreadId);
            var statesExplored = new HashSet<Fingerprint>();
            while (numberOfSchedulesExplored < ZingerConfiguration.MaxSchedulesPerIteration)
            {
                //increment the schedule count
                numberOfSchedulesExplored++;
                ZingerStats.IncrementNumberOfSchedules();
                //random walk always starts from the start state ( no frontier ).
                TraversalInfo currentState = startState.Clone();
                statesExplored.Clear();
                while (currentState.CurrentDepth < 10000)
                {
                    //kil the exploration if bug found
                    //Check if cancelation token triggered
                    if (CancelTokenZingExplorer.IsCancellationRequested)
                    {
                        //some task found bug and hence cancelling this task
                        return;
                    }

                    ZingerStats.MaxDepth = Math.Max(ZingerStats.MaxDepth, currentState.CurrentDepth);

                    //Check if the DFS Stack Overflow has occured.
                    if (currentState.CurrentDepth > ZingerConfiguration.BoundDFSStackLength)
                    {

                        //update the safety traces
                        SafetyErrors.Add(currentState.GenerateNonCompactTrace());
                        // return value
                        this.lastErrorFound = ZingerResult.DFSStackOverFlowError;

                        throw new ZingerDFSStackOverFlow();
                    }

                    TraversalInfo nextSuccessor = currentState.GetNextSuccessorUniformRandomly();
                    
                    ZingerStats.IncrementTransitionsCount();
                    ZingerStats.IncrementStatesCount();
                    if (nextSuccessor == null)
                    {
                        break;
                    }

                    if(nextSuccessor.IsAcceptingState)
                    {
                        Console.Write("a");
                    }
                    //check if the next step is entered through a accepting transition
                    if(nextSuccessor.IsAcceptingState && nextSuccessor.IsFingerPrinted && statesExplored.Contains(nextSuccessor.Fingerprint))
                    {
                        AcceptingCycles.Add(nextSuccessor.GenerateNonCompactTrace());
                        lastErrorFound = ZingerResult.AcceptanceCyleFound;
                        if (ZingerConfiguration.StopOnError)
                            throw new ZingerAcceptingCycleFound();
                    }

                    //add the current state in the set.
                    if(nextSuccessor.IsFingerPrinted) statesExplored.Add(nextSuccessor.Fingerprint);

                    TerminalState terminalState = nextSuccessor as TerminalState;
                    if (terminalState != null)
                    {
                        if (terminalState.IsErroneousTI)
                        {
                            lock (SafetyErrors)
                            {
                                // bugs found
                                SafetyErrors.Add(nextSuccessor.GenerateNonCompactTrace());
                                this.lastErrorFound = nextSuccessor.ErrorCode;
                            }

                            if (ZingerConfiguration.StopOnError)
                            {
                                CancelTokenZingExplorer.Cancel(true);
                                throw nextSuccessor.Exception;
                            }
                        }

                        break;
                    }

                    currentState = nextSuccessor;
                }
            }
        }

        protected override bool MustExplore(TraversalInfo ti)
        {
            throw new NotImplementedException();
        }

        protected override void VisitState(TraversalInfo ti)
        {
            throw new NotImplementedException();
        }
    
    }
}
