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
    /// Main entry point for Zinger.
    /// </summary>
    class Zinger
    {

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
           
            //Parse Command Line and Initialize the ZingerConfiguration
            if(!ZingerCommandLine.ParseCommandLine(args))
            {
                Environment.Exit((int)ZingerResult.InvalidParameters);
            }
            //Infer the Zinger Configuration and make sure all flags are set correctly.
            ZingerConfiguration.InferConfiguration();
            //Print the Current Configuration of Zinger
            ZingerConfiguration.PrintConfiguration();

            try
            {

                ZingerResult result;

                ZingExplorer zingExplorer;
                if (ZingerConfiguration.DoNDFSLiveness)
                    zingExplorer = new ZingExplorerNDFSLiveness();
                else if(ZingerConfiguration.DoRandomWalk)
                    zingExplorer = new ZingExplorerNaiveRandomWalk();
                else
                    zingExplorer = new ZingExplorerAsModelChecker();
                
                //start periodic stats
                if(ZingerConfiguration.PrintStats)
                {
                    ZingerStats.StartPeriodicStats();
                }

                //start the search
                result = zingExplorer.Explore();

                //Print Final Stats
                ZingerStats.PrintFinalStats();

                
             
                //Close operations
                if(ZingerConfiguration.PrintStats)
                {
                    ZingerStats.StopPeriodicStats();
                }

                Environment.Exit((int)result);
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception from Zing model checker:");
                Console.WriteLine(e);
                Environment.Exit((int)ZingerResult.ZingRuntimeError);
            }
        }   

    }


}
