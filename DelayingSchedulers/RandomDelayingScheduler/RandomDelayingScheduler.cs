using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Zing;

namespace ExternalDelayBoundedScheduler
{
    [Serializable]
    public class RandomDBSchedulerState : ZingerSchedulerState
    {
        public System.Random randGen;
        public List<int> NextSuccessors;
        public int currentProcess;
        public List<bool> isBlocked;
        /// <summary>
        /// default constructor
        /// </summary>
        public RandomDBSchedulerState () : base()
        {
            NextSuccessors = null;
            randGen = new Random(DateTime.Now.Second);
            isBlocked = new List<bool>();
            currentProcess = -1;
        }

        public RandomDBSchedulerState(RandomDBSchedulerState copyThis) : base(copyThis)
        {
            randGen = copyThis.randGen;
            currentProcess = copyThis.currentProcess;
            isBlocked = new List<bool>();
            foreach(var item in copyThis.isBlocked)
            {
                isBlocked.Add(item);
            }
            NextSuccessors = null;
        }
        public override string ToString()
        {
            string ret = "";
            foreach(var item in isBlocked)
            {
                ret = ret + item.ToString() + ","; 
            }
            return ret;
        }

        public override ZingerSchedulerState Clone(bool isCloneForFrontier)
        {
            
            RandomDBSchedulerState cloned = new RandomDBSchedulerState(this);
            if (isCloneForFrontier)
            {
                if (NextSuccessors != null)
                {
                    cloned.NextSuccessors = new List<int>();
                    foreach (var item in NextSuccessors)
                    {
                        cloned.NextSuccessors.Add(item);
                    }
                }
            }
            return cloned;
        }
    }
    
    public class RandomDelayingScheduler : ZingerDelayingScheduler
    {
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

        public override int Next (ZingerSchedulerState zSchedState)
        {
            //Console.WriteLine("Next");
            var SchedState = zSchedState as RandomDBSchedulerState;
            if (SchedState.NextSuccessors == null)
            {
                SchedState.NextSuccessors = SchedState.AllActiveProcessIds.ToList();
            }
            if (SchedState.AllActiveProcessIds.Count() == 0 || SchedState.NextSuccessors.Count() == 0)
                return -1;

            var index = SchedState.randGen.Next(0, SchedState.NextSuccessors.Count);
            var procId = SchedState.NextSuccessors.ElementAt(index);
            SchedState.currentProcess = procId;
            return procId;

        }

        public override void Delay (ZingerSchedulerState zSchedState)
        {
            var SchedState = zSchedState as RandomDBSchedulerState;
            if (SchedState.NextSuccessors == null)
            {
                SchedState.NextSuccessors = SchedState.AllActiveProcessIds.ToList();
            }
            // Drop the element 
            SchedState.NextSuccessors.Remove(SchedState.currentProcess);
            zSchedState.numOfTimesCurrStateDelayed++;
            return;
        }

        public override void OnEnabled(ZingerSchedulerState ZSchedulerState, int targetSM, int sourceSM)
        {
            //Do nothing
        }

        public override void OnBlocked(ZingerSchedulerState ZSchedulerState, int sourceSM)
        {
            //do nothing
        }
        public override bool MaxDelayReached(ZingerSchedulerState zSchedState)
        {
            return zSchedState.numOfTimesCurrStateDelayed > (zSchedState.AllActiveProcessIds.Count() - 1);
        }
    }
}
