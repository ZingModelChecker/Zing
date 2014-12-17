using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Zing;

namespace ExternalDelayBoundedScheduler
{
    [Serializable]
    public class RoundRobinDBSchedulerState : ZingerSchedulerState
    {
        //current process in the round robin 
        public int currentProcess;
        //blocking information
        public Dictionary<int, bool> isBlocked;

        public RoundRobinDBSchedulerState() : base()
        {
            currentProcess = 0;
            isBlocked = new Dictionary<int, bool>();
        }

        public RoundRobinDBSchedulerState(RoundRobinDBSchedulerState copyThis) : base(copyThis)
        {
            currentProcess = copyThis.currentProcess;
            isBlocked = new Dictionary<int, bool>();
            foreach (var item in copyThis.isBlocked)
            {
                isBlocked.Add(item.Key, item.Value);
            }
        }

        public override string ToString()
        {
            string ret = "";
            foreach (var item in isBlocked)
            {
                ret = ret + item.ToString() + ",";
            }
            return ret;
        }

        public override ZingerSchedulerState Clone(bool isCloneForFrontier)
        {
            RoundRobinDBSchedulerState cloned = new RoundRobinDBSchedulerState(this);
            return cloned;
        }

    }   
    public class RoundRobinDBSched : ZingerDelayingScheduler
    {

        public RoundRobinDBSched()
        {
    
        }

        /// <summary>
        /// This function is called by Zinger whenever a new process is created.
        /// </summary>
        /// <param name="processId"> process Id of the newly created process</param>
        public override void Start(ZingerSchedulerState ZSchedulerState, int processId)
        {
            ZSchedulerState.Start(processId);
            (ZSchedulerState as RoundRobinDBSchedulerState).isBlocked.Add(processId, false);
        }

        /// <summary>
        /// This function is called by Zinger whenever a process has finished execution.
        /// </summary>
        /// <param name="processId"> process Id of the completed process</param>
        public override void Finish(ZingerSchedulerState ZSchedulerState, int processId)
        {
            ZSchedulerState.Finish(processId);
            var schedState = ZSchedulerState as RoundRobinDBSchedulerState;
            (schedState).isBlocked.Remove(processId);
            schedState.currentProcess = 0;
            
        }

        public override void Delay(ZingerSchedulerState zSchedState)
        {
            //Console.WriteLine("Delayed");
            var SchedState = zSchedState as RoundRobinDBSchedulerState;
            if (SchedState.AllActiveProcessIds.Count == 0)
                return;
            SchedState.currentProcess = (SchedState.currentProcess + 1) % SchedState.AllActiveProcessIds.Count;
            zSchedState.numOfTimesCurrStateDelayed++;
        }

        public override bool MaxDelayReached(ZingerSchedulerState zSchedState)
        {
            var SchedState = zSchedState as RoundRobinDBSchedulerState;
            return zSchedState.numOfTimesCurrStateDelayed > (SchedState.isBlocked.Where(x => x.Value == false).Count() - 1);
        }

        public override int Next (ZingerSchedulerState zSchedState)
        {
            //Console.WriteLine("Next");
            var SchedState = zSchedState as RoundRobinDBSchedulerState;
            if (SchedState.AllActiveProcessIds.Count == 0)
                return -1;
            int iter = 0;
            while (iter < SchedState.AllActiveProcessIds.Count)
            {
                System.Diagnostics.Debug.Assert(SchedState.currentProcess < SchedState.AllActiveProcessIds.Count);
                var currProcessId = SchedState.AllActiveProcessIds[SchedState.currentProcess];

                if (!SchedState.isBlocked[currProcessId])
                    return currProcessId;
                else
                    SchedState.currentProcess = (SchedState.currentProcess + 1) % SchedState.AllActiveProcessIds.Count;
                
                iter++;
            }

            return -1;
        }

        public override void OnEnabled(ZingerSchedulerState ZSchedulerState, int targetSM, int sourceSM)
        {
            var SchedState = (ZSchedulerState as RoundRobinDBSchedulerState);
            var procId = SchedState.GetZingProcessId(targetSM);
            SchedState.isBlocked[procId] = false;
        }

        public override void OnBlocked(ZingerSchedulerState ZSchedulerState, int sourceSM)
        {
            var SchedState = (ZSchedulerState as RoundRobinDBSchedulerState);
            var procId = SchedState.GetZingProcessId(sourceSM);
            SchedState.isBlocked[procId] = true;
        }

        public override void ZingerOperation(ZingerSchedulerState ZSchedulerState, params object[] Params)
        {
            //do nothing
        }
    }
}
