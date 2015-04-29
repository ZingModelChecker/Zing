/************************************************
 * Scheduler Information:
 * The run to completion (RTC) explorer was introduced in P: Safe event-driven programming language paper for testing device drivers written in P.
 * The default strategy in RTC is to follow the causal sequence of events, giving priority to the receiver of the most recently sent event.
 * When a delay is applied, the highest priority process is moved to the lowest priority position.
 * Even for small values of delay bound, this explorer is able to explore long paths in the program since it follows the chain of generated events.
 * In our experience, this explorer is able to find bugs that are at large depth better than any other explorer.

**************************************************/

using Microsoft.Zing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExternalDelayingExplorer
{
    [Serializable]
    public class RTCDBSchedulerState : ZingerSchedulerState
    {
        //stack to maintain the scheduler information
        public Stack<int> DBStack;

        public RTCDBSchedulerState()
            : base()
        {
            DBStack = new Stack<int>();
        }

        //copy consttuctor
        public RTCDBSchedulerState(RTCDBSchedulerState copyThis)
            : base(copyThis)
        {
            DBStack = new Stack<int>();
            var DbStackList = copyThis.DBStack.ToList();
            DbStackList.Reverse();
            foreach (var item in DbStackList)
            {
                this.DBStack.Push(item);
            }
        }

        //print the scheduler state
        public override string ToString()
        {
            string ret = "";
            foreach (var item in DBStack)
            {
                ret = ret + "," + item.ToString();
            }
            return ret;
        }

        //clone
        public override ZingerSchedulerState Clone(bool isCloneForFrontier)
        {
            RTCDBSchedulerState cloned = new RTCDBSchedulerState(this);
            return cloned;
        }
    }

    public class RunToCompletionDelayingScheduler : ZingerDelayingScheduler
    {
        public RunToCompletionDelayingScheduler()
        {
        }

        /// <summary>
        /// Push newly created process on top of Stack
        /// </summary>
        /// <param name="processId"> process Id of the newly created process</param>
        public override void Start(ZingerSchedulerState zSchedState, int processId)
        {
            var SchedState = zSchedState as RTCDBSchedulerState;
            SchedState.DBStack.Push(processId);
            SchedState.Start(processId);
        }

        /// <summary>
        /// Remove the completed process from Stack so that it is never scheduled again.
        /// </summary>
        /// <param name="processId">Process id to be removed from the stack</param>
        public override void Finish(ZingerSchedulerState zSchedState, int processId)
        {
            var SchedState = zSchedState as RTCDBSchedulerState;
            Stack<int> tempStack = new Stack<int>();
            while (SchedState.DBStack.Count != 0)
            {
                int topStack;
                if ((topStack = SchedState.DBStack.Pop()) == processId)
                {
                    break;
                }
                else
                {
                    tempStack.Push(topStack);
                }
            }

            while (tempStack.Count != 0)
            {
                SchedState.DBStack.Push(tempStack.Pop());
            }
        }

        /// <summary>
        /// This function is called when an enqueue is performed on a process, hence it has a pending event to be serviced.
        /// </summary>
        /// <param name="ZSchedulerState"></param>
        /// <param name="targetSM"> targetSM is the target process in which the event was enqueued.
        /// This process is pushed on top of the stack to follow event enqueue order.</param>
        /// <param name="sourceSM">This parameter is passed for debugging purposes</param>
        public override void OnEnabled(ZingerSchedulerState ZSchedulerState, int targetSM, int sourceSM)
        {
            var SchedState = ZSchedulerState as RTCDBSchedulerState;
            var procId = SchedState.GetZingProcessId(targetSM);

            if (!SchedState.DBStack.Contains(procId))
                SchedState.DBStack.Push(procId);
        }

        /// <summary>
        /// This function is called when a process is blocked. In the context of asynchronous message
        /// passing programs, a process is blocked when its queue is empty and the process is waiting for an event.
        /// The process that gets blocked is poped off the stack.
        /// </summary>
        /// <param name="ZSchedulerState"></param>
        /// <param name="sourceSM">This parameter is passed for debugging purposes</param>
        public override void OnBlocked(ZingerSchedulerState ZSchedulerState, int sourceSM)
        {
            var SchedState = ZSchedulerState as RTCDBSchedulerState;
            if (SchedState.DBStack.Count > 0)
            {
                SchedState.DBStack.Pop();
            }
        }

        /// <summary>
        /// Move the process on top of stack to the bottom of the stack. Moving the process to the bottom of the stack deviates
        /// the scheduler from following the casual order of events (RTC strategy).
        /// </summary>
        public override void Delay(ZingerSchedulerState zSchedState)
        {
            var SchedState = zSchedState as RTCDBSchedulerState;
            if (SchedState.DBStack.Count == 0)
                return;

            var topOfStack = SchedState.DBStack.Pop();
            var tempStack = new Stack<int>();
            while (SchedState.DBStack.Count != 0)
            {
                tempStack.Push(SchedState.DBStack.Pop());
            }
            SchedState.DBStack.Push(topOfStack);
            while (tempStack.Count != 0)
            {
                SchedState.DBStack.Push(tempStack.Pop());
            }

            zSchedState.numOfTimesCurrStateDelayed++;
        }

        /// <summary>
        /// Return process at the top of stack.
        /// This process is executed next and follows the deterministic schedule.
        /// </summary>
        /// <returns>next process to be scheduled</returns>
        public override int Next(ZingerSchedulerState zSchedState)
        {
            var SchedState = zSchedState as RTCDBSchedulerState;
            if (SchedState.DBStack.Count == 0)
                return -1;

            return (int)SchedState.DBStack.Peek();
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
            var SchedState = zSchedState as RTCDBSchedulerState;
            return zSchedState.numOfTimesCurrStateDelayed >= (SchedState.DBStack.Count - 1);
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