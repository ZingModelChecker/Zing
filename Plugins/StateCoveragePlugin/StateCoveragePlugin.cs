using Microsoft.Zing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExternalPlugin
{
    public class StateCoverageState : ZingerPluginState
    {
        public ConcurrentDictionary<string, ConcurrentDictionary<string, bool>> StatesReached;

        public StateCoverageState()
        {
            StatesReached = new ConcurrentDictionary<string, ConcurrentDictionary<string, bool>>();
        }

        override public ZingerPluginState Clone()
        {
            return this;
        }

        override public string ToString()
        {
            return "";
        }
    }

    public class StateCoveragePlugin : ZingerPluginInterface
    {
        private StateCoverageState sc;

        public StateCoveragePlugin()
        {
            sc = new StateCoverageState();
        }

        override public void Invoke(int threadID, ZingerPluginState ZPluginState, params object[] Params)
        {
            var par1_MachineStringName = (string)Params[1];
            var par2_StateStringName = (string)Params[2];
            if (!sc.StatesReached.ContainsKey(par1_MachineStringName))
            {
                ConcurrentDictionary<string, bool> temp = new ConcurrentDictionary<string, bool>();
                temp.TryAdd(par2_StateStringName, true);
                sc.StatesReached.TryAdd(par1_MachineStringName, temp);
            }
            else
            {
                ConcurrentDictionary<string, bool> temp;
                var getStateList = sc.StatesReached.TryGetValue(par1_MachineStringName, out temp);
                temp.TryAdd(par2_StateStringName, true);
            }
        }

        override public void EndPlugin()
        {
            string fileName = "StateCoveragePlugin_Output.txt";
            StreamWriter logWriter = new StreamWriter(fileName);
            logWriter.WriteLine("************************************************");
            logWriter.WriteLine("               States Coverage                  ");
            logWriter.WriteLine("************************************************");
            logWriter.WriteLine();
            foreach (var Machine in sc.StatesReached)
            {
                foreach (var state in Machine.Value)
                {
                    logWriter.WriteLine("< {0} , {1} >", Machine.Key, state.Key);
                }
            }
            logWriter.WriteLine("************************************************");
            logWriter.Close();
        }
    }
}