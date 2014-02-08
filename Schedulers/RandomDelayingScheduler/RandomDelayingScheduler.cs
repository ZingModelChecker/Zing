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
        public List<int> TaskSet;
        public int createdProcessId;
        public Dictionary<int, int> ProcessIdMap;
        public System.Random randGen;
        public List<int> chooseProc;
        public int currentProcess;
        public bool issealed;
        public DBSchedulerState ()
        {
            ProcessIdMap = new Dictionary<int, int>();
            TaskSet = new List<int>();
            createdProcessId = -1;
            chooseProc = null;
            randGen = new Random(DateTime.Now.Second);
            issealed = false;
        }

        public void PrintState ()
        {
            Console.Write("|| ");
            foreach (var process in TaskSet)
            {
                Console.Write("<{0},{1}> ", process);
            }
            Console.WriteLine(" ||");
        }

        public IZingSchedulerState Clone ()
        {
            DBSchedulerState cloned = new DBSchedulerState();
            cloned.TaskSet = new List<int>();
            foreach(var item in TaskSet)
            {
                cloned.TaskSet.Add(item);
            }   
            foreach (var mapId in ProcessIdMap)
            {
                cloned.ProcessIdMap.Add(mapId.Key, mapId.Value);
            }
            cloned.createdProcessId = createdProcessId;
            cloned.chooseProc = null;
            cloned.randGen = randGen;
            cloned.currentProcess = currentProcess;
            cloned.issealed = issealed;
            return cloned;
        }

        public IZingSchedulerState CloneF ()
        {
            DBSchedulerState cloned = new DBSchedulerState();
            cloned.TaskSet = new List<int>();
            foreach (var item in TaskSet)
            {
                cloned.TaskSet.Add(item);
            }
            foreach (var mapId in ProcessIdMap)
            {
                cloned.ProcessIdMap.Add(mapId.Key, mapId.Value);
            }
            cloned.createdProcessId = createdProcessId;
            if (chooseProc != null)
            {
                cloned.chooseProc = new List<int>();
                foreach (var item in chooseProc)
                {
                    cloned.chooseProc.Add(item);
                }
            }
            else
            {
                cloned.chooseProc = null;
            }
            cloned.randGen = randGen;
            cloned.currentProcess = currentProcess;
            return cloned;
        }
    }
    
    public class RandomDelayingScheduler : IZingDelayingScheduler
    {
        public bool IsSealed(IZingSchedulerState zSchedState)
        {
            var SchedState = zSchedState as DBSchedulerState;
            return SchedState.issealed;
        }

        /// <summary>
        /// Adds the newly created process to the set with value 0 indicating its enabled
        /// </summary>
        /// <param name="zSchedState">scheduler state</param>
        /// <param name="processId">newly created process</param>
        public void Start (IZingSchedulerState zSchedState, uint processId)
        {
            var SchedState = zSchedState as DBSchedulerState;
            SchedState.createdProcessId = (int)processId;
            SchedState.TaskSet.Add((int)processId);
        }

        /// <summary>
        /// Removes the completed process from the task set
        /// </summary>
        /// <param name="zSchedState">scheduler state</param>
        /// <param name="processId">process to be removed from the task-set</param>
        public void Finish (IZingSchedulerState zSchedState, uint processId)
        {
            //Console.WriteLine("Finished : {0}", processId);
            var SchedState = zSchedState as DBSchedulerState;
            SchedState.TaskSet.Remove((int)processId);
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

            if (par1_operation == "map")
            {
                SchedState.ProcessIdMap.Add((int)Params[1], SchedState.createdProcessId);
            }
            else if(par1_operation == "push" || par1_operation == "pop")
            {
                // do nothing
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

        public int Next (IZingSchedulerState zSchedState)
        {
            //Console.WriteLine("Next");
            var SchedState = zSchedState as DBSchedulerState;
            if (SchedState.chooseProc == null)
            {
                SchedState.chooseProc = SchedState.TaskSet.ToList();
            }
            if (SchedState.TaskSet.Count() == 0 || SchedState.chooseProc.Count() == 0)
                return -1;

            var index = SchedState.randGen.Next(0, SchedState.chooseProc.Count);
            var procId = SchedState.chooseProc.ElementAt(index);
            SchedState.currentProcess = procId;
            return procId;

        }

        public void Delay (IZingSchedulerState zSchedState)
        {
            var SchedState = zSchedState as DBSchedulerState;
            if (SchedState.chooseProc == null)
            {
                SchedState.chooseProc = SchedState.TaskSet.ToList();
            }
            // Drop the element 
            SchedState.chooseProc.Remove(SchedState.currentProcess);
            return;
        }

        public int MaxDelay (IZingSchedulerState zSchedState)
        {

            var SchedState = zSchedState as DBSchedulerState;
            return SchedState.TaskSet.Count();
        }
    }
}
