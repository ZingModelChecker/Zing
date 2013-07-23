using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Zing
{
    public interface IZingSchedulerState
    {
        /// <summary>
        /// Print the current scheduler state
        /// </summary>
        void PrintState ();

        /// <summary>
        /// Clone current scheduler state for new zing state.
        /// </summary>
        /// <returns></returns>
        IZingSchedulerState Clone ();
        
        /// <summary>
        /// Clone current scheduler state for current state pushed on Frontier
        /// </summary>
        /// <returns></returns>
        IZingSchedulerState CloneF ();
    }
    public interface IZingDelayingScheduler
    {
        /// <summary>
        /// This function is called by Zinger whenever a new process is created.
        /// </summary>
        /// <param name="
        /// "> process Id of the newly created process</param>
        void Start (IZingSchedulerState ZSchedulerState, uint processId);

        /// <summary>
        /// This function is called by Zinger whenever a process has finished execution.
        /// </summary>
        /// <param name="processId"> process Id of the completed process</param>
        void Finish (IZingSchedulerState ZSchedulerState, uint processId);

        /// <summary>
        /// This function is called in response to invoke-scheduler function in the zing model.
        /// </summary>
        /// <param name="Params"></param>
        void Invoke (IZingSchedulerState ZSchedulerState, params object[] Params);

        /// <summary>
        /// This function is called by Zinger to delay the DBScheduler.
        /// </summary>
        void Delay (IZingSchedulerState ZSchedulerState);

        /// <summary>
        /// This function is called by Zinger to obtain next process to be scheduled.
        /// </summary>
        /// <returns>process Id of next process to be scheduled</returns>
        int Next (IZingSchedulerState ZSchedulerState);

        /// <summary>
        /// this function is called from zing scheduler to know how many times delay should be called
        /// on a particular state to explore all its successors.
        /// </summary>
        /// <param name="ZSchedulerState"></param>
        /// <returns>Max number of delays to explore all successors of the current state </returns>
        int MaxDelay (IZingSchedulerState ZSchedulerState);
    }
}
