using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Microsoft.Zing
{
    /// <summary>
    /// Zing Commandline Parsing and Setting up Zing Configuration
    /// </summary>
    public class ZingerCommandLine
    {
        public static bool ParseCommandLine(string[] args)
        {
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
                        PrintZingerHelp(arg, "Malformed option");
                        return false;
                    }

                    switch (option)
                    {
                        case "?":
                        case "h":
                            {
                                PrintZingerHelp(null, null);
                                Environment.Exit((int)ZingerResult.Success);
                                break;
                            }
                        case "fbound":
                            ZingerConfiguration.zBoundedSearch.FinalExecutionCutOff = int.Parse(param);
                            break;
                        case "ibound":
                            ZingerConfiguration.zBoundedSearch.IterativeIncrement = int.Parse(param);
                            break;
                        case "p":
                            if (param.Length == 0)
                            {
                                ZingerConfiguration.DegreeOfParallelism = Environment.ProcessorCount;
                            }
                            else
                            {
                                ZingerConfiguration.DegreeOfParallelism = int.Parse(param);
                            }
                            break;
                        case "m":
                        case "multiple":
                            ZingerConfiguration.StopOnError = false;
                            break;
                        case "s":
                        case "stats":
                            ZingerConfiguration.PrintStats = true;
                            break;
                        case "et":
                            ZingerConfiguration.EnableTrace = true;
                            ZingerConfiguration.traceLogFile = param;
                            break;
                        case "entirezingtrace":
                            ZingerConfiguration.DetailedZingTrace = true;
                            break;
                        case "ct":
                            ZingerConfiguration.CompactTraces = true;
                            break;
                        case "frontiertodisk":
                            ZingerConfiguration.FrontierToDisk = true;
                            break;
                        case "co":
                            ZingerConfiguration.NonChooseProbability = double.Parse(param);
                            break;
                        case "maxmemory":
                            ZingerConfiguration.MaxMemoryConsumption = double.Parse(param);
                            break;
                        case "maxdfsstack":
                            ZingerConfiguration.BoundDFSStackLength = int.Parse(param);
                            break;
                        case "randomwalk":
                            {
                                if(param.Length != 0)
                                {
                                    ZingerConfiguration.MaxSchedulesPerIteration = int.Parse(param);
                                }
                                ZingerConfiguration.DoRandomWalk = true;
                            }
                            break;
                        case "stateless":
                            ZingerConfiguration.DoStateLess = true;
                            break;
                        case "delayb":
                            {
                                ZingerConfiguration.DoDelayBounding = true;
                                ZingerConfiguration.delayingSchedDll = param;
                                try
                                {
                                    //check if the file exists
                                    if(!File.Exists(ZingerConfiguration.delayingSchedDll))
                                    {
                                        PrintZingerHelp(option, String.Format("File {0} not found", ZingerConfiguration.delayingSchedDll));
                                    }

                                    var schedAssembly = Assembly.LoadFrom(ZingerConfiguration.delayingSchedDll);
                                    if (schedAssembly.GetTypes().Where(t => (t.BaseType.Name == "ZingerDelayingScheduler")).Count() != 1)
                                    {

                                        ZingerUtilities.PrintErrorMessage(String.Format("Zing Scheduler {0}: Should have (only one) class inheriting the base class ZingerDelayingScheduler", ZingerConfiguration.delayingSchedDll));
                                        return false;
                                    }

                                    if (schedAssembly.GetTypes().Where(t => (t.BaseType.Name == "ZingerSchedulerState")).Count() != 1)
                                    {

                                        ZingerUtilities.PrintErrorMessage(String.Format("Zing Scheduler {0}: Should have (only one) class inheriting the base class IZingerSchedulerState", ZingerConfiguration.delayingSchedDll));
                                        return false;
                                    }
                                    // get class name 
                                    string schedClassName = schedAssembly.GetTypes().Where(t => (t.BaseType.Name == "ZingerDelayingScheduler")).First().FullName;
                                    var schedStateClassName = schedAssembly.GetTypes().Where(t => (t.BaseType.Name == "ZingerSchedulerState")).First().FullName;
                                    var schedClassType = schedAssembly.GetType(schedClassName);
                                    var schedStateClassType = schedAssembly.GetType(schedStateClassName);
                                    ZingerConfiguration.ZExternalScheduler.zDelaySched = Activator.CreateInstance(schedClassType) as ZingerDelayingScheduler;
                                    ZingerConfiguration.ZExternalScheduler.zSchedState = Activator.CreateInstance(schedStateClassType) as ZingerSchedulerState;
                                }
                                catch(Exception e)
                                {
                                    ZingerUtilities.PrintErrorMessage(String.Format("Passed dll {0} implementing delaying scheduler is Invalid", Path.GetFileName(ZingerConfiguration.delayingSchedDll)));
                                    ZingerUtilities.PrintErrorMessage(e.Message);
                                    return false;
                                }
                            }
                            break;
                        case "bc":
                            ZingerConfiguration.BoundChoices = true;
                            ZingerConfiguration.zBoundedSearch.FinalChoiceCutOff = int.Parse(param);
                            break;
                        case "ndfsliveness":
                            ZingerConfiguration.DoNDFSLiveness = true;
                            break;
                        case "maceliveness":
                            ZingerConfiguration.DoMaceliveness = true;
                            if (param.Length == 0)
                            {
                                //Use the default parameters
                                ZingerConfiguration.MaceLivenessConfiguration = new ZingerMaceLiveness();
                            }
                            else
                            {
                                var parameters = Regex.Match(param, "([0-9]*,[0-9]*,[0-9]*)").Groups[0].ToString();
                                var bounds = parameters.Split(',');
                                if(bounds.Count() != 3)
                                {
                                    PrintZingerHelp(arg, "Invalid parameters passed to maceliveness");
                                    return false;
                                }
                                else
                                {
                                    ZingerConfiguration.MaceLivenessConfiguration = new ZingerMaceLiveness(int.Parse(bounds[0]), int.Parse(bounds[1]), int.Parse(bounds[2]));
                                }
                            }
                            break;
                        case "mapliveness":
                            ZingerConfiguration.DoMAPLiveness = true;
                            break;
                        default:
                            PrintZingerHelp(arg, "Invalid Option");
                            return false;
                    }

                }
                else
                {
                    if(ZingerConfiguration.ZingModelFile != "")
                    {
                        PrintZingerHelp(arg, "Only one Zing model may be referenced");
                        return false;
                    }

                    if(!File.Exists(arg))
                    {
                        PrintZingerHelp(arg, "Can't find Zing Assembly");
                        return false;
                    }

                    ZingerConfiguration.ZingModelFile = arg;
                }
            }

            if(ZingerConfiguration.ZingModelFile == "")
            {
                PrintZingerHelp(null, "No Zing Model Specified");
                return false;
            }
            return true;

        }
        public static void PrintZingerHelp(string arg, string errorMessage)
        {
            if (errorMessage != null)
            {
                if (arg != null)
                    ZingerUtilities.PrintErrorMessage(String.Format("Error: \"{0}\" - {1}", arg, errorMessage));
                else
                    ZingerUtilities.PrintErrorMessage(String.Format("Error: {0}", errorMessage));
            }

            Console.WriteLine("Usage: zinger [options] <ZingModel>");
            Console.WriteLine("-h | -?     display this message (-h or -?)");
            Console.WriteLine("===========================");
            Console.WriteLine("Iterative Bounding Options:");
            Console.WriteLine("---------------------------");
            Console.WriteLine("-fBound:<int>");
            Console.WriteLine("Final Cutoff or Maximum bound in the case of depth or delay bounding. Default value is (int max)\n");
            Console.WriteLine("-iBound:<int>");
            Console.WriteLine("Iterative increment bound in the case of depth or delay bounding. Default value is 1\n");
            Console.WriteLine();
            Console.WriteLine("===========================");
            Console.WriteLine("Zinger Configuration:");
            Console.WriteLine("---------------------------");
            Console.WriteLine("-p | -p:<n>");
            Console.WriteLine("Degree of Parallelism during Search. -p would create no_of_worker_threads = no_of_cores.");
            Console.WriteLine("You can control the degree of parallelism by using -p:<n>, in that case no_of_worker_threads = n\n");
            Console.WriteLine("-m | -multiple");
            Console.WriteLine("Find all bugs in the model. Dont stop exploration after finding first bug.\n");
            Console.WriteLine("-s | -stats");
            Console.WriteLine("Print Search Statistics after each Iterative Bound\n");
            Console.WriteLine("-et:<filename>");
            Console.WriteLine("Dump the generated (only trace statements) Zing error trace in file.\n");
            Console.WriteLine("-entireZingTrace");
            Console.WriteLine("Genererates detailed Zing Stack Error trace.\n");
            Console.WriteLine();
            Console.WriteLine("===========================");
            Console.WriteLine("Zinger Optimizations:");
            Console.WriteLine("---------------------------");
            Console.WriteLine("-ct");
            Console.WriteLine("Use trace compaction, steps from states that have single successor are not stored.\n");
            Console.WriteLine("-frontiersToDisk");
            Console.WriteLine("Dump frontier states after each iteration into files on disk.");
            Console.WriteLine("This option should be used when you dont want store frontiers in memory.");
            Console.WriteLine("The search will be severely slowed down because of disk access but memory consumption is minimal\n");
            Console.WriteLine("-co:n");
            Console.WriteLine("Fingerprint states having single successor with probability n/1000000 <default n = 0>.\n");
            Console.WriteLine("-maxMemory:<double>GB");
            Console.WriteLine("Maximum memory consumption during stateful search. After process consumes 70% ofmax_size (e.g. 2GB).");
            Console.WriteLine("States are randomly replaced from the state table and frontiers are stored on disk.\n");
            Console.WriteLine();
            Console.WriteLine("===========================");
            Console.WriteLine("Zinger Search Strategy:");
            Console.WriteLine("---------------------------");
            Console.WriteLine("-maxDFSStack:<int>");
            Console.WriteLine("Maximum size of the DFS search stack. A counter example is generated if size of the stack exceeds the bound.\n");
            Console.WriteLine("-randomWalk:<int>");
            Console.WriteLine("Zinger performs random walk without DFS stack. <int> represents the maximum number of schedules explored per iteration. (default is int max).\n");
            Console.WriteLine("-stateless");
            Console.WriteLine("Zinger performs stateless search. No state caching ! (default is stateful)\n");
            Console.WriteLine("-delayB:<scheduler.dll>");
            Console.WriteLine("Zinger performs delay bounding using the deterministic scheduler (scheduler.dll).\n");
            Console.WriteLine("-bc:<int>");
            Console.WriteLine("Bound the choice operations or bound the number of times choose(bool) returns true."); 
            Console.WriteLine("The default value is \"false\" for choose(bool), choice budget is used each time true is returned.\n");
            Console.WriteLine();
            Console.WriteLine("===========================");
            Console.WriteLine("Zinger Search Strategy For Liveness:");
            Console.WriteLine("---------------------------");
            Console.WriteLine("-NDFSliveness");
            Console.WriteLine("To perform liveness search using NDFS <use only with sequential and non-iterative>\n");
            Console.WriteLine("-maceliveness:(exhaustivesearchbound,livestatebound,finalcutoff)");
            Console.WriteLine("This option uses MaceMC liveness algorithm.");
            Console.WriteLine("It performs exhaustive search till bound exhaustivesearchbound and then performs randomwalk till finalcutoff.");
            Console.WriteLine("error trace is reported if no \"cold\" state is found within livestatebound interval\n");
            Console.WriteLine("-MAPLiveness");
            Console.WriteLine("Uses MAP cycle detection algorithm for finding accepting cycles. Can be used with Parallelism.\n");
        
        }

    }
}
