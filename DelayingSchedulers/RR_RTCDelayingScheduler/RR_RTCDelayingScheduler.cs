using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Zing;

namespace RRPlusRTCScheduler
{
    

    public class DBSchedulerState : IZingerSchedulerState
    {
        public enum ModeOfOperation
        {
            RR,
            RTC
        };
        //stack to keep track of the last mode of operation
        public Stack<ModeOfOperation> modestack;
        //current mode of operation
        public ModeOfOperation mode;
        //List of all the task in the system for round robin scheduler
        public List<KeyValuePair<int, bool>> TaskList;
        //newly created process
        public int createdProcessId;
        //map from the process id to the state machine id 
        public Dictionary<int, int> ProcessIdMap;
        //current process in the round robin 
        public int currentProcess;
        //is the delaying scheduler sealed
        public bool issealed;
        //stack for the run to completion 
        public Stack<uint> DBStack;

        public DBSchedulerState()
        {
            ProcessIdMap = new Dictionary<int, int>();
            TaskList = new List<KeyValuePair<int, bool>>();
            createdProcessId = -1;
            currentProcess = 0;
            issealed = false;
            DBStack = new Stack<uint>();
            modestack = new Stack<ModeOfOperation>();
             mode = ModeOfOperation.RR;
        }

        public void PrintState()
        {
            Console.Write("|| ");
            foreach (var process in TaskList)
            {
                Console.Write("{0} ", process);
            }
            Console.WriteLine(" ||");
        }

        public IZingerSchedulerState Clone()
        {
            DBSchedulerState cloned = new DBSchedulerState();
            cloned.TaskList = TaskList.ToList();
            foreach (var mapId in ProcessIdMap)
            {
                cloned.ProcessIdMap.Add(mapId.Key, mapId.Value);
            }
            cloned.createdProcessId = createdProcessId;
            cloned.currentProcess = currentProcess;
            cloned.issealed = issealed;
            var DbStackList = DBStack.ToList();
            DbStackList.Reverse();
            foreach (var item in DbStackList)
            {
                cloned.DBStack.Push(item);
            }
            cloned.mode = mode;
            var modestacklist = modestack.ToList();
            DbStackList.Reverse();
            foreach (var item in modestacklist)
            {
                cloned.modestack.Push(item);
            }
            return cloned;
        }

        public IZingerSchedulerState CloneF()
        {
            return Clone();
        }
    }   

    public class RRPlusRTCScheduler : IZingerDelayingScheduler
    {
        public bool IsSealed(IZingerSchedulerState zSchedState)
        {
            var SchedState = zSchedState as DBSchedulerState;
            return SchedState.issealed;
        }

        public RRPlusRTCScheduler()
        {
        }
        /// <summary>
        /// Push current process on top of Db Stack
        /// </summary>
        /// <param name="processId"> process Id of the newly created process</param>
        public void Start (IZingerSchedulerState zSchedState, uint processId)
        {
            var SchedState = zSchedState as DBSchedulerState;
            SchedState.createdProcessId = (int)processId;
            if (SchedState.mode == DBSchedulerState.ModeOfOperation.RR)
            {
                SchedState.TaskList.Add(new KeyValuePair<int, bool>((int)processId, true));
            }
            else
            {
                SchedState.DBStack.Push(processId);
            }
        }
        /// <summary>
        /// Remove the process from DB Stack
        /// </summary>
        /// <param name="processId">Process to be removed from the stack</param>
        public void Finish (IZingerSchedulerState zSchedState, uint processId)
        {
            var SchedState = zSchedState as DBSchedulerState;
            if (SchedState.mode == DBSchedulerState.ModeOfOperation.RTC)
            {
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
            else
            {
                var finishedTask = SchedState.TaskList.Where(x => (x.Key == processId)).First();
                SchedState.TaskList.Remove(finishedTask);

                if (SchedState.TaskList.Count == 0 || SchedState.TaskList.Where((proc) => proc.Value == true).Count() == 0)
                    return;

                bool foundProc = false;
                foreach (var process in SchedState.TaskList)
                {
                    if (process.Key >= SchedState.currentProcess)
                    {
                        foundProc = true;
                    }

                    if (foundProc && process.Value)
                    {
                        SchedState.currentProcess = process.Key;
                        return;
                    }
                }

                foreach (var process in SchedState.TaskList)
                {
                    if (process.Key == SchedState.currentProcess)
                    {
                        System.Diagnostics.Debug.Assert(false);
                    }
                    if (process.Value)
                    {
                        SchedState.currentProcess = process.Key;
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Perform operation corresponding to the invoke scheduler call
        /// </summary>
        /// <param name="Params">parameters given with the invocation</param>
        public void Invoke (IZingerSchedulerState zSchedState, params object[] Params)
        {

            var SchedState = zSchedState as DBSchedulerState;
            var par1_operation = (string)Params[0];
            if (par1_operation == "push")
            {
                if (SchedState.mode == DBSchedulerState.ModeOfOperation.RTC)
                {
                    var machineId = (int)Params[1];
                    int procId;
                    SchedState.ProcessIdMap.TryGetValue(machineId, out procId);
                    if (!SchedState.DBStack.Contains((uint)procId))
                        SchedState.DBStack.Push((uint)procId);
                }
                else
                {
                    var machineId = (int)Params[1];
                    int procId;
                    SchedState.ProcessIdMap.TryGetValue(machineId, out procId);
                    var enableProcess = SchedState.TaskList.Where(x => (x.Key == procId)).First();
                    enableProcess = new KeyValuePair<int, bool>(procId, true);
                }
                
            }
            else if (par1_operation == "pop")
            {
                if (SchedState.mode == DBSchedulerState.ModeOfOperation.RTC)
                {
                    SchedState.DBStack.Pop();
                }
                else
                {
                    var blockedProcess = SchedState.TaskList.Where(x => (x.Key == SchedState.currentProcess)).First();
                    blockedProcess = new KeyValuePair<int, bool>(SchedState.currentProcess, false);

                    if (SchedState.TaskList.Where((proc) => proc.Value == true).Count() == 0)
                        return;

                    //set the currentProcess appropriately
                    bool foundProc = false;
                    foreach (var process in SchedState.TaskList)
                    {
                        if (process.Key == SchedState.currentProcess)
                        {
                            foundProc = true;
                            continue;
                        }

                        if (foundProc && process.Value)
                        {
                            SchedState.currentProcess = process.Key;
                            return;
                        }
                    }

                    foreach (var process in SchedState.TaskList)
                    {
                        if (process.Key == SchedState.currentProcess)
                        {
                            System.Diagnostics.Debug.Assert(false);
                        }
                        if (process.Value)
                        {
                            SchedState.currentProcess = process.Key;
                            return;
                        }
                    }
                }
            }
            else if (par1_operation == "map")
            {
                SchedState.ProcessIdMap.Add((int)Params[1], SchedState.createdProcessId);
            }
            else if (par1_operation == "seal")
            {
                string sealmode  = "";
                if(Params.Count() > 1)
                {
                    sealmode = (string)Params[1];
                
                    if(sealmode == "rtc")
                    {
                        SchedState.modestack.Push(SchedState.mode);
                        SchedState.mode = DBSchedulerState.ModeOfOperation.RTC;
                        if(SchedState.modestack.Peek() == DBSchedulerState.ModeOfOperation.RR)
                        {
                            SchedState.DBStack.Push((uint)SchedState.currentProcess);
                        }
                    }
                    else
                    {
                        SchedState.modestack.Push(SchedState.mode);
                        SchedState.mode = DBSchedulerState.ModeOfOperation.RR;
                        if (SchedState.modestack.Peek() == DBSchedulerState.ModeOfOperation.RTC)
                        {
                            SchedState.currentProcess = (int)SchedState.DBStack.Peek();
                        }
                    }

                }
                SchedState.issealed = true;
            }
            else if (par1_operation == "unseal")
            {
                SchedState.mode = SchedState.modestack.Pop();
                if (SchedState.mode == DBSchedulerState.ModeOfOperation.RTC)
                {
                    SchedState.DBStack.Push((uint)SchedState.currentProcess);
                }
                else
                {
                    SchedState.currentProcess = (int)SchedState.DBStack.Peek();   
                }
                SchedState.issealed = false;
            }
            else
            {
                throw new Exception("Invalid Parameter Passed to 'invokescheduler'");
            }
        }

        /// <summary>
        /// Move the process on top of stack to the bottom of the stack
        /// </summary>
        public void Delay (IZingerSchedulerState zSchedState)
        {
            var SchedState = zSchedState as DBSchedulerState;
            if (SchedState.mode == DBSchedulerState.ModeOfOperation.RTC)
            {
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
            else
            {
                if (SchedState.TaskList.Count == 0 || SchedState.TaskList.Where((proc) => proc.Value == true).Count() == 0)
                    return;
                //SchedState.PrintState();
                bool foundProc = false;
                foreach (var process in SchedState.TaskList)
                {
                    if (process.Key == SchedState.currentProcess)
                    {
                        foundProc = true;
                        continue;
                    }

                    if (foundProc && process.Value)
                    {
                        SchedState.currentProcess = process.Key;
                        return;
                    }
                }

                foreach (var process in SchedState.TaskList)
                {
                    if (process.Value)
                    {
                        SchedState.currentProcess = process.Key;
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Return process at the top of stack to be scheduled next
        /// </summary>
        /// <returns>next process to be scheduled</returns>
        public int Next (IZingerSchedulerState zSchedState)
        {
            var SchedState = zSchedState as DBSchedulerState;
            if (SchedState.mode == DBSchedulerState.ModeOfOperation.RTC)
            {
                if (SchedState.DBStack.Count == 0)
                    return -1;

                return (int)SchedState.DBStack.Peek();
            }
            else
            {
                if (SchedState.TaskList.Count == 0 || SchedState.TaskList.Where((proc) => proc.Value == true).Count() == 0)
                    return -1;

                var nextProcess = SchedState.TaskList.Where(proc => proc.Key == SchedState.currentProcess).First();
                System.Diagnostics.Debug.Assert(nextProcess.Value);

                return SchedState.currentProcess;
            }
        }

        public int MaxDelay (IZingerSchedulerState zSchedState)
        {

            var SchedState = zSchedState as DBSchedulerState;
            if (SchedState.mode == DBSchedulerState.ModeOfOperation.RTC)
                return SchedState.DBStack.Count - 1;
            else
                return SchedState.TaskList.Where(proc => proc.Value).Count() - 1;
        }
    }
}
