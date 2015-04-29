/*********************************************************************
 * Scheduler Information:
 * The scheduler custom delaying scheduler with priority associated with each process.
 * The processes are executed in the priority order at each state.
 * Customization function "changePriority" is provided to dynamically change the
 * priority of processes in the round-robin order.
 * *******************************************************************/

using Microsoft.Zing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExternalDelayingExplorer
{
    [Serializable]
    public class CustomDBSchedulerState : ZingerSchedulerState
    {
        //random number generator
        public System.Random randGen;

        //Priority Map for each process (process -> priority).
        public Dictionary<int, int> PriorityMap;

        //all the enabled processes
        public Dictionary<int, int> EnabledProcessesWithPriority;

        //sorted list over which the explorer does a round robin.
        public List<int> sortedProcesses;

        public CustomDBSchedulerState()
            : base()
        {
            randGen = new Random();
            PriorityMap = new Dictionary<int, int>();
            EnabledProcessesWithPriority = new Dictionary<int, int>();
            sortedProcesses = new List<int>();
        }

        public CustomDBSchedulerState(CustomDBSchedulerState copyThis)
            : base(copyThis)
        {
            randGen = copyThis.randGen;
            PriorityMap = new Dictionary<int, int>();
            foreach (var item in copyThis.PriorityMap)
            {
                PriorityMap.Add(item.Key, item.Value);
            }
            EnabledProcessesWithPriority = new Dictionary<int, int>();
            foreach (var item in copyThis.EnabledProcessesWithPriority)
            {
                EnabledProcessesWithPriority.Add(item.Key, item.Value);
            }
            sortedProcesses = new List<int>();
            foreach (var item in EnabledProcessesWithPriority.OrderBy(i => i.Value))
            {
                sortedProcesses.Add(item.Key);
            }
        }

        public override string ToString()
        {
            string ret = "";
            foreach (var item in EnabledProcessesWithPriority)
            {
                ret = ret + String.Format("({0} - {1}), ", item.Key, item.Value);
            }
            foreach (var item in sortedProcesses)
            {
                ret = ret + String.Format("--", item);
            }
            return ret;
        }

        public override ZingerSchedulerState Clone(bool isCloneForFrontier)
        {
            CustomDBSchedulerState cloned = new CustomDBSchedulerState(this);
            if (isCloneForFrontier)
            {
                cloned.EnabledProcessesWithPriority = new Dictionary<int, int>();
                foreach (var item in EnabledProcessesWithPriority)
                {
                    cloned.EnabledProcessesWithPriority.Add(item.Key, item.Value);
                }
                cloned.sortedProcesses = new List<int>();
                foreach (var item in sortedProcesses)
                {
                    cloned.sortedProcesses.Add(item);
                }
            }
            return cloned;
        }
    }

    public class CustomDelayingScheduler : ZingerDelayingScheduler
    {
        public CustomDelayingScheduler()
        {
        }

        /// <summary>
        /// This function is called by Zinger whenever a new process is created.
        /// Add the new created process to the enabledProcesses List and also assign random priority.
        /// </summary>
        /// <param name="processId"> process Id of the newly created process</param>
        public override void Start(ZingerSchedulerState ZSchedulerState, int processId)
        {
            var schedState = ZSchedulerState as CustomDBSchedulerState;
            schedState.PriorityMap.Add(processId, schedState.randGen.Next());
            schedState.EnabledProcessesWithPriority.Add(processId, schedState.PriorityMap[processId]);
            schedState.sortedProcesses.Add(processId);
            schedState.Start(processId);
        }

        /// <summary>
        /// Remove process from the list if completed
        /// </summary>
        /// <param name="ZSchedulerState"></param>
        /// <param name="processId"></param>
        public override void Finish(ZingerSchedulerState ZSchedulerState, int processId)
        {
            var schedState = ZSchedulerState as CustomDBSchedulerState;
            schedState.PriorityMap.Remove(processId);
            schedState.EnabledProcessesWithPriority.Remove(processId);
            schedState.sortedProcesses.Remove(processId);
        }

        /// <summary>
        /// remove the process at the start of the sortedList and push it at the end.
        /// </summary>
        /// <param name="ZSchedulerState"></param>
        public override void Delay(ZingerSchedulerState ZSchedulerState)
        {
            var schedState = ZSchedulerState as CustomDBSchedulerState;
            if (schedState.sortedProcesses.Count == 0)
            {
                return;
            }

            var delayProcess = schedState.sortedProcesses.ElementAt(0);
            schedState.sortedProcesses.RemoveAt(0);
            schedState.sortedProcesses.Add(delayProcess);
            //one delay operation performed
            ZSchedulerState.numOfTimesCurrStateDelayed++;
        }

        /// <summary>
        /// This function is used internally by the ZING explorer.
        /// It checks if we have applied the maximum number of delays in the current state.
        /// Applying any more delay operations will not lead to new transitions/states being explored.
        /// Maximum delay operations for a state is always (totalEnabledProcesses - 1).
        /// </summary>
        /// <param name="zSchedState"></param>
        /// <returns>If max bound for the given state has reached</returns>
        public override bool MaxDelayReached(ZingerSchedulerState ZSchedulerState)
        {
            var schedState = ZSchedulerState as CustomDBSchedulerState;
            return schedState.numOfTimesCurrStateDelayed > (schedState.EnabledProcessesWithPriority.Count() - 1);
        }

        /// <summary>
        /// Returns the first element in the list.
        /// </summary>
        /// <param name="zSchedState"></param>
        /// <returns>The next process to be executed</returns>
        public override int Next(ZingerSchedulerState ZSchedulerState)
        {
            var schedState = ZSchedulerState as CustomDBSchedulerState;
            if (schedState.sortedProcesses.Count == 0)
                return -1;
            else
            {
                return schedState.sortedProcesses.ElementAt(0);
            }
        }

        /// <summary>
        /// This function is called when a process is blocked on dequeue.
        /// There are no more events to be serviced and the queue is empty.
        /// </summary>
        /// <param name="ZSchedulerState"></param>
        /// <param name="sourceSM">Process that is blocked</param>
        ///
        public override void OnBlocked(ZingerSchedulerState ZSchedulerState, int sourceSM)
        {
            var schedState = ZSchedulerState as CustomDBSchedulerState;
            var procId = schedState.GetZingProcessId(sourceSM);
            schedState.EnabledProcessesWithPriority.Remove(procId);
            schedState.sortedProcesses.Remove(procId);
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
            var schedState = ZSchedulerState as CustomDBSchedulerState;
            var procId = schedState.GetZingProcessId(targetSM);
            if (!schedState.EnabledProcessesWithPriority.ContainsKey(procId))
            {
                schedState.EnabledProcessesWithPriority.Add(procId, schedState.PriorityMap[procId]);
                schedState.sortedProcesses.Add(procId);
            }
        }

        public override void ZingerOperation(ZingerSchedulerState ZSchedulerState, params object[] Params)
        {
            //do nothing
        }

        public override void OtherOperations(ZingerSchedulerState ZSchedulerState, params object[] Params)
        {
            var param1_operation = (string)Params[0];
            var schedState = ZSchedulerState as CustomDBSchedulerState;
            if (param1_operation == "changepriority")
            {
                var param2_priority = (int)Params[1];
                var currentProcess = schedState.sortedProcesses.ElementAt(0);
                schedState.PriorityMap[currentProcess] = param2_priority;
                schedState.EnabledProcessesWithPriority[currentProcess] = param2_priority;
            }
        }
    }
}