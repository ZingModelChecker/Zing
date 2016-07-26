/****************************************************************
 * Scheduler Information:
 * The sealing explorer is used to prune interleavings that are not interesting.
 * For that we provide two seal operations
 * (1) Seal with Run to completion semantics: In this case the scheduler only explores one schedule using 
 * run to completion semantics ignoring the yields during the execution. The seal and unseal operation must be called 
 * appropriately. This type of sealing can be used for making the sends synchronous.
 * (2) Seal with Round robin semantics: In this case the scheduler only explorers one schedule using round-robin 
 * semantics ignoring the yields during the execution. The seal and unseal operation must be invoked appropriately.
 * This type of sealing can be used for making a handler atomic and making some group of sends as broadcast.
 * 
 * By default the scheduler using random strategy/scheduler.
 
 ****************************************************************/

using Microsoft.Zing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace ExternalDelayingExplorer
{
    public class SSSchedulerState : ZingerSchedulerState
    {
        /// <summary>
        /// stack for maintainting the runtocompletion information.
        /// </summary>
        public Stack<int> RTCStack;

        /// <summary>
        /// List of enabled processes to be executed in round-robin fashion 
        /// </summary>
        public List<int> RRList;

        /// <summary>
        /// sealed with RTC
        /// </summary>
        public bool isSealedRTC;

        /// <summary>
        /// sealed with RR
        /// </summary>
        public bool isSealedRR;


        //random number generator
        public System.Random randGen;

        //list of all enabled processes
        public List<int> EnabledProcesses;

        //last process scheduled (by call to Next).
        public int scheculedProcess;

        //list of not yet delayed processes
        public List<int> setOfProcesses;

        public SSSchedulerState() : base()
        {
            RTCStack = new Stack<int>();
            RRList = new List<int>();
            isSealedRR = false;
            isSealedRTC = false;

            //for random scheduler
            setOfProcesses = new List<int>();
            randGen = new Random(DateTime.Now.Millisecond);
            scheculedProcess = -1;
            EnabledProcesses = new List<int>();
        }

        //copy constructor
        public SSSchedulerState(SSSchedulerState copyThis) : base(copyThis)
        {
            RTCStack = new Stack<int>();
            var DbStackList = copyThis.RTCStack.ToList();
            DbStackList.Reverse();
            foreach (var item in DbStackList)
            {
                this.RTCStack.Push(item);
            }

            RRList = copyThis.RRList.ToList();
            isSealedRR = copyThis.isSealedRR;
            isSealedRTC = copyThis.isSealedRTC;

            //random scheduler
            randGen = copyThis.randGen;
            scheculedProcess = copyThis.scheculedProcess;
            setOfProcesses = new List<int>();
            EnabledProcesses = new List<int>();
            foreach (var item in copyThis.EnabledProcesses)
            {
                setOfProcesses.Add(item);
                EnabledProcesses.Add(item);
            }
        }

        public override string ToString()
        {
            //nothing
            return "not implemented";
        }

        public override ZingerSchedulerState Clone(bool isCloneForFrontier)
        {
            SSSchedulerState cloned = new SSSchedulerState(this);
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
    public class SealingScheduler : ZingerDelayingScheduler
    {
        /// <summary>
        /// This function is called by Zinger whenever a new process is created.
        /// </summary>
        /// <param name="processId"> process Id of the newly created process</param>
        public override void Start(ZingerSchedulerState ZSchedulerState, int processId)
        {
            //for random schduler
            var schedState = (ZSchedulerState as SSSchedulerState);
            ZSchedulerState.Start(processId);
            schedState.EnabledProcesses.Add(processId);
            schedState.setOfProcesses.Add(processId);

            //RR sealed
            if(schedState.isSealedRR)
            {
                schedState.RRList.Add(processId);
            }

            //RTC sealed
            if(schedState.isSealedRTC)
            {
                schedState.RTCStack.Push(processId);
            }
        }

        /// <summary>
        /// This function is called by Zinger whenever a process has finished execution (terminated).
        /// </summary>
        /// <param name="processId"> process Id of the completed process</param>
        public override void Finish(ZingerSchedulerState ZSchedulerState, int processId)
        {

            //for random scheduler
            var schedState = (ZSchedulerState as SSSchedulerState);
            schedState.EnabledProcesses.Remove(processId);
            schedState.setOfProcesses.Remove(processId);

            //RR sealed
            if(schedState.isSealedRR)
            {
                schedState.RRList.Remove(processId);
            }

            //RTC sealed
            if(schedState.isSealedRTC)
            {
                Stack<int> tempStack = new Stack<int>();
                while (schedState.RTCStack.Count != 0)
                {
                    int topStack;
                    if ((topStack = schedState.RTCStack.Pop()) == processId)
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
                    schedState.RTCStack.Push(tempStack.Pop());
                }
            }
        }

        /// <summary>
        /// Randomly return a process from the set of processes SetOfProcesses not yet delayed.
        /// </summary>
        /// <param name="zSchedState"></param>
        /// <returns></returns>
        public override int Next(ZingerSchedulerState zSchedState)
        {
            var SchedState = zSchedState as SSSchedulerState;
            //for RR sealed
            if (SchedState.isSealedRR)
            {
                if (SchedState.RRList.Count == 0)
                    Debug.Assert(false, "All processes blocked without calling unsealRR");
                else
                    return SchedState.RRList.ElementAt(0);
            }
            
            // for RTC sealed
            if(SchedState.isSealedRTC)
            {
                if (SchedState.RTCStack.Count == 0)
                    Debug.Assert(false, "All processes blocked without calling unsealRTC");

                return (int)SchedState.RTCStack.Peek();
            }

            //for random scheduler
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
        public override void Delay(ZingerSchedulerState zSchedState)
        {
            
            var SchedState = zSchedState as SSSchedulerState;
            if (SchedState.isSealedRR || SchedState.isSealedRTC)
                Debug.Assert(false, "Delay called when the scheduler is sealed");
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
            //for random scheduler
            var SchedState = ZSchedulerState as SSSchedulerState;
            var procId = SchedState.GetZingProcessId(targetSM);
            if (!SchedState.EnabledProcesses.Contains(procId))
            {
                SchedState.EnabledProcesses.Add(procId);
                SchedState.setOfProcesses.Add(procId);
            }

            //for RR sealed
            if (SchedState.isSealedRR)
            {
                if (!SchedState.RRList.Contains(procId))
                    SchedState.RRList.Add(procId);
            }

            //for RTC sealed
            if(SchedState.isSealedRTC)
            {
                if (!SchedState.RTCStack.Contains(procId))
                    SchedState.RTCStack.Push(procId);
            }
        }

        /// <summary>
        /// This function is called when a process is blocked on dequeue.
        /// There are no more events to be serviced and the queue is empty.
        /// </summary>
        /// <param name="ZSchedulerState"></param>
        /// <param name="sourceSM">Process that is blocked</param>
        public override void OnBlocked(ZingerSchedulerState ZSchedulerState, int sourceSM)
        {
            //for random scheduler
            var SchedState = ZSchedulerState as SSSchedulerState;
            var procId = SchedState.GetZingProcessId(sourceSM);
            // Console.WriteLine(SchedState.ToString());
            SchedState.setOfProcesses.Remove(procId);
            SchedState.EnabledProcesses.Remove(procId);

            //for RR sealed
            if(SchedState.isSealedRR)
            {
                SchedState.RRList.Remove(procId);
            }
            //for RTC sealed
            if(SchedState.isSealedRTC)
            {
                if (SchedState.RTCStack.Count > 0)
                {
                    SchedState.RTCStack.Pop();
                }
            }
        }

        public override bool MaxDelayReached(ZingerSchedulerState zSchedState)
        {
            var SchedState = zSchedState as SSSchedulerState;

            if(SchedState.isSealedRR || SchedState.isSealedRTC)
            {
                return true;
            }

            return zSchedState.numOfTimesCurrStateDelayed >= (SchedState.EnabledProcesses.Count() - 1);
        }

        public override void ZingerOperation(ZingerSchedulerState ZSchedulerState, params object[] Params)
        {
            //do nothing
        }

        public override void OtherOperations(ZingerSchedulerState ZSchedulerState, params object[] Params)
        {
            var SchedState = ZSchedulerState as SSSchedulerState;
            var param1_operation = (string)Params[0];
            if(param1_operation == "sealRTC")
            {
                Debug.Assert(!SchedState.isSealedRR, "sealRTC called when scheduler is sealed with RR");
                SchedState.isSealedRTC = true;
                SchedState.IsSealed = true;
                //control going to the RTC scheduler
                SchedState.RTCStack = new Stack<int>();
                SchedState.RTCStack.Push(SchedState.scheculedProcess);
            }
            else if(param1_operation == "unsealRTC")
            {
                Debug.Assert(SchedState.isSealedRTC, "UnsealRTC called when scheduler is not sealed with RTC");
                SchedState.scheculedProcess = SchedState.RTCStack.Peek();
                SchedState.isSealedRTC = false;
                SchedState.IsSealed = false;
            }
            else if(param1_operation == "sealRR")
            {
                Debug.Assert(!SchedState.isSealedRTC, "sealRR called when scheduler is sealed with RTC");
                SchedState.isSealedRR = true;
                SchedState.IsSealed = true;
                //control going to the RR scheduler
                SchedState.RRList = new List<int>();
                SchedState.RRList.Add(SchedState.scheculedProcess);
                foreach(var proc in SchedState.EnabledProcesses)
                {
                    if(proc != SchedState.scheculedProcess)
                    {
                        SchedState.RRList.Add(proc);
                    }
                }
            }
            else if (param1_operation == "unsealRR")
            {
                Debug.Assert(SchedState.isSealedRR, "UnsealRR called when scheduler is not sealed with RR");
                SchedState.isSealedRR = false;
                SchedState.IsSealed = false;
                SchedState.scheculedProcess = SchedState.RRList.ElementAt(0);
            }
            else
            {
                throw new NotImplementedException("Operation not implemented in the external scheduler");
            }
        }
    }
}
