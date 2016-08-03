using P.PRuntime;
using System.Collections;
using System.Collections.Generic;
using System;
/*
* Simple P program

event dummy;

machine Main {
start state Init {
entry {
send this, dummy;
}
on dummy goto Fail;
}

state Fail {
entry {
assert false;
}
}

}
*/
namespace SimpleMachine
{
    public class Application : PStateImpl
    {
        List<BaseMachine> MainMachines;

        public override IEnumerable<BaseMachine> AllAliveMachines
        {
            get
            {
                List<BaseMachine> ret = new List<BaseMachine>();
                ret.AddRange(MainMachines);
                return ret;
            }
        }

        public override IEnumerable<BaseMonitor> AllInstalledMonitors
        {
            get
            {
                return new List<BaseMonitor>();
            }
        }

        public Application() :base()
        {
            //initialize all the fields
        }

        public Application(bool initialize) : base(initialize)
        {
            //initialize all the fields
        }

        //pass the right parameters here !!
        public static Event dummy = new Event("dummy", null, 0, false);



    }

    
} 