using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Zing;

namespace ExternalDelayBoundedScheduler
{
    [Serializable]
    public class DBSchedulerState : IZingSchedulerState
    {
        public Stack<uint> DBStack;
        public int createdProcessId;
        public Dictionary<int, int> ProcessIdMap;

        public DBSchedulerState ()
        {
            ProcessIdMap = new Dictionary<int, int>();
            DBStack = new Stack<uint>();
            createdProcessId = -1;
        }
        public void PrintState ()
        {
            Console.Write("|| ");
            foreach (var process in DBStack)
            {
                Console.Write("{0} ", process);
            }
            Console.WriteLine(" ||");
        }

        public IZingSchedulerState Clone ()
        {
            DBSchedulerState cloned = new DBSchedulerState();
            var DbStackList = DBStack.ToList();
            DbStackList.Reverse();
            foreach (var item in DbStackList)
            {
                cloned.DBStack.Push(item);
            }

            foreach (var mapId in ProcessIdMap)
            {
                cloned.ProcessIdMap.Add(mapId.Key, mapId.Value);
            }

            cloned.createdProcessId = createdProcessId;

            return cloned;
        }

        public IZingSchedulerState CloneF ()
        {
            return Clone();
        }
    }

    public class RunToCompletionDBSched : IZingDelayingScheduler
    {

        private bool isSealed = false;

        public bool IsSealed
        {
            get { return isSealed; }
            set { isSealed = value; }
        }

        public RunToCompletionDBSched ()
        {
        }
        /// <summary>
        /// Push current process on top of Db Stack
        /// </summary>
        /// <param name="processId"> process Id of the newly created process</param>
        public void Start (IZingSchedulerState zSchedState, uint processId)
        {
            var SchedState = zSchedState as DBSchedulerState;
            SchedState.createdProcessId = (int)processId;
            SchedState.DBStack.Push(processId);
        }
        /// <summary>
        /// Remove the process from DB Stack
        /// </summary>
        /// <param name="processId">Process to be removed from the stack</param>
        public void Finish (IZingSchedulerState zSchedState, uint processId)
        {
            var SchedState = zSchedState as DBSchedulerState;
            Stack<uint> tempStack = new Stack<uint>();
            while (SchedState.DBStack.Count != 0)
            {
                uint topStack;
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
        /// Perform operation corresponding to the invoke scheduler call
        /// </summary>
        /// <param name="Params">parameters given with the invocation</param>
        public void Invoke (IZingSchedulerState zSchedState, params object[] Params)
        {

            var SchedState = zSchedState as DBSchedulerState;
            var par1_operation = (string)Params[0];
            if (par1_operation == "push")
            {
                var machineId = (int)Params[1];
                int procId;
                SchedState.ProcessIdMap.TryGetValue(machineId, out procId);
                if (!SchedState.DBStack.Contains((uint)procId))
                    SchedState.DBStack.Push((uint)procId);
                
            }
            else if (par1_operation == "pop")
            {
                if (IsSealed)
                {
                    Delay(zSchedState);
                }
                else
                    SchedState.DBStack.Pop();
            }
            else if (par1_operation == "map")
            {
                SchedState.ProcessIdMap.Add((int)Params[1], SchedState.createdProcessId);
            }
            else if (par1_operation == "seal")
            {
                IsSealed = true;
            }
            else if (par1_operation == "unseal")
            {
                IsSealed = false;
            }
            else
            {
                throw new Exception("Invalid Parameter Passed to 'invokescheduler'");
            }
        }

        /// <summary>
        /// Move the process on top of stack to the bottom of the stack
        /// </summary>
        public void Delay (IZingSchedulerState zSchedState)
        {
            var SchedState = zSchedState as DBSchedulerState;
            if (SchedState.DBStack.Count == 0)
                return;

            var topOfStack = SchedState.DBStack.Pop();
            var tempStack = new Stack<uint>();
            while (SchedState.DBStack.Count != 0)
            {
                tempStack.Push(SchedState.DBStack.Pop());
            }
            SchedState.DBStack.Push(topOfStack);
            while (tempStack.Count != 0)
            {
                SchedState.DBStack.Push(tempStack.Pop());
            }
        }

        /// <summary>
        /// Return process at the top of stack to be scheduled next
        /// </summary>
        /// <returns>next process to be scheduled</returns>
        public int Next (IZingSchedulerState zSchedState)
        {
            var SchedState = zSchedState as DBSchedulerState;
            if (SchedState.DBStack.Count == 0)
                return -1;

            return (int)SchedState.DBStack.Peek();
        }

        public int MaxDelay (IZingSchedulerState zSchedState)
        {
            var SchedState = zSchedState as DBSchedulerState;
            return SchedState.DBStack.Count - 1;
        }

        public bool IsDelaySealed()
        {
            return IsSealed;
        }
    }
}
