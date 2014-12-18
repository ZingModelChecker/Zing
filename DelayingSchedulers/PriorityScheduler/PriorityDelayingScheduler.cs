using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Zing;

namespace ExternalDelayBoundedScheduler
{
    [Serializable]
    public class PriorityDBSchedulerState : ZingerSchedulerState
    {
        //priority queue with enable and block information
        public Dictionary<int, KeyValuePair<int, bool>> PriorityMap;
        
        public PriorityDBSchedulerState():base()
        {
            PriorityMap = new Dictionary<int, KeyValuePair<int, bool>>();
        }

        public PriorityDBSchedulerState(PriorityDBSchedulerState copyThis):base(copyThis)
        {
            PriorityMap = new Dictionary<int, KeyValuePair<int, bool>>();
            foreach(var item in copyThis.PriorityMap)
            {
                PriorityMap.Add(item.Key, new KeyValuePair<int, bool>(item.Value.Key, item.Value.Value));
            }
        }

        public override string ToString()
        {
            string ret = "";
            foreach(var item in PriorityMap)
            {
                ret = ret + String.Format("({0} - {1} - {2}), ", item.Key, item.Value.Key, item.Value.Value);
            }
            return ret;
        }

        public override ZingerSchedulerState Clone(bool isCloneForFrontier)
        {
            PriorityDBSchedulerState cloned = new PriorityDBSchedulerState(this);
            return cloned;
        }
    }

    public class PriorityDelayingScheduler : ZingerDelayingScheduler
    {
        public PriorityDelayingScheduler()
        {

        }

        /// <summary>
        /// Add the new process to the priority Map
        /// </summary>
        /// <param name="ZSchedulerState"></param>
        /// <param name="processId"></param>
        public override void Start(ZingerSchedulerState ZSchedulerState, int processId)
        {
            var schedState = ZSchedulerState as PriorityDBSchedulerState;
            schedState.PriorityMap.Add(processId, new KeyValuePair<int, bool>(processId, true));
            schedState.Start(processId);
        }

        /// <summary>
        /// Remove process from the list if completed
        /// </summary>
        /// <param name="ZSchedulerState"></param>
        /// <param name="processId"></param>
        public override void Finish(ZingerSchedulerState ZSchedulerState, int processId)
        {
            var schedState = ZSchedulerState as PriorityDBSchedulerState;
            schedState.Finish(processId);
            schedState.PriorityMap.Remove(processId);
        }

        /// <summary>
        /// Just increment the 
        /// </summary>
        /// <param name="ZSchedulerState"></param>
        public override void Delay(ZingerSchedulerState ZSchedulerState)
        {
            var schedState = ZSchedulerState as PriorityDBSchedulerState;
            var nextId = Next(ZSchedulerState);
            if (nextId == -1)
                return;
            
            schedState.PriorityMap[nextId] = new KeyValuePair<int, bool>(schedState.PriorityMap[nextId].Key + 50, schedState.PriorityMap[nextId].Value);
            ZSchedulerState.numOfTimesCurrStateDelayed++;
        }

        public override bool MaxDelayReached(ZingerSchedulerState ZSchedulerState)
        {
            var schedState = ZSchedulerState as PriorityDBSchedulerState;
            return schedState.numOfTimesCurrStateDelayed > (schedState.PriorityMap.Where(item => item.Value.Value == true).Count() - 1);
        }

        
        public override int Next(ZingerSchedulerState ZSchedulerState)
        {
            var schedState = ZSchedulerState as PriorityDBSchedulerState;
            if (schedState.PriorityMap.Count == 0)
                return -1;

            foreach(var item in schedState.PriorityMap.OrderBy(item => item.Value.Key))
            {
                if(item.Value.Value)
                {
                    return item.Key;
                }
                
            }

            return -1;

        }

        public override void OnBlocked(ZingerSchedulerState ZSchedulerState, int sourceSM)
        {
            var schedState = ZSchedulerState as PriorityDBSchedulerState;
            var procId = schedState.GetZingProcessId(sourceSM);
            schedState.PriorityMap[procId] = new KeyValuePair<int, bool>(schedState.PriorityMap[procId].Key, false);
        }

        public override void OnEnabled(ZingerSchedulerState ZSchedulerState, int targetSM, int sourceSM)
        {
            var schedState = ZSchedulerState as PriorityDBSchedulerState;
            var procId = schedState.GetZingProcessId(targetSM);
            schedState.PriorityMap[procId] = new KeyValuePair<int, bool>(schedState.PriorityMap[procId].Key, true);
        }

        public override void ZingerOperation(ZingerSchedulerState ZSchedulerState, params object[] Params)
        {
            //do nothing
        }

        public override void OtherOperations(ZingerSchedulerState ZSchedulerState, params object[] Params)
        {
            var param1_operation = (string)Params[0];
            var schedState = ZSchedulerState as PriorityDBSchedulerState;
            if(param1_operation == "setpriority")
            {
                var param2_targetSM = (int)Params[1];
                var param3_priority = (int)Params[2];
                var procId = schedState.GetZingProcessId(param2_targetSM);
                schedState.PriorityMap[procId] = new KeyValuePair<int, bool>(param3_priority, schedState.PriorityMap[procId].Value);
            }
        }
    }
}
