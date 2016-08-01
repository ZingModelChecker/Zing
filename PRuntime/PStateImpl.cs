using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace P.PRuntime
{
    public abstract class PStateImpl
    {
        public abstract IEnumerable<BaseMachine> AllMachines
        {
            get;
        }

        public abstract IEnumerable<BaseMonitor> AllMonitors
        {
            get;
        }

        public bool Deadlock
        {
            get
            {
                bool enabled = false;
                foreach (var x in AllMachines)
                {
                    if (enabled) break;
                    enabled = enabled || x.enabled;
                }
                bool hot = false;
                foreach (var x in AllMonitors)
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

        public void SetPendingChoices(PrtStateMachine process, object[] choices)
        {
            throw new NotImplementedException();
        }

        public object GetSelectedChoiceValue(PrtStateMachine process)
        {
            throw new NotImplementedException();
        }
    }

    
}
