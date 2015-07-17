using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;

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
        public string motionPlannerEXEPath;
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
                    motionPlannerEXEPath = configXML.GetElementsByTagName("PathToMotionPlannerEXE")[0].InnerText;
                    ws.length_X = int.Parse(configXML.GetElementsByTagName("Length_X")[0].InnerText);
                    ws.length_Y = int.Parse(configXML.GetElementsByTagName("Length_Y")[0].InnerText);

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to parse the XML config file");
                    Console.WriteLine(ex.ToString());
                }

            }
        }
    }

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

            Console.WriteLine("In ZingDronacharya : {0}-{1}", ex.startLocation, ex.endLocation);
            Console.WriteLine("Obstacles: {0}", ex.obstacles.Count());
        }

        public void RunMotionPlanner()
        {
            string inputFile = "Input_MotionPlanner.txt";
            string outputFile = "Output_MotionPlanner.txt";

            //Dump all the inputs in a file
            StreamWriter sw = new StreamWriter(File.Open(inputFile, FileMode.Create));
            foreach (var mp in GenerateMotionPlans)
            {
                sw.Write(String.Format("{0} {1} ", mp.startPosition, mp.endPosition));
                foreach (var obs in mp.obstacles)
                {
                    sw.Write(String.Format("{0} ", obs));
                }
                sw.WriteLine();
            }
            sw.Close();

        }
    }
}
