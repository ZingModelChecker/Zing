using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Zing
{
    /// <summary>
    /// Stores the current Exploration Cost.
    /// This value is used to check if the bound is exceeded
    /// </summary>
    [Serializable]
    public class ZingerBounds
    {
        public Int32 ExecutionCost;
        public Int32 ChoiceCost;
        public ZingerBounds(Int32 searchB, Int32 choiceB)
        {
            ExecutionCost = searchB;
            ChoiceCost = choiceB;
        }

        public ZingerBounds()
        {
            ExecutionCost = 0;
            ChoiceCost = 0;
        }

        public void IncrementDepthCost()
        {
            if(!ZingerConfiguration.DoDelayBounding && !ZingerConfiguration.DoPreemptionBounding)
            {
                ExecutionCost++;
            }
        }

        public void IncrementDelayCost()
        {
            if(ZingerConfiguration.DoDelayBounding)
            {
                ExecutionCost++;
            }
        }

        public void IncrementPreemptionCost()
        {
            if(ZingerConfiguration.DoPreemptionBounding)
            {
                ExecutionCost++;
            }
        }
    }

    /// <summary>
    /// Stores the configuration for zinger bounded search
    /// </summary>
    public class ZingerBoundedSearch
    {
        #region Bounds
        public int FinalExecutionCutOff;
        public int FinalChoiceCutOff;
        public int IterativeIncrement;
        public int IterativeCutoff;
        #endregion

        #region Contructor
        public ZingerBoundedSearch(int exeFinalCutoff, int exeIterativeInc, int finalChoiceCutoff)
        {
            FinalExecutionCutOff = exeFinalCutoff;
            FinalChoiceCutOff = finalChoiceCutoff;
            IterativeIncrement = exeIterativeInc;
            IterativeCutoff = 0;
        }

        public ZingerBoundedSearch()
        {
            FinalExecutionCutOff = int.MaxValue;
            FinalChoiceCutOff = int.MaxValue;
            IterativeIncrement = 1;
            IterativeCutoff = 0;
        }
        #endregion
        
        #region Functions
        public bool checkIfFinalCutOffReached()
        {
            if(IterativeCutoff >= FinalExecutionCutOff)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool checkIfIterativeCutOffReached(ZingerBounds currBounds)
        {
            
                if ((currBounds.ExecutionCost >= IterativeCutoff) || (ZingerConfiguration.BoundChoices && currBounds.ChoiceCost >= FinalChoiceCutOff))
                {
                    return true;
                }
                else
                {
                    return false;

                }
        
        }

        public void IncrementIterativeBound()
        {
            IterativeCutoff += IterativeIncrement;
            IterativeCutoff = Math.Min(FinalExecutionCutOff, IterativeCutoff);
        }
        #endregion
    }

    /// <summary>
    /// Zing External Scheduler
    /// </summary>
    public class ZingerExternalScheduler
    {
        // Zing Delaying Scheduler
        public ZingerDelayingScheduler zDelaySched;
        // Zing External Scheduler State
        public ZingerSchedulerState zSchedState;
    }
    
    /// <summary>
    /// Maceliveness configuration
    /// </summary>
    public class ZingerMaceLiveness
    {
        //Exhaustive Search Depth
        public int exSearchDepth;
        //iterative live-state period
        public int liveStatePeriod;
        //random walk final cutoff
        public int randomFinalCutOff;

        public ZingerMaceLiveness()
        {
            exSearchDepth = 10;
            liveStatePeriod = 1000;
            randomFinalCutOff = 100000;
        }

        public ZingerMaceLiveness(int exS, int liveP, int ranF)
        {
            exSearchDepth = exS;
            liveStatePeriod = liveP;
            randomFinalCutOff = ranF;
        }
    }

    
    /// <summary>
    /// Zinger Configuration
    /// </summary>
    public class ZingerConfiguration
    {
        private ZingerConfiguration() { }

        //Zing Bounded Search Configuration
        public static ZingerBoundedSearch zBoundedSearch = new ZingerBoundedSearch();
        //zing model file
        public static string ZingModelFile = "";

        //deterministic delaying scheduler dll
        public static string delayingSchedDll = "";

        //trace log file
        public static string traceLogFile = "";

        // Commandline Option to dump trace based counter example in a file
        private static bool enableTrace = false;
        public static bool EnableTrace
        {
            get { return ZingerConfiguration.enableTrace; }
            set { ZingerConfiguration.enableTrace = value; }
        }

        // commandline option to dump out detailed Zing stack trace
        private static bool detailedZingTrace = false;
        public static bool DetailedZingTrace
        {
            get { return ZingerConfiguration.detailedZingTrace; }
            set { ZingerConfiguration.detailedZingTrace = value; }
        }

        // Avoid Fingerprinting single transition states in Zing (default value is true).
        private static bool notFingerprintSingleTransitionStates = true;
        public static bool FingerprintSingleTransitionStates
        {
            get { return notFingerprintSingleTransitionStates; }
            set { notFingerprintSingleTransitionStates = value; }
        }

        // Probability of fingerprinting single transition states
        private static double nonChooseProbability = 0;
        public static double NonChooseProbability
        {
            get { return nonChooseProbability; }
            set { nonChooseProbability = (double)value / (double)1000000; }
        }

        //degree of parallelism (default = single threaded)
        private static int degreeOfParallelism = 1;
        public static int DegreeOfParallelism
        {
            get { return degreeOfParallelism; }
            set { degreeOfParallelism = value; }
        }

        //print detailed states after each iteration.
        private static bool printStats = false;
        public static bool PrintStats
        {
            get { return ZingerConfiguration.printStats; }
            set { ZingerConfiguration.printStats = value; }
        }

        //Configuration for work-stealing (default is 10 items from other threads queue)
        private static int workStealAmount = 10;
        public static int WorkStealAmount
        {
            get { return workStealAmount; }
            set { workStealAmount = value; }
        }

        //Compact Execution traces (not store the step taken if the state has single successor).
        private static bool compactTraces = false;
        public static bool CompactTraces
        {
            get { return compactTraces; }
            set { compactTraces = value; }
        }

        //do preemption bounding
        private static bool doPreemptionBounding = false;
        public static bool DoPreemptionBounding
        {
            get { return ZingerConfiguration.doPreemptionBounding; }
            set { ZingerConfiguration.doPreemptionBounding = value; }
        }

        //do delay bounding
        private static bool doDelayBounding = false;
        public static bool DoDelayBounding
        {
            get { return ZingerConfiguration.doDelayBounding; }
            set { ZingerConfiguration.doDelayBounding = value; }
        }


        //do Random DFS
        private static bool doRandomWalk = false;
        public static bool DoRandomWalk
        {
            get { return ZingerConfiguration.doRandomWalk; }
            set { ZingerConfiguration.doRandomWalk = value; }
        }

        //maximum number of schedules per iteration
        private static int maxSchedulesPerIteration = 10000;
        public static int MaxSchedulesPerIteration
        {
            get { return ZingerConfiguration.maxSchedulesPerIteration; }
            set { ZingerConfiguration.maxSchedulesPerIteration = value; }
        }



        //do stateless search
        private static bool doStateLess = false;
        public static bool DoStateLess
        {
            get { return ZingerConfiguration.doStateLess; }
            set { ZingerConfiguration.doStateLess = value; }
        }

        //Zing External Scheduler for delay bounding
        private static ZingerExternalScheduler zExternalScheduler = new ZingerExternalScheduler();
        public static ZingerExternalScheduler ZExternalScheduler
        {
            get { return ZingerConfiguration.zExternalScheduler; }
            set { ZingerConfiguration.zExternalScheduler = value; }
        }

        //Do NDFS based liveness checking
        private static bool doNDFSLiveness = false;
        public static bool DoNDFSLiveness
        {
            get { return ZingerConfiguration.doNDFSLiveness; }
            set { ZingerConfiguration.doNDFSLiveness = value; }
        }

        //Bound the max stack length, this is used in cases where zing has infinite stack trace.
        private static int boundDFSStackLength = int.MaxValue;
        public static int BoundDFSStackLength
        {
            get { return ZingerConfiguration.boundDFSStackLength; }
            set { ZingerConfiguration.boundDFSStackLength = value; }
        }

        // bound the choice points
        private static bool boundChoices = false;
        public static bool BoundChoices
        {
            get { return ZingerConfiguration.boundChoices; }
            set { ZingerConfiguration.boundChoices = value; }
        }

        //perform maceliveness
        private static bool doMaceliveness = false;
        public static bool DoMaceliveness
        {
            get { return ZingerConfiguration.doMaceliveness; }
            set { ZingerConfiguration.doMaceliveness = value; }
        }

        public static ZingerMaceLiveness MaceLivenessConfiguration = new ZingerMaceLiveness();

        //perform Iterative MAP algorithm for liveness
        private static bool doMAPLiveness = false;
        public static bool DoMAPLiveness
        {
            get { return ZingerConfiguration.doMAPLiveness; }
            set { ZingerConfiguration.doMAPLiveness = value; }
        }


        //push all the frontiers to disk
        private static bool frontierToDisk = false;
        public static bool FrontierToDisk
        {
            get { return ZingerConfiguration.frontierToDisk; }
            set { ZingerConfiguration.frontierToDisk = value; }
        }

        //do state caching after memory usage has exceeded
        private static double maxMemoryConsumption = double.MaxValue;
        public static double MaxMemoryConsumption
        {
            get { return ZingerConfiguration.maxMemoryConsumption; }
            set { ZingerConfiguration.maxMemoryConsumption = value; }
        }

        //find all errors in the program.
        private static bool stopOnError = true;
        public static bool StopOnError
        {
            get { return ZingerConfiguration.stopOnError; }
            set { ZingerConfiguration.stopOnError = value; }
        }

        //execute TraceStatements when in Error Trace generation mode.
        private static bool executeTraceStatements = false;
        public static bool ExecuteTraceStatements
        {
            get { return ZingerConfiguration.executeTraceStatements; }
            set { ZingerConfiguration.executeTraceStatements = value; }
        }

        public static void InferConfiguration()
        {
            //Initialize the conflicting settings
            //The set of conflicting configurations are 
            //Randomwalk + Statefull
            //NDFliveness + iterative + sequential
            if(DoRandomWalk)
            {
                DoStateLess = true;
            }
            
            if(DoNDFSLiveness)
            {
                zBoundedSearch.IterativeIncrement = zBoundedSearch.FinalExecutionCutOff;
                DegreeOfParallelism = 1;
                DoStateLess = false;
                DoRandomWalk = false;
            }

            if(DoDelayBounding)
            {
                DoPreemptionBounding = false;
            }
        }

        public static void PrintConfiguration()
        {
            ZingerUtilities.PrintMessage("Zinger Configuration :");
            ZingerUtilities.PrintMessage(String.Format("EnableTrace : {0} and TraceFile: {1}", EnableTrace, traceLogFile));
            ZingerUtilities.PrintMessage(String.Format("DetailedZingTrace: {0}", DetailedZingTrace));
            ZingerUtilities.PrintMessage(String.Format("FingerPrint Single Transition States : {0}", !notFingerprintSingleTransitionStates));
            ZingerUtilities.PrintMessage(String.Format("Degree Of Parallelism: {0}", DegreeOfParallelism));
            ZingerUtilities.PrintMessage(String.Format("Print Statistics: {0}", PrintStats));
            ZingerUtilities.PrintMessage(String.Format("Compact Trace : {0}", CompactTraces));
            ZingerUtilities.PrintMessage(String.Format("Do Preemption Bounding :{0}", doPreemptionBounding));
            ZingerUtilities.PrintMessage(String.Format("Delay Bounding : {0} with Scheduler : {1}", doDelayBounding, delayingSchedDll));
            ZingerUtilities.PrintMessage(String.Format("Do RanddomWalk : {0} and max schedules per iteration {1}", doRandomWalk, maxSchedulesPerIteration));
            ZingerUtilities.PrintMessage(String.Format("Do Stateless : {0}", doStateLess));
            ZingerUtilities.PrintMessage(String.Format("Do NDFLiveness : {0}", doNDFSLiveness));
            ZingerUtilities.PrintMessage(String.Format("Max Stack Size : {0}", boundDFSStackLength));
            ZingerUtilities.PrintMessage(String.Format("Bound Choices: {0} and max Bound {1}", BoundChoices, zBoundedSearch.FinalChoiceCutOff));
            ZingerUtilities.PrintMessage(String.Format("Bounded Search : Iterative bound {0} and Max Bound {1}", zBoundedSearch.IterativeIncrement, zBoundedSearch.FinalExecutionCutOff));
            ZingerUtilities.PrintMessage(String.Format("Do Maceliveness: {0} with exhaustive bound {1}, live state period {2} and final bound {3}", doMaceliveness, MaceLivenessConfiguration.exSearchDepth, MaceLivenessConfiguration.liveStatePeriod, MaceLivenessConfiguration.randomFinalCutOff));
            ZingerUtilities.PrintMessage(String.Format("Do MapLiveness: {0}", doMAPLiveness));
            ZingerUtilities.PrintMessage(String.Format("Frontier to Disk :{0}", frontierToDisk));
            ZingerUtilities.PrintMessage(String.Format("Max memory : {0}", maxMemoryConsumption));
            ZingerUtilities.PrintMessage(String.Format("Stop on First Error: {0}", stopOnError));
            ZingerUtilities.PrintMessage("");
        }

    }
}
