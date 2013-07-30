using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Zing
{
    public class ZingSchedulerOptions
    {
        public IZingDelayingScheduler zSched;
        public IZingSchedulerState zSchedState;
    }
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class Options
    {
        private Options() { }

        private static bool enableTrace = false;
        public static bool EnableTrace
        {
            get { return Options.enableTrace; }
            set { Options.enableTrace = value; }
        }

        private static bool enableEvents = true;
        public static bool EnableEvents
        {
            get { return enableEvents; }
            set { enableEvents = value; }
        }

        private static bool fingerprintSingleTransitionStates = true;
        public static bool FingerprintSingleTransitionStates
        {
            get { return fingerprintSingleTransitionStates; }
            set { fingerprintSingleTransitionStates = value; }
        }

        private static double nonChooseProbability = 0;

        public static double NonChooseProbability
        {
            get { return nonChooseProbability; }
            set { nonChooseProbability = (double)value / (double)1000000; }
        }

        private static int degreeOfParallelism = 1;

        public static int DegreeOfParallelism
        {
            get { return degreeOfParallelism; }
            set { degreeOfParallelism = value; }
        }

        private static bool printStats = false;

        public static bool PrintStats
        {
            get { return Options.printStats; }
            set { Options.printStats = value; }
        }

        private static int workStealAmount = 10;
        public static int WorkStealAmount
        {
            get { return workStealAmount; }
            set { workStealAmount = value; }
        }

        private static bool compactTraces = false;
        public static bool CompactTraces
        {
            get { return compactTraces; }
            set { compactTraces = value; }
        }

        private static bool useHierarchicalFrontiers = false;

        public static bool UseHierarchicalFrontiers
        {
            get { return Options.useHierarchicalFrontiers; }
            set { Options.useHierarchicalFrontiers = value; }
        }

        private static bool isSchedulerDecl = false;

        public static bool IsSchedulerDecl
        {
            get { return Options.isSchedulerDecl; }
            set { Options.isSchedulerDecl = value; }
        }

        private static bool isRandomSearch = false;

        public static bool IsRandomSearch
        {
            get { return Options.isRandomSearch; }
            set { Options.isRandomSearch = value; }
        }

        
        private static ZingSchedulerOptions zSchedOptions = new ZingSchedulerOptions();

        public static ZingSchedulerOptions ZSchedOptions
        {
            get { return Options.zSchedOptions; }
            set { Options.zSchedOptions = value; }
        }

        private static bool checkLiveNess = false;

        public static bool CheckLiveNess
        {
            get { return Options.checkLiveNess; }
            set { Options.checkLiveNess = value; }
        }

        private static bool checkDFSStackLength = false;

        public static bool CheckDFSStackLength
        {
            get { return Options.checkDFSStackLength; }
            set { Options.checkDFSStackLength = value; }
        }

        private static bool frontierToDisk = false;

        public static bool FrontierToDisk
        {
            get { return Options.frontierToDisk; }
            set { Options.frontierToDisk = value; }
        }
    }
}
