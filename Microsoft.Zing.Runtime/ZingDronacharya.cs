using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace Microsoft.Zing
{
    /// <summary>
    /// Stores the workspace information
    /// </summary>
    public struct WorkspaceInfo {
        public int length_X;
        public int length_Y;
    };

    /// <summary>
    /// Stores the configuration information for dronacharya
    /// </summary>
    public class DronacharyaConfiguration
    {
        public string motionPlannerPluginPath;
        public string motionPlannerDllPath;
        public string PcompilerPath;
        public string P_ProgramPath;
        public string P_ProgramDriverPath;
        public WorkspaceInfo ws;

        public DronacharyaConfiguration()
        {
            ws = new WorkspaceInfo();
        }

        public void Initialize(string configFile)
        {
            if(!File.Exists(configFile))
            {
                ZingerUtilities.PrintErrorMessage("The config file passed with -dronacharya does not exist");
                ZingerUtilities.PrintErrorMessage(configFile);
                Environment.Exit(0);
            }
            else
            {
                XmlDocument configXML = new XmlDocument();
                try
                {
                    configXML.Load(configFile);
                    var configFileName = Path.GetFileName(configFile);
                    Console.WriteLine("Loaded the config file : {0}", configFileName);

                    motionPlannerPluginPath = configXML.GetElementsByTagName("PathToMotionPlannerPlugin")[0].InnerText;

                    motionPlannerDllPath = configXML.GetElementsByTagName("PathToMotionPlannerDll")[0].InnerText;
                    //copy the dll file locally
                    if(!File.Exists(motionPlannerDllPath))
                    {
                        ZingerUtilities.PrintErrorMessage("The dll file does not exist");
                        ZingerUtilities.PrintErrorMessage(motionPlannerDllPath);
                        Environment.Exit(0);
                    }
                    else
                    {
                        File.Copy(motionPlannerDllPath, Path.GetFileName(motionPlannerDllPath), true);
                    }
                    //load workspace
                    ws.length_X = int.Parse(configXML.GetElementsByTagName("Length_X")[0].InnerText);
                    ws.length_Y = int.Parse(configXML.GetElementsByTagName("Length_Y")[0].InnerText);

                    //load p compiler path
                    PcompilerPath = configXML.GetElementsByTagName("P_Compiler")[0].InnerText;

                    //load p program path
                    P_ProgramPath = configXML.GetElementsByTagName("P_ProgramDirectory")[0].InnerText;

                    //load p driver path
                    P_ProgramDriverPath = configXML.GetElementsByTagName("P_DriverFile")[0].InnerText;


                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to parse the XML config file");
                    Console.WriteLine(ex.ToString());
                }

            }
        }
    }

    /// <summary>
    /// Stores the scenario for which we have to generate a motion plan
    /// </summary>
    public class GenerateMotionPlanFor
    {
        public int startPosition;
        public int endPosition;
        public List<int> obstacles;

        public GenerateMotionPlanFor()
        {
            obstacles = new List<int>();
        }
        public override int GetHashCode()
        {
            int obsHash = 0;
            int code = 0;
            var intList = obstacles.ToList();
            intList.Add(startPosition);
            intList.Add(endPosition);
            for (int i = 0; i < intList.Count(); ++i)
		    {
			    obsHash = intList[i].GetHashCode();
			    for (int j = 0; j < 4; ++j)
			    {
				    code += (obsHash & 0x000000FF);
				    code += (code << 10);
				    code ^= (code >> 6);
				    obsHash = (obsHash >> 8);
			    }
		    }

		    code += (code << 3);
		    code ^= (code >> 11);
		    code += (code << 15);
		    return 0x40000000 ^ code;
        }
        public override bool Equals(object obj)
        {
            if (obj is GenerateMotionPlanFor)
            {
                var compItem = obj as GenerateMotionPlanFor;
                if (compItem.endPosition == this.endPosition)
                {
                    if (compItem.startPosition == this.startPosition)
                    {
                        if (compItem.obstacles.Count() == this.obstacles.Count())
                        {
                            foreach (var obs in compItem.obstacles)
                            {
                                if (!this.obstacles.Contains(obs))
                                {
                                    return false;
                                }
                            }
                        }
                        else
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }

                return true;
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }


    /// <summary>
    /// Zing interaction with dronacharya
    /// </summary>
    public class ZingDronacharya
    {
        public string configFilepath;

        /// <summary>
        /// Stores the configuration information for the current simulation.
        /// </summary>
        public DronacharyaConfiguration DronaConfiguration;

        /// <summary>
        /// List of all the generate motion plans invocation.
        /// </summary>
        public HashSet<GenerateMotionPlanFor> GenerateMotionPlans;

        public ZingDronacharya(string configFile)
        {
            configFilepath = configFile;
            DronaConfiguration = new DronacharyaConfiguration();
            DronaConfiguration.Initialize(configFilepath);
            GenerateMotionPlans = new HashSet<GenerateMotionPlanFor>();
        }

        /// <summary>
        /// This function is called for each invocation of generateMotionPLan in the plugin
        /// </summary>
        /// <param name="terminalState"></param>
        public void GenerateMotionPlanFor(TraversalInfo terminalState)
        {
            var ex = terminalState.Exception as ZingerInvokeMotionPlanning;
            //add it to the list
            GenerateMotionPlanFor GMP = new Zing.GenerateMotionPlanFor();
            GMP.startPosition = ex.startLocation;
            GMP.endPosition = ex.endLocation;
            GMP.obstacles = ex.obstacles.ToList();
            GenerateMotionPlans.Add(GMP);

            
        }


        public class MotionPlan
        {
            public int start;
            public int end;
            public List<int> Plan;

            public MotionPlan(int s, int e, List<int> p)
            {
                start = s;
                end = e;
                Plan = p.ToList();
            }
        }
        [DllImport("Complan_v2.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool GenerateMotionPlanFor(int startLocation, int endLocation, int[] sequenceObstacles, int obsSize, [In, Out]int[] sequenceOfSteps, [In, Out]ref int stepSize);
        /// <summary>
        /// This function is called at the end of IterativeSearch after exploring all the executions.
        /// </summary>
        public static void RunMotionPlanner(ZingDronacharya zDrona)
        {
            List<MotionPlan> allMotionPlans = new List<MotionPlan>();
            foreach(var motionPlan in zDrona.GenerateMotionPlans)
            {
                int[] outputPath = new int[100];
                int outputSize = 0;
                Console.WriteLine("Invoking Complan:");
                bool result = GenerateMotionPlanFor(motionPlan.startPosition, motionPlan.endPosition, motionPlan.obstacles.ToArray(), motionPlan.obstacles.Count(), outputPath, ref outputSize);
                if (!result)
                {
                    outputPath = new int[1] { -1 };
                    outputSize = 1;
                }
                allMotionPlans.Add(new MotionPlan(motionPlan.startPosition, motionPlan.endPosition, outputPath.Take(outputSize).ToList()));
            }
           
            //generate the new function
            GenerateMotionPlanningModelFunction(zDrona, allMotionPlans);

        }

        public static void RecompileProgram(ZingDronacharya zDrona)
        {
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            process.StartInfo.CreateNoWindow = false;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.FileName = zDrona.DronaConfiguration.PcompilerPath;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.StartInfo.Arguments = zDrona.DronaConfiguration.P_ProgramDriverPath;

            try
            {
                // Start the process with the info we specified.
                // Call WaitForExit and then the using statement will close.
                process.Start();
                process.WaitForExit();
            }
            catch(Exception ex)
            {
                ZingerUtilities.PrintErrorMessage("Failed to compile P program");
                ZingerUtilities.PrintErrorMessage(ex.ToString());
            }
        }

        public static void ReloadProgram(ZingDronacharya zDrona)
        {

        }
        #region Helper functions 
        public static void GenerateMotionPlanningModelFunction(ZingDronacharya zDrona, List<MotionPlan> allMotionPlans)
        {
            string motionPlanningFile = zDrona.DronaConfiguration.P_ProgramPath + "\\MotionPlanning.p";
            if(!File.Exists(motionPlanningFile))
            {
                ZingerUtilities.PrintErrorMessage("Failed to find file: " + motionPlanningFile);
            }
            var motionPlanningFunction = File.ReadAllLines(motionPlanningFile);
            //concatenate the strings
            string newFunction = "";
            foreach(var lines in motionPlanningFunction)
            {
                newFunction += (lines + "\n");
            }

            
            

            string genSeq = "\n tempSeq = default(seq[int]);\n";
            foreach(var MP in allMotionPlans)
            {

                foreach(var point in MP.Plan)
                {
                    string temp1 = String.Format("tempSeq += (sizeof(tempSeq), {0});\n", point);
                    genSeq = genSeq + temp1;
                }
                genSeq = genSeq + String.Format("AllMotionPlans[({0}, {1})] = tempSeq;\n\n", MP.start, MP.end);
            }
            genSeq += "return AllMotionPlans;\n";

            newFunction = newFunction.Replace("return AllMotionPlans;", genSeq);
            
            //update the motion planning file.
            File.Delete(motionPlanningFile);
            StreamWriter sw = new StreamWriter(File.Create(motionPlanningFile));
            sw.WriteLine(newFunction);
            sw.Close();
        }
        #endregion
    }
}
