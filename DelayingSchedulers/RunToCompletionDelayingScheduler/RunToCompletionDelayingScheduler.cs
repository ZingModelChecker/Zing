using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Zing;

namespace ExternalDelayBoundedScheduler
{
    [Serializable]
    public class RTCDBSchedulerState : ZingerSchedulerState
    {
        public Stack<int> DBStack;
        public RTCDBSchedulerState () : base()
        {
            DBStack = new Stack<int>();
        }
        
        public RTCDBSchedulerState(RTCDBSchedulerState copyThis) : base(copyThis)
        {
            DBStack = new Stack<int>();
            var DbStackList = copyThis.DBStack.ToList();
            DbStackList.Reverse();
            foreach (var item in DbStackList)
            {
                this.DBStack.Push(item);
            } 
        }

        public override string ToString()
        {
            string ret = "";
            foreach(var item in DBStack)
            {
                ret = ret + "," + item.ToString();
            }
            return ret;
        }

        public override ZingerSchedulerState Clone ()
        {
            RTCDBSchedulerState cloned = new RTCDBSchedulerState(this);
            return cloned;
        }

    }

    public class RunToCompletionDelayingScheduler : ZingerDelayingScheduler
    {


        public RunToCompletionDelayingScheduler ()
        {
        }
        /// <summary>
        /// Push current process on top of Db Stack
        /// </summary>
        /// <param name="processId"> process Id of the newly created process</param>
        public override void Start(ZingerSchedulerState zSchedState, int processId)
        {
            var SchedState = zSchedState as RTCDBSchedulerState;
            SchedState.DBStack.Push(processId);
            SchedState.Start(processId);
        }
        /// <summary>
        /// Remove the process from DB Stack
        /// </summary>
        /// <param name="processId">Process to be removed from the stack</param>
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
            SchedState.Finish(processId);
        }

        public override void OnEnabled(ZingerSchedulerState ZSchedulerState, int targetSM, int sourceSM)
        {
            var SchedState = ZSchedulerState as RTCDBSchedulerState;
            var procId = SchedState.GetZingProcessId(targetSM);
            
            if (!SchedState.DBStack.Contains(procId))
                SchedState.DBStack.Push(procId);

        }
        /// <summary>
        /// Move the process on top of stack to the bottom of the stack
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
        /// Return process at the top of stack to be scheduled next
        /// </summary>
        /// <returns>next process to be scheduled</returns>
        public override int Next(ZingerSchedulerState zSchedState)
        {
            var SchedState = zSchedState as RTCDBSchedulerState;
            if (SchedState.DBStack.Count == 0)
                return -1;

            return (int)SchedState.DBStack.Peek();
        }

        public override bool MaxDelayReached(ZingerSchedulerState zSchedState)
        {
            var SchedState = zSchedState as RTCDBSchedulerState;
            return zSchedState.numOfTimesCurrStateDelayed >= (SchedState.DBStack.Count - 1);

        }
    }
}
