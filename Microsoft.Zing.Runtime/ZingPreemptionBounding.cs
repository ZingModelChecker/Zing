using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Zing
{
    /// <summary>
    /// This class stores the necessary information in traversalInfo
    /// for performing preemption bounding.
    /// </summary>
    public class ZingPreemptionBounding
    {
        public bool preempted;

        /// <summary>
        /// stores the current process being executed.
        /// </summary>
        public int currentProcess;

        /// <summary>
        /// list of enabled processes
        /// </summary>
        private List<int> restOfEnabledProcesses;

        //
        /// <summary>
        /// get the next process to be executed.
        /// </summary>
        /// <returns>process id</returns>
        public int GetNextProcessToExecute()
        {
            if (restOfEnabledProcesses.Count == 0)
            {
                return -1;
            }
            var next = restOfEnabledProcesses.ElementAt(0);
            restOfEnabledProcesses.RemoveAt(0);
            currentProcess = next;
            return next;
        }

        public ZingPreemptionBounding()
        {
            restOfEnabledProcesses = new List<int>();
            currentProcess = 0;
            preempted = false;
        }

        /// <summary>
        /// Initialize the object
        /// </summary>
        /// <param name="partialTraversalInfo"></param>
        public ZingPreemptionBounding(ProcessInfo[] processInfo, int numSuccessor, int currentProcessParam)
        {
            int i = 0;
            restOfEnabledProcesses = new List<int>();
            while (i < numSuccessor)
            {
                if (processInfo[i].Status == ProcessStatus.Runnable && i != currentProcess)
                {
                    restOfEnabledProcesses.Add(i);
                }
                i++;
            }
            currentProcess = currentProcessParam;
            preempted = false;
        }

        public ZingPreemptionBounding Clone()
        {
            ZingPreemptionBounding cloned = new ZingPreemptionBounding();
            cloned.preempted = false;
            cloned.currentProcess = currentProcess;
            foreach (var item in restOfEnabledProcesses)
            {
                cloned.restOfEnabledProcesses.Add(item);
            }

            return cloned;
        }
    }
}