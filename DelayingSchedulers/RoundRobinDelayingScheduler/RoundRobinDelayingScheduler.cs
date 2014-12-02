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

        public RoundRobinDBSchedulerState() : base()
        {
            currentProcess = 0;
        }

        public RoundRobinDBSchedulerState(RoundRobinDBSchedulerState copyThis) : base(copyThis)
        {
            currentProcess = copyThis.currentProcess;
        }

        public override string ToString()
        {
            throw new NotImplementedException();
        }   

        public override ZingerSchedulerState Clone()
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
        }

        /// <summary>
        /// This function is called by Zinger whenever a process has finished execution.
        /// </summary>
        /// <param name="processId"> process Id of the completed process</param>
        public override void Finish(ZingerSchedulerState ZSchedulerState, int processId)
        {
            ZSchedulerState.Finish(processId);
        }

        public override void Delay(ZingerSchedulerState zSchedState)
        {
            //Console.WriteLine("Delayed");
            var SchedState = zSchedState as RoundRobinDBSchedulerState;
            if (SchedState.AllActiveProcessIds.Count == 0)
                return;
            SchedState.currentProcess++;
            zSchedState.numOfTimesCurrStateDelayed++;
        }

        public override bool MaxDelayReached(ZingerSchedulerState zSchedState)
        {
            return zSchedState.numOfTimesCurrStateDelayed > (zSchedState.AllActiveProcessIds.Count() - 1);
        }

        public override int Next (ZingerSchedulerState zSchedState)
        {
            //Console.WriteLine("Next");
            var SchedState = zSchedState as RoundRobinDBSchedulerState;
            if (SchedState.AllActiveProcessIds.Count == 0)
                return -1;
            if (SchedState.currentProcess < SchedState.AllActiveProcessIds.Count())
                return SchedState.AllActiveProcessIds.ElementAt(SchedState.currentProcess);
            else
            {
                SchedState.currentProcess = 0;
                return SchedState.AllActiveProcessIds.ElementAt(SchedState.currentProcess);
            }
        }

        public override void OnEnabled(ZingerSchedulerState ZSchedulerState, int targetSM, int sourceSM)
        {
            // do nothing
        }

    }
}
