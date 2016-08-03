using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace P.PRuntime
{
    public abstract class PStateImpl
    {
        #region Constructors
        /// <summary>
        /// This function is used for cloning the stateimpl
        /// </summary>
        protected PStateImpl()
        { }

        /// <summary>
        /// This function is called when the stateimp is loaded first time.
        /// </summary>
        protected PStateImpl(bool initialState)
        {
            //can only call by passing true
            Debug.Assert(initialState);

            statemachines = new Dictionary<int, PrtStateMachine>();
            nextStateMachineId = 0;

            //create the main machine
            foreach (Type nestedClassType in this.GetType().GetNestedTypes(BindingFlags.NonPublic))
            {
                if (nestedClassType.Name == "Main")
                {
                    
                }
            }
        }
        #endregion

        /// <summary>
        /// Map from the statemachine id to the instance of the statemachine.
        /// </summary>
        private Dictionary<int, PrtStateMachine> statemachines;

        /// <summary>
        /// Represents the next statemachine id.  
        /// </summary>
        private int nextStateMachineId;

        public abstract IEnumerable<BaseMachine> AllAliveMachines
        {
            get;
        }

        public abstract IEnumerable<BaseMonitor> AllInstalledMonitors
        {
            get;
        }

        public bool Deadlock
        {
            get
            {
                bool enabled = false;
                foreach (var x in AllAliveMachines)
                {
                    if (enabled) break;
                    enabled = enabled || x.enabled;
                }
                bool hot = false;
                foreach (var x in AllInstalledMonitors)
                {
                    if (hot) break;
                    hot = hot || x.hot;
                }
                return (!enabled && hot);
            }
        }

        public void Trace(string message, params object[] arguments)
        {
            Console.WriteLine(String.Format(message, arguments));
        }

        public Exception Exception
        {
            get { return exception; }
            set { exception = value; }
        }

        private Exception exception;

        private bool isCall;

        //IExplorable
        public bool IsCall
        {
            get { return isCall; }
            set { isCall = value; }
        }

        private bool isReturn;

        //IExplorable
        public bool IsReturn
        {
            get { return isReturn; }
            set { isReturn = value; }
        }

        public void SetPendingChoicesAsBoolean(PrtStateMachine process)
        {
            throw new NotImplementedException();
        }

        public object GetSelectedChoiceValue(PrtStateMachine process)
        {
            throw new NotImplementedException();
        }
    }

    
}
