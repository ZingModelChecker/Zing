using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Zing;
using System.IO;
using System.Collections.Concurrent;

namespace ExternalPlugin
{
    public class StateCoveragePlugin : IZingPlugin
    {
        ConcurrentDictionary<string, ConcurrentDictionary<string, bool>> StatesReached;
        public StateCoveragePlugin()
        {
            StatesReached = new ConcurrentDictionary<string, ConcurrentDictionary<string, bool>>();
        }

        public void Invoke(params object[] Params)
        {
            var par1_MachineStringName = (string)Params[0];
            var par2_StateStringName = (string)Params[1];
            if (!StatesReached.ContainsKey(par1_MachineStringName))
            {
                ConcurrentDictionary<string, bool> temp = new ConcurrentDictionary<string, bool>();
                temp.TryAdd(par2_StateStringName, true);
                StatesReached.TryAdd(par1_MachineStringName, temp);
            }
            else
            {
                ConcurrentDictionary<string, bool> temp;
                var getStateList = StatesReached.TryGetValue(par1_MachineStringName, out temp);
                temp.TryAdd(par2_StateStringName, true);
            }
        }

        public void End()
        {
            string fileName = "StateCoveragePlugin_Output.txt";
            StreamWriter logWriter = new StreamWriter(fileName);
            logWriter.WriteLine("************************************************");
            logWriter.WriteLine("               States Coverage                  ");
            logWriter.WriteLine("************************************************");
            logWriter.WriteLine();
            foreach (var Machine in StatesReached)
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
