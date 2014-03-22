using System;
using System.IO;
using System.Collections;
using System.Threading;
using System.Timers;
using System.Reflection;
using System.Runtime;
using Microsoft.Zing;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Microsoft.Zing
{

    /// <summary> 
    /// Summary description for Driver.
    /// </summary>
    class Driver
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        
        private static ParallelExplorer pExp;
        private static ArrayList Filters;
        [STAThread]
        static void Main(string[] args)
        {
            string modelFile = null;
            HashSet<string> pluginDlls = new HashSet<string>();
            HashSet<string> schedDlls = new HashSet<string>();
            string tracelogFile = null;
            bool oEventsOnly = false;
            bool oMultiple = false;
            bool oPeriodicStats = false;
            int odepthCutoff = int.MaxValue;
            int odepthInterval = 1;
            int odelayCutoff = -1;
            int odelayInterval = -1;
            int oMaxDFSStackLength = -1;
            bool oCompactTraces = false;
            bool oOptimizedIDBDFS = false;
            int oDegreeOfParallelism = 1;
            int oWorkStealAmount = 1;
            uint oNonChooseProbability = 0;

            #region Parse command line

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                if (arg[0] == '-' || arg[0] == '/')
                {
                    string option = arg.TrimStart('/', '-').ToLower();
                    string param = string.Empty;

                    int sepIndex = option.IndexOf(':');

                    if (sepIndex > 0)
                    {
                        param = option.Substring(sepIndex + 1);
                        option = option.Substring(0, sepIndex);
                    }
                    else if (sepIndex == 0)
                    {
                        Usage(arg, "Malformed option");
                        return;
                    }


                    switch (option)
                    {
                        case "co":

                            if (param.Length == 0)
                            {
                                oNonChooseProbability = 0;
                            }
                            else
                            {
                                oNonChooseProbability = UInt32.Parse(param);
                            }
                            break;
                        case "ct":
                            oCompactTraces = true;
                            break;
                        case "maceliveness":
                            Options.Maceliveness = true;
                            if(param.Length == 0)
                            {
                                Usage(arg, "Please provide correct parameters with maceliveness option");
                                return;
                            }
                            else
                            {
                                var parameters = Regex.Match(param, "([0-9]*,[0-9]*,[0-9]*)").Groups[0].ToString();
                                var bounds = parameters.Split(',');
                                if(bounds.Count() != 3)
                                {
                                    Usage(arg, "Please provide correct parameters with maceliveness option");
                                    return;
                                }
                                else
                                {
                                    MaceLiveness.ExhaustiveSearchBound = Int32.Parse(bounds[0]);
                                    MaceLiveness.RandomWalkBound = Int32.Parse(bounds[1]);
                                    MaceLiveness.FinalBound = Int32.Parse(bounds[2]);
                                }
                            }
                            break;
                        case "et":
                        case "enabletrace":
                            Options.EnableTrace = true;
                            if (param.Length == 0)
                            {
                                Usage(arg, "Please provide a file name for the trace file");
                                return;
                            }
                            else
                            {
                                tracelogFile = param;
                            }

                            break;

                        case "s":
                        case "stats":
                            Options.PrintStats = true;
                            break;

                        case "h":
                        case "help":
                        case "?":
                            Usage(null, null);
                            return;

                        case "m":
                        case "multiple":
                            oMultiple = true;
                            break;

                        case "c":
                        case "cutoff":
                            if (param.Length == 0)
                            {
                                Usage(arg, "Missing cutoff depth value");
                                return;
                            }
                            try
                            {
                                odepthCutoff = Int32.Parse(param);
                            }
                            catch (Exception)
                            {
                                Usage(arg, "Invalid numeric parameter");
                                return;
                            }

                            if (odepthCutoff <= 0)
                            {
                                Usage(arg, "Cutoff must be positive");
                                return;
                            }
                            break;

                        case "delayc":
                        
                            if (param.Length == 0)
                            {
                                Usage(arg, "Missing cutoff depth value");
                                return;
                            }
                            try
                            {
                                odelayCutoff = Int32.Parse(param);
                            }
                            catch (Exception)
                            {
                                Usage(arg, "Invalid numeric parameter");
                                return;
                            }

                            if (odelayCutoff <= 0)
                            {
                                Usage(arg, "Cutoff must be positive");
                                return;
                            }
                            break;
                        case "inc":
                        case "i":
                            if (param.Length == 0)
                            {
                                Usage(arg, "Missing iterative deepening interval value");
                                return;
                            }
                            try
                            {
                                odepthInterval = Int32.Parse(param);
                            }
                            catch (Exception)
                            {
                                Usage(arg, "Invalid numeric parameter");
                                return;
                            }
                            if (odepthInterval <= 0)
                            {
                                Usage(arg, "Iterative deepening interval must be positive");
                                return;
                            }
                            break;

                        case "delayi":
                            if (param.Length == 0)
                            {
                                Usage(arg, "Missing iterative delay interval value");
                                return;
                            }
                            try
                            {
                                odelayInterval = Int32.Parse(param);
                            }
                            catch (Exception)
                            {
                                Usage(arg, "Invalid numeric parameter");
                                return;
                            }
                            if (odelayInterval <= 0)
                            {
                                Usage(arg, "Iterative delay interval must be positive");
                                return;
                            }
                            break;

                        case "randomdfs":
                            Options.IsRandomSearch = true;
                            break;
                        case "eo":
                            oEventsOnly = true;
                            break;

                        case "p":
                        case "parallel":
                            oOptimizedIDBDFS = true;
                            if (param.Length == 0)
                            {
                                Console.WriteLine("Using Degree of Parallelism = {0} for parallel exploration", Environment.ProcessorCount);
                                oDegreeOfParallelism = Environment.ProcessorCount;
                                Options.DegreeOfParallelism = oDegreeOfParallelism;
                            }
                            else
                            {
                                oDegreeOfParallelism = int.Parse(param);
                                Options.DegreeOfParallelism = oDegreeOfParallelism;
                            }
                            break;

                        case "plugin":
                            pluginDlls.Add(param);
                            // Perform checks on plugin
                            var check = CheckZingPlugin(param);
                            if (!check)
                            {
                                Usage(arg, "Invalid ZingPlugin Dll");
                                return;
                            }
                            break;
                        case "liveness":
                            {
                                Options.CheckLiveNess = true;
                                odepthInterval = int.MaxValue;
                            }
                            break;
                        case "cdfsstack":
                            Options.CheckDFSStackLength = true;
                            oMaxDFSStackLength = int.Parse(param);
                            break;
                        case "frontiertodisk":
                            Options.FrontierToDisk = true;
                            break;

                        case "bc":
                            Options.BoundChoices = true;
                            break;

                        case "sched":
                            {
                                Options.IsSchedulerDecl = true;

                                schedDlls.Add(param);
                                if(schedDlls.Count > 1)
                                {
                                    var color = Console.ForegroundColor;
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.WriteLine("Error: Multiple Scheduler DLL are provided");
                                    Console.ForegroundColor = color;
                                    return;
                                }
                                var checkSched = CheckZingScheduler(param);
                                if(!checkSched)
                                {
                                    Usage(arg, "Invalid ZingScheduler Dll");
                                    return;
                                }

                                //
                                // Set the default values for delay and depth bounds (iterative and final)
                                //
                                if (odelayCutoff == -1)
                                {
                                    odelayCutoff = int.MaxValue;
                                }
                                if(odelayInterval == -1)
                                {
                                    odelayInterval = 1;
                                }
                                odepthCutoff = int.MaxValue;
                                odepthInterval = int.MaxValue;
                                break;
                            }
                        default:
                            Usage(arg, "Unknown option");
                            return;
                    }
                }
                else
                {
                    // must be a model file
                    if (modelFile != null)
                    {
                        Usage(arg, "Only one Zing model may be referenced");
                        return;
                    }

                    if (!File.Exists(arg))
                    {
                        Usage(arg, "Can't find Zing assembly");
                        return;
                    }

                    modelFile = arg;
                }
            }

            if (modelFile == null)
            {
                Usage(null, "No Zing model specified");
                return;
            }

            #endregion

            try
            {
                Filters = new ArrayList();

                Trace[] SafetyErrorTraces;
                Trace[] AcceptanceErrorTraces;

                CheckerResult result;

                if(Options.IsSchedulerDecl)
                {
                    var schedAssembly = Assembly.LoadFrom(schedDlls.First());
                    // get class name 
                    string schedClassName = schedAssembly.GetTypes().Where(t => t.GetInterface("IZingDelayingScheduler") != null).First().FullName;
                    var schedStateClassName = schedAssembly.GetTypes().Where(t => t.GetInterface("IZingSchedulerState") != null).First().FullName;
                    var schedClassType = schedAssembly.GetType(schedClassName);
                    var schedStateClassType = schedAssembly.GetType(schedStateClassName);
                    Options.ZSchedOptions.zSched = Activator.CreateInstance(schedClassType) as IZingDelayingScheduler;
                    Options.ZSchedOptions.zSchedState = Activator.CreateInstance(schedStateClassType) as IZingSchedulerState;
                }

                //check if plugin/scheduler provided
                if (pluginDlls.Count == 0)
                {
                    pExp = new ParallelExplorer(modelFile);
                }
                else
                {
                    Dictionary<string, IZingPlugin> zPlugin = new Dictionary<string, IZingPlugin>();
                    foreach (var zingPluginDll in pluginDlls)
                    {
                        // Initialize zingplugin
                        var assembly_ = Assembly.LoadFrom(zingPluginDll);
                        // get class name
                        string className = "";
                        var TypesInAssembly = assembly_.GetTypes().ToList();

                        foreach (var T in TypesInAssembly)
                        {
                            if (T.GetInterface("IZingPlugin") != null)
                            {
                                className = T.FullName;
                                var dllName = Path.GetFileName(zingPluginDll);
                                var classType = assembly_.GetType(className);
                                var pluginInstance = Activator.CreateInstance(classType) as IZingPlugin;
                                zPlugin.Add(dllName.ToLower(), pluginInstance);
                            }
                        }
                    }
                    pExp= new ParallelExplorer(modelFile, zPlugin);
                }

                pExp.MaxDFSStackLength = oMaxDFSStackLength;
                pExp.StopOnError = !oMultiple;
                if(Options.Maceliveness)
                {
                    odelayCutoff = MaceLiveness.ExhaustiveSearchBound;
                    odepthCutoff = MaceLiveness.ExhaustiveSearchBound;
                }
                pExp.BoundedSearch = new ZingBoundedSearch(odepthInterval, odelayInterval, odepthCutoff, odelayCutoff);
                Options.WorkStealAmount = oWorkStealAmount;
                Options.CompactTraces = oCompactTraces;

                Options.DegreeOfParallelism = oDegreeOfParallelism;

                DateTime startTime = DateTime.Now;

                {
                    ICruncherCallback cb = null;
                    if (oPeriodicStats)
                    {
                        cb = new ZingerPeriodicStatsCallback(pExp);
                    }

                    result = pExp.Crunch(cb, out SafetyErrorTraces, out AcceptanceErrorTraces);
                }


                DateTime finishTime = DateTime.Now;

                if (result == CheckerResult.Success)
                    Console.WriteLine("Check passed");
                else
                    Console.WriteLine("Check failed");

                if (Options.PrintStats)
                {
                    Console.WriteLine("{0} distinct states, {1} total transitions, {2} steps max depth",
                        pExp.NumDistinctStates, pExp.NumTotalStates, pExp.MaxDepth);

                    TimeSpan elapsedTime = finishTime.Subtract(startTime);

                    Console.WriteLine("Elapsed time: {0:00}:{1:00}:{2:00}\r\nTransitions/sec: {3}",
                        (int)elapsedTime.TotalHours, (int)elapsedTime.Minutes, (int)elapsedTime.Seconds,
                        ((int)(pExp.NumTotalStates / elapsedTime.TotalSeconds)).ToString());
                    Console.WriteLine("Unique states/sec: {0}.", (pExp.NumDistinctStates / elapsedTime.TotalSeconds).ToString());
                    ;
                }
                if (Options.PrintStats)
                {
                    Console.WriteLine("Memory Stats:");
                    Console.WriteLine("Peak Virtual Memory Size: {0} MB",
                        (double)System.Diagnostics.Process.GetCurrentProcess().PeakVirtualMemorySize64 / (1 << 20));
                    Console.WriteLine("Peak Paged Memory Size  : {0} MB",
                        (double)System.Diagnostics.Process.GetCurrentProcess().PeakPagedMemorySize64 / (1 << 20));
                    Console.WriteLine("Peak Working Set Size   : {0} MB",
                        (double)System.Diagnostics.Process.GetCurrentProcess().PeakWorkingSet64 / (1 << 20));
                }


                Hashtable methodNameToLabelMap = null;
                string modelName = modelFile.Substring(0, modelFile.Length - 4);
                string labelFile = modelName + ".labels";
                if (File.Exists(labelFile))
                {
                    methodNameToLabelMap = ParseLabelFile(labelFile);
                }

                #region Safety Error Trace
                StreamWriter tracer = null;
                if (Options.EnableTrace)
                {
                    tracer = new StreamWriter(tracelogFile);
                }
                for (int i = 0; i < SafetyErrorTraces.Length; i++)
                {
                    Trace trace = SafetyErrorTraces[i];

                    if (methodNameToLabelMap != null)
                    {
                        string tempName = modelName + ".trace" + i;
                        StreamWriter sw = File.CreateText(tempName);
                        sw.Write(trace.GetErrorTraceWithLabels(pExp.InitialState, methodNameToLabelMap));
                        sw.Close();
                    }

                    State[] states;
                    states = trace.GetStates(pExp.InitialState);

                    Console.WriteLine(" *******************         Safety Error Trace          ***********************");
                    Console.WriteLine(" *******************************************************************************");
                    Console.WriteLine(" Error trace {0}: length: {1} states", i, states.Length);

                    if (oEventsOnly)
                    {
                        for (int j = 0; j < states.Length; j++)
                        {
                            ZingEvent[] events;
                            events = states[j].GetEvents();

                            for (int k = 0; k < events.Length; k++)
                            {
                                Console.Write("  {0}\r\n", events[k]);
                            }
                            if (states[j].Error != null)
                            {
                                Console.WriteLine();
                                Console.WriteLine("Error in state:");
                                Console.WriteLine("{0}", states[j].Error);
                            }
                        }

                        Console.WriteLine("Depth on error {0}", states.Length);

                    }
                    else
                    {

                        for (int j = 0; j < states.Length; j++)
                        {
                            if (j == 0)
                                Console.Write("#### State {0} : \r\n {1}", j, states[j]);
                            else
                            {
                                if (trace[j - 1].IsExecution)
                                    Console.Write("#### State {0} (ran process {1}) :\r\n{2}", j, trace[j - 1].Selection, states[j]);
                                else
                                    Console.Write("#### State {0} (took choice {1}) :\r\n{2}", j, trace[j - 1].Selection, states[j]);
                            }

                            if (states[j].Error != null)
                            {
                                Console.WriteLine();
                                Console.WriteLine("Error in state:");
                                Console.WriteLine("{0}", states[j].Error);
                            }
                            if (pExp.DFSStackOverFlowError)
                            {
                                Console.WriteLine();
                                Console.WriteLine("Zing Error : DFS Stack Overflow");
                                Console.WriteLine("Size of DFS Stack exceeded {0}", pExp.MaxDFSStackLength);
                            }
                        }
                        Console.WriteLine();
                    }

                    //Print trace into the file
                    if (Options.EnableTrace)
                    {
                        tracer.WriteLine("Safety Error Trace");
                        tracer.WriteLine("Trace-Log {0}:",i);
                        for (int j = 0; j < states.Length; j++)
                        {
                            ZingEvent[] traceLogs;
                            traceLogs = states[j].GetTraceLog();

                            for (int k = 0; k < traceLogs.Length; k++)
                            {
                                tracer.Write("  {0}\r\n", traceLogs[k]);
                            }
                        }

                        if (pExp.DFSStackOverFlowError)
                        {
                            tracer.WriteLine("<ZING EXCEPTION> Size of DFS Stack exceeded {0}", pExp.MaxDFSStackLength);
                        }
                        
                    }
                }
                if (Options.EnableTrace)
                {
                    tracer.Close();
                }
                #endregion

                #region Liveness Error Trace
                
                if (Options.EnableTrace)
                {
                    tracer = new StreamWriter(tracelogFile, true);
                }
                for (int i = 0; i < AcceptanceErrorTraces.Length; i++)
                {
                    Trace trace = AcceptanceErrorTraces[i];

                    if (methodNameToLabelMap != null)
                    {
                        string tempName = modelName + ".trace" + i;
                        StreamWriter sw = File.CreateText(tempName);
                        sw.Write(trace.GetErrorTraceWithLabels(pExp.InitialState, methodNameToLabelMap));
                        sw.Close();
                    }

                    State[] states;
                    states = trace.GetStates(pExp.InitialState);

                    Console.WriteLine(" *******************         Liveness Error Trace          ***********************");
                    Console.WriteLine(" *******************************************************************************");
                    Console.WriteLine(" Error trace {0}: length: {1} states", i, states.Length);

                    if (oEventsOnly)
                    {
                        for (int j = 0; j < states.Length; j++)
                        {
                            ZingEvent[] events;
                            events = states[j].GetEvents();

                            for (int k = 0; k < events.Length; k++)
                            {
                                Console.Write("  {0}\r\n", events[k]);
                            }
                            if (states[j].Error != null)
                            {
                                Console.WriteLine();
                                Console.WriteLine("Error in state:");
                                Console.WriteLine("{0}", states[j].Error);
                            }
                        }

                        Console.WriteLine("Depth on error {0}", states.Length);

                    }
                    else
                    {

                        for (int j = 0; j < states.Length; j++)
                        {
                            if (states[j].IsAcceptanceState)
                            {
                                Console.WriteLine();
                                Console.WriteLine("#### Accepting State ####");
                            }

                            if (j == 0)
                                Console.Write("#### State {0} : \r\n {1}", j, states[j]);
                            else
                            {
                                if (trace[j - 1].IsExecution)
                                    Console.Write("#### State {0} (ran process {1}) :\r\n{2}", j, trace[j - 1].Selection, states[j]);
                                else
                                    Console.Write("#### State {0} (took choice {1}) :\r\n{2}", j, trace[j - 1].Selection, states[j]);
                            }

                            if (states[j].Error != null)
                            {
                                Console.WriteLine();
                                Console.WriteLine("Error in state:");
                                Console.WriteLine("{0}", states[j].Error);
                            }
                        }
                        Console.WriteLine();
                    }

                    //Print trace into the file
                    if (Options.EnableTrace)
                    {
                        tracer.WriteLine("Liveness Error Trace --- ");
                        tracer.WriteLine("Trace-Log {0}:", i);
                        for (int j = 0; j < states.Length; j++)
                        {
                            ZingEvent[] traceLogs;
                            traceLogs = states[j].GetTraceLog();

                            for (int k = 0; k < traceLogs.Length; k++)
                            {
                                tracer.Write("  {0}\r\n", traceLogs[k]);
                            }
                        }

                    }
                }
                if (Options.EnableTrace)
                {
                    tracer.Close();
                }
                #endregion
                //Call Plugin End function if plugin specified
                if (pluginDlls.Count != 0)
                {
                    var previousColor = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                    Console.WriteLine("********************************************************************");
                    Console.WriteLine("                         Plugin                                     ");
                    Console.WriteLine("********************************************************************");
                    Console.ForegroundColor = previousColor;
                    foreach (var zingPlugin in pExp.ZingPlugin)
                    {
                        previousColor = Console.ForegroundColor;
                        Console.ForegroundColor = ConsoleColor.DarkGreen;
                        Console.WriteLine("Called {0}.End() at the end of exploration", zingPlugin.Value.GetType().Name);
                        Console.ForegroundColor = previousColor;
                        zingPlugin.Value.End();
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception from Zing model checker:");
                Console.WriteLine(e);
            }
        }

        private static Hashtable ParseLabelFile(string labelFile)
        {
            string[] contents = File.ReadAllLines(labelFile);
            Hashtable methodNameToLabelMap = new Hashtable();
            int i = 0;
            while (i < contents.Length)
            {
                string methodName = contents[i];
                Hashtable labelMap = new Hashtable();
                methodNameToLabelMap[methodName] = labelMap;
                int j = i + 1;
                while (j < contents.Length)
                {
                    string[] separator = new string[1];
                    separator[0] = " ";
                    string[] blockLabel = contents[j].Split(separator, StringSplitOptions.None);
                    if (blockLabel.Length == 1)
                        break;
                    else if (blockLabel.Length == 2)
                        labelMap[blockLabel[0]] = blockLabel[1];
                    else
                        System.Diagnostics.Debug.Assert(false);
                    j++;
                }
                i = j;
            }
            return methodNameToLabelMap;
        }

        private static void Usage(string arg, string error)
        {
            if (error != null)
            {
                if (arg != null)
                    Console.WriteLine("Error: \"{0}\" - {1}", arg, error);
                else
                    Console.WriteLine("Error: {0}", error);
            }

            Console.WriteLine("Usage: zinger [options] <ZingModel>");
            Console.WriteLine("  -help                           display this message (-h or -?)");
            Console.WriteLine("  -randomdfs                      uses random dfs, instead of ordered dfs");
            Console.WriteLine("  -cutoff:<int> | -c:<int>        limit search depth to <depth> steps (-c)");
            Console.WriteLine("  -delayc:<int>                   limit search delay budget to <int>, delay cutoff");
            Console.WriteLine("  -delayi:<int>                   increment the iterative delay bound by <int>");
            Console.WriteLine("  -inc:<int> | -i:<int>           increment amount for depth cutoff (-i)");
            Console.WriteLine("  -multiple | -m                  search all errors, dont stop exploration after one error(-m)");
            Console.WriteLine("  -stats | -s                     show search statistics during exploration");
            Console.WriteLine("  -parallel:<int> | -p:<int>      Use Parallel Optimized Iterative Depth Bounding Search");
            Console.WriteLine("                                  With the specified number of concurrent tasks <default = 1>");
            Console.WriteLine("  -iseq:<int>                     The initial sequential exploration depth <default = 5>");
            Console.WriteLine("  -eo                             Print only the event logs in the error trace");
            Console.WriteLine("  -ct                             Compact the traces used for TraversalInfo Regeneration");
            Console.WriteLine("  -co                             Fingerprint states having single successor with probability x/1000000 <default x = 0>");
            Console.WriteLine("  -et:<filename>                  Dump the trace logs into <filename>");
            Console.WriteLine("  -plugin:<plugin_dll>            Plugin Dll which contains class implementing IZingPlugin Interface");
            Console.WriteLine("  -sched:<scheduler.dll>          Scheduler Dll which contains class implementing IZingDelayingScheduler Interface");
            Console.WriteLine("  -liveness                       To perform liveness search, search for accepting cycles using NDFS <use only with sequential and non-iterative>");
            Console.WriteLine("  -cdfsstack:<int>                Limit the size of DFS Search stack to <int>, if the size of stack exceeds cutoff corresponding error trace is generated");
            Console.WriteLine("  -frontiertodisk                 Flush Frontiers to Disk < Memory Optimization >");
            Console.WriteLine("  -bc                             Bound the internal choice points <used during delay bounding>");
            Console.WriteLine("  -maceliveness:(exhaustivesearchbound,randomwalkbound,finalcutoff) This option uses macemc liveness algorithm. It performs exhaustive search till bound \"exhaustivesearchbound\" and then performs randomwalk till \"finalcutoff\". An error trace is reported if no \"stable\" state is found within randomwalkbound interval");                   
        }

        private static bool CheckZingPlugin(string PluginDll)
        {
            // Initialize zingplugin
            
            var assembly_ = Assembly.LoadFrom(PluginDll);
            if (assembly_.GetTypes().Where(t => (t.GetInterface("IZingPlugin") != null)).Count() != 1 )
            {
                var saveColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("ZingPLugin {0}: Should have (only one) class implementing IZingPlugin Interface", PluginDll);
                Console.ForegroundColor = saveColor;
                return false;
            }
            return true;
        }

        private static bool CheckZingScheduler (string SchedDll)
        {
            // Initialize zingplugin
            var assembly_ = Assembly.LoadFrom(SchedDll);
            if (assembly_.GetTypes().Where(t => (t.GetInterface("IZingDelayingScheduler") != null)).Count() != 1)
            {
                var saveColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Zing Scheduler {0}: Should have (only one) class implementing IZingDelayingScheduler Interface", SchedDll);
                Console.ForegroundColor = saveColor;
                return false;
            }
            return true;
        }

        private class ZingerPeriodicStatsCallback : ICruncherCallback
        {
            private ParallelExplorer pExp;
            DateTime startTime;

            public ZingerPeriodicStatsCallback(ParallelExplorer p)
            {
                pExp = p;
                startTime = DateTime.Now;
            }


            #region ICruncherCallback Members

            public bool CheckCancel()
            {
                if (pExp.NumTotalStates == 0)
                {
                    return false;
                }

                TimeSpan elapsedTime = DateTime.Now.Subtract(startTime);

                int numTransitions = (int)(pExp.NumTotalStates / elapsedTime.TotalSeconds);

                string timeString = string.Format("{0:00}:{1:00}:{2:00}", (int)elapsedTime.TotalHours,
                    elapsedTime.Minutes, elapsedTime.Seconds);

                System.Console.Write("States: {0}", pExp.NumDistinctStates);
                System.Console.Write(", Transitions: {0}", pExp.NumTotalStates);
                System.Console.Write(", MaxDepth: {0}", pExp.MaxDepth);
                System.Console.Write(", Time: {0}", timeString);
                System.Console.Write(", Trans/sec: {0}", numTransitions);
                System.Console.WriteLine("");

                return false;
            }

            #endregion
        }

    }


}
