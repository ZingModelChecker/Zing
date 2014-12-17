using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Zing
{
    [Serializable]
    public abstract class ZingerSchedulerState
    {
        /// <summary>
        /// Check if the delaying scheduler is Sealed
        /// </summary>
        private bool isSealed;
        public bool IsSealed
        {
            get { return isSealed; }
            set { isSealed = value; }
        }

        /// <summary>
        /// List of all the active processes
        /// </summary>
        public List<int> AllActiveProcessIds;

        /// <summary>
        /// Map from the P Statemachine id to Zing Process Id.
        /// </summary>
        protected Dictionary<int, int> PprocessToZingprocess;
        /// <summary>
        /// Stores the Zing Id of the last Zing process created
        /// </summary>
        private int lastZingProcessCreatedId;

        public int numOfTimesCurrStateDelayed;
        /// <summary>
        /// Defualt Constructor
        /// </summary>
        public ZingerSchedulerState()
        {
            PprocessToZingprocess = new Dictionary<int, int>();
            lastZingProcessCreatedId = -1;
            AllActiveProcessIds = new List<int>();
            isSealed = false;
            numOfTimesCurrStateDelayed = 0;
        }

        /// <summary>
        /// Copy Construtor
        /// </summary>
        /// <param name="copyThis"></param>
        public ZingerSchedulerState(ZingerSchedulerState copyThis)
        {
            this.PprocessToZingprocess = new Dictionary<int,int>();
            AllActiveProcessIds = new List<int>();
            //map
            foreach(var item in copyThis.PprocessToZingprocess)
            {
                this.PprocessToZingprocess.Add(item.Key, item.Value);
                
            }
            lastZingProcessCreatedId = copyThis.lastZingProcessCreatedId;
            isSealed = copyThis.isSealed;
            foreach(var item in copyThis.AllActiveProcessIds)
            {
                this.AllActiveProcessIds.Add(item);
            }
            numOfTimesCurrStateDelayed = 0;
        }
        /// <summary>
        /// This function is called by Zinger Explorer whenever a Zing Processes is created.
        /// </summary>
        /// <param name="processId"></param>
        public void Start(int processId)
        {
            lastZingProcessCreatedId = processId;
            AllActiveProcessIds.Add(processId);
        }

        /// <summary>
        /// This function returns the Zing process Id corresponding to the P process.
        /// </summary>
        /// <param name="P_ProcessId"></param>
        /// <returns></returns>
        public int GetZingProcessId(int P_ProcessId)
        {
            return PprocessToZingprocess[P_ProcessId];
        }

        
        /// <summary>
        /// This function Maps P process to Zing Process
        /// </summary>
        /// <param name="P_ProcessId"></param>
        public void Map(int P_ProcessId)
        {
            PprocessToZingprocess.Add(P_ProcessId, lastZingProcessCreatedId);
        }
        /// <summary>
        /// This function is called when a Zing process has finished execution.
        /// </summary>
        /// <param name="processId"></param>
        public void Finish(int processId)
        {
            AllActiveProcessIds.Remove(processId);
        }

        /// <summary>
        /// Print the current scheduler state
        /// </summary>
        public abstract string ToString();

        /// <summary>
        /// Clone current scheduler state for new zing state.
        /// </summary>
        /// <returns></returns>
        public abstract ZingerSchedulerState Clone (bool isCloneForFrontier);

    }
    public abstract class ZingerDelayingScheduler
    {
        /// <summary>
        /// This functions returns whether the scheduler is sealed or not.
        /// </summary>
        /// <param name="ZSchedulerState"></param>
        /// <returns></returns>
        public bool IsSealed(ZingerSchedulerState ZSchedulerState)
        {
            return ZSchedulerState.IsSealed;
        }
        /// <summary>
        /// This function is called by Zinger whenever a new process is created.
        /// </summary>
        /// <param name="processId"> process Id of the newly created process</param>
        public abstract void Start(ZingerSchedulerState ZSchedulerState, int processId);

        /// <summary>
        /// This function is called by Zinger whenever a process has finished execution.
        /// </summary>
        /// <param name="processId"> process Id of the completed process</param>
        public abstract void Finish(ZingerSchedulerState ZSchedulerState, int processId);

        /// <summary>
        /// This function is invoked from within the zinger explorer
        /// </summary>
        /// <param name="ZSchedulerState"></param>
        /// <param name="Params"></param>
        public abstract void ZingerOperation(ZingerSchedulerState ZSchedulerState, params object[] Params);

        /// <summary>
        /// Unhandled operation or a special operation.
        /// </summary>
        /// <param name="ZSchedulerState"></param>
        /// <param name="Params"></param>
        public virtual void OtherOperations(ZingerSchedulerState ZSchedulerState, params object[] Params)
        {
            throw new Exception("This operation is not supported by the delaying scheduler");
        }

        /// <summary>
        /// This function is called in response to invoke-scheduler function in the zing model.
        /// </summary>
        /// <param name="Params"></param>
        public void Invoke (ZingerSchedulerState ZSchedulerState, params object[] Params)
        {
            var param1_operation = (string)Params[0];
            if(param1_operation == "map")
            {
                var Param2_PprocessId = (int)Params[1];
                ZSchedulerState.Map(Param2_PprocessId);
            }
            else if(param1_operation == "seal")
            {
                ZSchedulerState.IsSealed = true;
            }
            else if(param1_operation == "unseal")
            {
                ZSchedulerState.IsSealed = false;
            }
            else if(param1_operation == "enabled")
            {
                var param2_target = (int)Params[1];
                var param3_source = (int)Params[2];
                OnEnabled(ZSchedulerState, param2_target, param3_source);
            }
            else if(param1_operation == "blocked")
            {
                var param2_source = (int)Params[1];
                OnBlocked(ZSchedulerState, param2_source);
            }
            else if(param1_operation == "zingerop")
            {
                ZingerOperation(ZSchedulerState, Params);
            }
            else
            {
                OtherOperations(ZSchedulerState, Params);
            }
        }

        /// <summary>
        /// This function is called when a P process is enabled.
        /// </summary>
        /// <param name="ZSchedulerState"></param>
        /// <param name="targetSM"></param>
        /// <param name="sourceSM"></param>
        public abstract void OnEnabled(ZingerSchedulerState ZSchedulerState, int targetSM, int sourceSM);

        /// <summary>
        /// This function is called when a P process is blocked on a dequeue
        /// </summary>
        /// <param name="ZSchedulerState"></param>
        /// <param name="sourceSM"></param>
        public abstract void OnBlocked(ZingerSchedulerState ZSchedulerState, int sourceSM);
        /// <summary>
        /// This function is called by Zinger to delay the DBScheduler.
        /// </summary>
        public abstract void Delay (ZingerSchedulerState ZSchedulerState);

        /// <summary>
        /// This function is called by Zinger to obtain next process to be scheduled.
        /// </summary>
        /// <returns>process Id of next process to be scheduled</returns>
        public abstract int Next (ZingerSchedulerState ZSchedulerState);

        /// <summary>
        /// this function is called from zing scheduler to know how many times delay should be called
        /// on a particular state to explore all its successors.
        /// </summary>
        /// <param name="ZSchedulerState"></param>
        /// <returns>Max number of delays to explore all successors of the current state </returns>
        public abstract bool MaxDelayReached (ZingerSchedulerState ZSchedulerState);
    }
}
