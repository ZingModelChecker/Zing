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
        //List of all the task in the system
        public List<KeyValuePair<int, bool>> TaskList;
        //newly created process
        public int createdProcessId;
        //map from the process id to the state machine id 
        public Dictionary<int, int> ProcessIdMap;
        //current process in the round robin 
        public int currentProcess;
        //is the delaying scheduler sealed
        public bool issealed;

        public DBSchedulerState()
        {
            ProcessIdMap = new Dictionary<int, int>();
            TaskList = new List<KeyValuePair<int,bool>>();
            createdProcessId = -1;
            currentProcess = 0;
            issealed = false;
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

        public IZingSchedulerState Clone()
        {
            DBSchedulerState cloned = new DBSchedulerState();
            cloned.TaskList = TaskList.ToList();
            foreach(var mapId in ProcessIdMap)
            {
                cloned.ProcessIdMap.Add(mapId.Key, mapId.Value);
            }
            cloned.createdProcessId = createdProcessId;
            cloned.currentProcess = currentProcess;
            cloned.issealed = issealed;
            return cloned;
        }

        public IZingSchedulerState CloneF ()
        {
            return Clone();
        }
    }   
    public class RoundRobinDBSched : IZingDelayingScheduler
    {

        public bool IsSealed(IZingSchedulerState zSchedState)
        {
            var SchedState = zSchedState as DBSchedulerState;
            return SchedState.issealed;
        }

        public RoundRobinDBSched()
        {
    
        }

        /// <summary>
        /// Adds the newly created process to the end of the list
        /// </summary>
        /// <param name="zSchedState">scheduler state</param>
        /// <param name="processId">newly created process</param>
        public void Start (IZingSchedulerState zSchedState, uint processId)
        {
            var SchedState = zSchedState as DBSchedulerState;
            SchedState.createdProcessId = (int)processId;
            SchedState.TaskList.Add(new KeyValuePair<int,bool>((int)processId, true));
        }

        /// <summary>
        /// Removes the completed process from the task list
        /// </summary>
        /// <param name="zSchedState">scheduler state</param>
        /// <param name="processId">process to be removed from the task-list</param>
        public void Finish (IZingSchedulerState zSchedState, uint processId)
        {
            //Console.WriteLine("Finished : {0}", processId);
            var SchedState = zSchedState as DBSchedulerState;
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

        /// <summary>
        /// Perform operation corresponding to the invoke scheduler call
        /// </summary>
        /// <param name="zSchedState">scheduler state</param>
        /// <param name="Params">parameters given with the invocation</param>
        public void Invoke (IZingSchedulerState zSchedState, params object[] Params)
        {
            var SchedState = zSchedState as DBSchedulerState;
            var par1_operation = (string)Params[0];
            if (par1_operation == "push")
            {
                //Console.WriteLine("Enable");
                var machineId = (int)Params[1];
                int procId;
                SchedState.ProcessIdMap.TryGetValue(machineId, out procId);
                var enableProcess = SchedState.TaskList.Where(x => (x.Key == procId)).First();
                enableProcess = new KeyValuePair<int,bool>(procId, true);
                
            }
            else if (par1_operation == "pop")
            {
                //Console.WriteLine("Blocked");
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
            else if (par1_operation == "map")
            {
                SchedState.ProcessIdMap.Add((int)Params[1], SchedState.createdProcessId);
            }
            else if (par1_operation == "seal")
            {
                SchedState.issealed = true;
            }
            else if (par1_operation == "unseal")
            {
                SchedState.issealed = false;
            }
            else
            {
                throw new Exception("Invalid Parameter Passed to 'invokescheduler'");
            }
        }

        public void Delay (IZingSchedulerState zSchedState)
        {
            //Console.WriteLine("Delayed");
            var SchedState = zSchedState as DBSchedulerState;
            if (SchedState.TaskList.Count == 0 || SchedState.TaskList.Where((proc) => proc.Value == true).Count() == 0)
                return;
            //SchedState.PrintState();
            bool foundProc = false;
            foreach(var process in SchedState.TaskList)
            {
                if(process.Key == SchedState.currentProcess)
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

            foreach(var process in SchedState.TaskList)
            {
                if(process.Value)
                {
                    SchedState.currentProcess = process.Key;
                    return;
                }
            }
        }

        public int Next (IZingSchedulerState zSchedState)
        {
            //Console.WriteLine("Next");
            var SchedState = zSchedState as DBSchedulerState;
            if (SchedState.TaskList.Count == 0 || SchedState.TaskList.Where((proc) => proc.Value == true).Count() == 0)
                return -1;

            var nextProcess = SchedState.TaskList.Where(proc => proc.Key == SchedState.currentProcess).First();
            System.Diagnostics.Debug.Assert(nextProcess.Value);

            return SchedState.currentProcess;
        }

        public int MaxDelay (IZingSchedulerState zSchedState)
        {
            var SchedState = zSchedState as DBSchedulerState;
            return SchedState.TaskList.Where(proc => proc.Value).Count() - 1;
        }

    }
}
