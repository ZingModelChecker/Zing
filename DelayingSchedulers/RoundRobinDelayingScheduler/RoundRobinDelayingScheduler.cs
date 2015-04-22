/***********************************************************
 * Scheduler Information:
 * The round-robin(RR) delaying explorer cycles through the processes in process creation order.  
 * It moves to the next task in the list only on a delay or when the current task is completed. 
 * Round-robin explorer has been used in the past (\cite{delaypaper,Thomson2014}) to test multithreaded programs.
 * In our experience, in most of the cases (Table~\ref{tab:resultsTable1}) other delaying explorers perform better than \RR.
 * \RR can be used for finding bugs that manifest through a small number of preemptions or interleaving between processes.
**********************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Zing;

namespace ExternalDelayingExplorer
{
    [Serializable]
    public class RoundRobinDBSchedulerState : ZingerSchedulerState
    {
        //list of enabled processes.
        public List<int> enabledProcesses;
        

        public RoundRobinDBSchedulerState() : base()
        {
            enabledProcesses = new List<int>();
        }

        //copy constructor
        public RoundRobinDBSchedulerState(RoundRobinDBSchedulerState copyThis) : base(copyThis)
        {
            enabledProcesses = new List<int>();
            foreach (var item in copyThis.enabledProcesses)
            {
                enabledProcesses.Add(item);
            }
        }

        //print the string
        public override string ToString()
        {
            string ret = "";
            foreach (var item in enabledProcesses)
            {
                ret = ret + item.ToString() + ",";
            }
            return ret;
        }

        //clone
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
        /// Add the new created process at the end of RR list.
        /// </summary>
        /// <param name="processId"> process Id of the newly created process</param>
        public override void Start(ZingerSchedulerState ZSchedulerState, int processId)
        {
            var SchedState = ZSchedulerState as RoundRobinDBSchedulerState;
            ZSchedulerState.Start(processId);
            //add the process to the enabled processes list
            SchedState.enabledProcesses.Add(processId);
        }

        /// <summary>
        /// This function is called by Zinger whenever a process has finished execution.
        /// Remove the process from list of enabled processes.
        /// </summary>
        /// <param name="processId"> process Id of the completed process</param>
        public override void Finish(ZingerSchedulerState ZSchedulerState, int processId)
        {
            ZSchedulerState.Finish(processId);
            var schedState = ZSchedulerState as RoundRobinDBSchedulerState;
            schedState.enabledProcesses.Remove(processId);
        }

        /// <summary>
        /// Perform the delay operation. Move process at the start of the list to the end.
        /// </summary>
        /// <param name="zSchedState"></param>
        public override void Delay(ZingerSchedulerState zSchedState)
        {
            //Console.WriteLine("Delayed");
            var SchedState = zSchedState as RoundRobinDBSchedulerState;
            if (SchedState.enabledProcesses.Count == 0)
                return;
            //remove the current process and push it at the back of the queue.
            var delayProcess = SchedState.enabledProcesses.ElementAt(0);
            SchedState.enabledProcesses.RemoveAt(0);
            SchedState.enabledProcesses.Add(delayProcess);
            //one delay operation performed
            zSchedState.numOfTimesCurrStateDelayed++;
        }


        /// <summary>
        /// This function is used internally by the ZING explorer.
        /// It checks if we have applied the maximum number of delays in the current state. 
        /// Applying any more delay operations will not lead to new transitions/states being explored.
        /// Maximum delay operations for a state is always (totalEnabledProcesses - 1).
        /// </summary>
        /// <param name="zSchedState"></param>
        /// <returns>If max bound for the given state has reached</returns>
        public override bool MaxDelayReached(ZingerSchedulerState zSchedState)
        {
            var SchedState = zSchedState as RoundRobinDBSchedulerState;
            return zSchedState.numOfTimesCurrStateDelayed > (SchedState.enabledProcesses.Count - 1);
        }

        /// <summary>
        /// Returns the first element in the list.
        /// </summary>
        /// <param name="zSchedState"></param>
        /// <returns>The next process to be executed</returns>
        public override int Next (ZingerSchedulerState zSchedState)
        {
            var SchedState = zSchedState as RoundRobinDBSchedulerState;
            if (SchedState.enabledProcesses.Count == 0)
                return -1;
            else
                return SchedState.enabledProcesses.ElementAt(0);

        }

        /// <summary>
        /// This function is called on a enqueue operation. A process is enabled
        /// if it has messages in its queue to be serviced
        /// </summary>
        /// <param name="ZSchedulerState"></param>
        /// <param name="targetSM">The process that is enabled because of an enqueue</param>
        /// <param name="sourceSM">This parameter is passed for debugging purposes</param>
        public override void OnEnabled(ZingerSchedulerState ZSchedulerState, int targetSM, int sourceSM)
        {
            var SchedState = (ZSchedulerState as RoundRobinDBSchedulerState);
            var procId = SchedState.GetZingProcessId(targetSM);
            if(!SchedState.enabledProcesses.Contains(procId))
                SchedState.enabledProcesses.Add(procId);
        }

        /// <summary>
        /// This function is called when a process is blocked on dequeue.
        /// There are no more events to be serviced and the queue is empty.
        /// </summary>
        /// <param name="ZSchedulerState"></param>
        /// <param name="sourceSM">Process that is blocked</param>
        public override void OnBlocked(ZingerSchedulerState ZSchedulerState, int sourceSM)
        {
            var SchedState = (ZSchedulerState as RoundRobinDBSchedulerState);
            var procId = SchedState.GetZingProcessId(sourceSM);
            SchedState.enabledProcesses.Remove(procId);
        }

        /// <summary>
        /// This function is provided for extending or customizing the delayingExplorer.
        /// </summary>
        /// <param name="ZSchedulerState"></param>
        /// <param name="Params"></param>
        public override void ZingerOperation(ZingerSchedulerState ZSchedulerState, params object[] Params)
        {
            //do nothing
        }
    }
}
