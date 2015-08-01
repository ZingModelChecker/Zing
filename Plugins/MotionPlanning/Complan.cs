using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Zing;

namespace ZingExternalPlugin
{
    public class ComplanMotionPlannerState : ZingerPluginState
    {
        public List<int> Obstacles;
        
        public ComplanMotionPlannerState(){
            Obstacles = new List<int>();
        }

        override public ZingerPluginState Clone()
        {
            var cloneVal = new ComplanMotionPlannerState();
            foreach(var item in Obstacles)
            {
                cloneVal.Obstacles.Add(item);
            }

            return cloneVal;
        }

        override public string ToString()
        {
            var output = "";
            foreach (var item in Obstacles)
                output += (" " + item.ToString());

            return output;
        }
    }
    public class ComplanMotionPlanner : ZingerPluginInterface
    {
        private ComplanMotionPlannerState[] complanState;

        public ComplanMotionPlanner()
        {
            complanState = new ComplanMotionPlannerState[ZingerConfiguration.DegreeOfParallelism];
            for(int i = 0; i<ZingerConfiguration.DegreeOfParallelism;i++)
            {
                complanState[i] = new ComplanMotionPlannerState();
            }
        }
        override public void Invoke(int threadID, ZingerPluginState ZPluginState, params object[] Params)
        {
            
            var functionName = (string)Params[0];
            if (functionName == "GenerateMotionPlan")
            {
                int start = (int)Params[1];
                int end = (int)Params[2];
                throw new ZingerInvokeMotionPlanning(start, end, complanState[threadID].Obstacles);
            }
            else if (functionName == "AddObstacle")
            {
                complanState[threadID].Obstacles.Add((int)Params[1]);
            }
            else if (functionName == "ResetObstacle")
            {
                complanState[threadID].Obstacles = new List<int>();
            }
        }

        override public void EndPlugin()
        {
            Console.WriteLine("Complan plugin closed");
        }
    }
}
