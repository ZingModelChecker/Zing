using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Zing;

namespace ExternalDelayBoundedScheduler
{
    [Serializable]
    public class PCTDBSchedulerState : ZingerSchedulerState
    {
        public System.Random randGen;
        //priority queue with enable and block information
        public Dictionary<int, KeyValuePair<int, bool>> PriorityMap;

        public PCTDBSchedulerState()
            : base()
        {
            PriorityMap = new Dictionary<int, KeyValuePair<int, bool>>();
            randGen = new Random(DateTime.Now.Millisecond);
        }

        public PCTDBSchedulerState(PCTDBSchedulerState copyThis)
            : base(copyThis)
        {
            PriorityMap = new Dictionary<int, KeyValuePair<int, bool>>();
            foreach (var item in copyThis.PriorityMap)
            {
                PriorityMap.Add(item.Key, new KeyValuePair<int, bool>(item.Value.Key, item.Value.Value));
            }
            randGen = copyThis.randGen;
        }

        public override string ToString()
        {
            string ret = "";
            foreach (var item in PriorityMap)
            {
                ret = ret + String.Format("({0} - {1} - {2}), ", item.Key, item.Value.Key, item.Value.Value);
            }
            return ret;
        }

        public override ZingerSchedulerState Clone(bool isCloneForFrontier)
        {
            PCTDBSchedulerState cloned = new PCTDBSchedulerState(this);
            return cloned;
        }
    }

    public class PCTDelayingScheduler : ZingerDelayingScheduler
    {
        private class UniquePriorityGenerator
        {
            static int low_Range = 100;
            static int high_range = 1000;
            static Random rand = new Random(DateTime.Now.Millisecond);
            public static int GetLowRange()
            {
                return rand.Next(low_Range);
            }

            public static int GetHighRange()
            {
                return rand.Next(low_Range, high_range);
            }

        }
        public PCTDelayingScheduler()
        {

        }

        /// <summary>
        /// Add the new process to the priority Map
        /// </summary>
        /// <param name="ZSchedulerState"></param>
        /// <param name="processId"></param>
        public override void Start(ZingerSchedulerState ZSchedulerState, int processId)
        {
            var schedState = ZSchedulerState as PCTDBSchedulerState;
            
            schedState.PriorityMap.Add(processId, new KeyValuePair<int, bool>(UniquePriorityGenerator.GetLowRange(), true));
            schedState.Start(processId);
        }

        /// <summary>
        /// Remove process from the list if completed
        /// </summary>
        /// <param name="ZSchedulerState"></param>
        /// <param name="processId"></param>
        public override void Finish(ZingerSchedulerState ZSchedulerState, int processId)
        {
            var schedState = ZSchedulerState as PCTDBSchedulerState;
            schedState.Finish(processId);
            schedState.PriorityMap.Remove(processId);
        }

        /// <summary>
        /// Just increment the 
        /// </summary>
        /// <param name="ZSchedulerState"></param>
        public override void Delay(ZingerSchedulerState ZSchedulerState)
        {
            ZSchedulerState.numOfTimesCurrStateDelayed++;
            if(ZingerConfiguration.DoRandomWalk)
            {
                var schedState = ZSchedulerState as PCTDBSchedulerState;
                var nextId = Next(ZSchedulerState);
                if (nextId == -1)
                    return;

                schedState.PriorityMap[nextId] = new KeyValuePair<int,bool>(UniquePriorityGenerator.GetHighRange(), schedState.PriorityMap[nextId].Value);
            }
            else
            {
                var schedState = ZSchedulerState as PCTDBSchedulerState;
                var nextId = Next(ZSchedulerState);
                if (nextId == -1)
                    return;
                schedState.PriorityMap[nextId] = new KeyValuePair<int, bool>(schedState.PriorityMap[nextId].Key + 100, schedState.PriorityMap[nextId].Value);
            }
        }

        public override bool MaxDelayReached(ZingerSchedulerState ZSchedulerState)
        {
            var schedState = ZSchedulerState as PCTDBSchedulerState;
            return schedState.numOfTimesCurrStateDelayed > (schedState.PriorityMap.Where(item => item.Value.Value == true).Count() - 1);
        }


        public override int Next(ZingerSchedulerState ZSchedulerState)
        {
            var schedState = ZSchedulerState as PCTDBSchedulerState;
            if (schedState.PriorityMap.Count == 0)
                return -1;

            foreach (var item in schedState.PriorityMap.OrderBy(item => item.Value.Key))
            {
                if (item.Value.Value)
                {
                    return item.Key;
                }
            }

            return -1;

        }

        public override void OnBlocked(ZingerSchedulerState ZSchedulerState, int sourceSM)
        {
            var schedState = ZSchedulerState as PCTDBSchedulerState;
            var procId = schedState.GetZingProcessId(sourceSM);
            schedState.PriorityMap[procId] = new KeyValuePair<int, bool>(schedState.PriorityMap[procId].Key, false);
        }

        public override void OnEnabled(ZingerSchedulerState ZSchedulerState, int targetSM, int sourceSM)
        {
            var schedState = ZSchedulerState as PCTDBSchedulerState;
            var procId = schedState.GetZingProcessId(targetSM);
            schedState.PriorityMap[procId] = new KeyValuePair<int, bool>(schedState.PriorityMap[procId].Key, true);
        }


        public override void ZingerOperation(ZingerSchedulerState ZSchedulerState, params object[] Params)
        {
            // do nothing
        }
    }
}
