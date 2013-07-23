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
    public class StateVisitCount : IZingPlugin
    {
        public ConcurrentDictionary<string, int> StateVisitCounter;

        public StateVisitCount()
        {
            StateVisitCounter = new ConcurrentDictionary<string, int>();    
        }

        public void Invoke (params object[] Params)
        {
            var par1_StateStringName = (string)Params[0];
            if (!StateVisitCounter.ContainsKey(par1_StateStringName))
            {
                StateVisitCounter.TryAdd(par1_StateStringName, 1);
            }
            else
            {
                int counter = 0;
                StateVisitCounter.TryGetValue(par1_StateStringName, out counter);
                while(!StateVisitCounter.TryUpdate(par1_StateStringName, counter + 1, counter));
            }
        }

        public void End ()
        {
            string fileName = "StateCounterPlugin_Output.txt";
            StreamWriter logWriter = new StreamWriter(fileName);
            logWriter.WriteLine("************************************************");
            logWriter.WriteLine("               States Explored                  ");
            logWriter.WriteLine("************************************************");
            logWriter.WriteLine();
            foreach (var state in StateVisitCounter)
            {
                logWriter.WriteLine("< {0} , {1} >", state.Key, state.Value);
            }
            logWriter.WriteLine("************************************************");
            logWriter.Close();
        }

    }
}
