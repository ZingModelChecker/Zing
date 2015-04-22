/*************************************************
 * Scheduler Information:
 * RandomDelayingScheduler uses a random strategy to prioritize the search space.
 * The next call randomly schedules a process from the list of enabled processes.
 * The delay call removes the last scheduled process from the list. Applying delay operation max times 
 * guarantees that all the processes will be scheduled in a given state. Hence the scheduler is sound.
 * ***********************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Zing;

namespace ExternalDelayingExplorer
{
    [Serializable]
    public class RandomDBSchedulerState : ZingerSchedulerState
    {
        //random number generator
        public System.Random randGen;
        //list of all enabled processes 
        public List<int> EnabledProcesses;
        //last process scheduled (by call to Next).
        public int scheculedProcess;
        //list of not yet delayed processes
        public List<int> setOfProcesses;

        /// <summary>
        /// default constructor
        /// </summary>
        public RandomDBSchedulerState () : base()
        {
            setOfProcesses = null;
            randGen = new Random(DateTime.Now.Millisecond);
            scheculedProcess = -1;
            EnabledProcesses = new List<int>();
        }

        //copy constructor
        public RandomDBSchedulerState(RandomDBSchedulerState copyThis) : base(copyThis)
        {
            randGen = copyThis.randGen; 
            scheculedProcess = copyThis.scheculedProcess;
            setOfProcesses = copyThis.EnabledProcesses.ToList();
            EnabledProcesses = copyThis.EnabledProcesses.ToList();
        }
        public override string ToString()
        {
            string ret = "";
            foreach (var item in setOfProcesses)
            {
                ret = ret + item.ToString() + ","; 
            }
            return ret;
        }

        //clone
        public override ZingerSchedulerState Clone(bool isCloneForFrontier)
        {
            
            RandomDBSchedulerState cloned = new RandomDBSchedulerState(this);
            if (isCloneForFrontier)
            {
               
                cloned.setOfProcesses = new List<int>();
                foreach (var item in setOfProcesses)
                {
                    cloned.setOfProcesses.Add(item);
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
            var schedState = (ZSchedulerState as RandomDBSchedulerState);
            ZSchedulerState.Start(processId);
            schedState.EnabledProcesses.Add(processId);
            schedState.setOfProcesses.Add(processId);

        }

        /// <summary>
        /// This function is called by Zinger whenever a process has finished execution (terminated).
        /// </summary>
        /// <param name="processId"> process Id of the completed process</param>
        public override void Finish(ZingerSchedulerState ZSchedulerState, int processId)
        {
            var schedState = (ZSchedulerState as RandomDBSchedulerState);
            ZSchedulerState.Finish(processId);
            schedState.EnabledProcesses.Remove(processId);
            schedState.setOfProcesses.Remove(processId);
        }

        /// <summary>
        /// Randomly return a process from the set of processes SetOfProcesses not yet delayed.
        /// </summary>
        /// <param name="zSchedState"></param>
        /// <returns></returns>
        public override int Next (ZingerSchedulerState zSchedState)
        {
            var SchedState = zSchedState as RandomDBSchedulerState;
            if (SchedState.setOfProcesses.Count() == 0)
                return -1;

            var index = SchedState.randGen.Next(0, SchedState.setOfProcesses.Count);
            var procId = SchedState.setOfProcesses.ElementAt(index);
            SchedState.scheculedProcess = procId;
            return procId;

        }

        /// <summary>
        /// The Delay operation drops the last scheduled process such that it is never scheduled again 
        /// for that state.
        /// </summary>
        /// <param name="zSchedState"></param>
        public override void Delay (ZingerSchedulerState zSchedState)
        {
            var SchedState = zSchedState as RandomDBSchedulerState;
            // Drop the element 
            SchedState.setOfProcesses.Remove(SchedState.scheculedProcess);
            zSchedState.numOfTimesCurrStateDelayed++;
            return;
        }

        /// <summary>
        /// This function is called on a enqueue operation. A process is enabled
        /// if it has messages in its queue to be serviced
        /// </summary>
        /// <param name="ZSchedulerState"></param>
        /// <param name="targetSM">process is added to the set of enabled processes</param>
        /// <param name="sourceSM"></param>
        public override void OnEnabled(ZingerSchedulerState ZSchedulerState, int targetSM, int sourceSM)
        {
            var SchedState = ZSchedulerState as RandomDBSchedulerState;
            var procId = SchedState.GetZingProcessId(targetSM);
            if (!SchedState.EnabledProcesses.Contains(procId))
                SchedState.EnabledProcesses.Add(procId);
        }

        /// <summary>
        /// This function is called when a process is blocked on dequeue.
        /// There are no more events to be serviced and the queue is empty.
        /// </summary>
        /// <param name="ZSchedulerState"></param>
        /// <param name="sourceSM">Process that is blocked</param>
        public override void OnBlocked(ZingerSchedulerState ZSchedulerState, int sourceSM)
        {
            var SchedState = ZSchedulerState as RandomDBSchedulerState;
            var procId = SchedState.GetZingProcessId(sourceSM);
            SchedState.EnabledProcesses.Remove(procId);
        }
        public override bool MaxDelayReached(ZingerSchedulerState zSchedState)
        {
            var SchedState = zSchedState as RandomDBSchedulerState;
            return zSchedState.numOfTimesCurrStateDelayed > (SchedState.EnabledProcesses.Count() - 1);
        }

        public override void ZingerOperation(ZingerSchedulerState ZSchedulerState, params object[] Params)
        {
            //do nothing
        }
    }
}
