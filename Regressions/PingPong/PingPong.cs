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

        #region Constructors
        public Application() :base()
        {
            //initialize all the fields
        }

        public Application(bool initialize) : base(initialize)
        {
            //initialize all the fields
        }
        #endregion

        //pass the right parameters here !!
        public static Event dummy = new Event("dummy", null, 0, false);

        public class Main : Machine<Main>
        {
            public override State<Main> StartState
            {
                get
                {
                    return Init_State;
                }
            }

            public override string Name
            {
                get
                {
                    return "Main";
                }
            }
            //constructor
            public Main(int instance) : base (instance, 10)
            {
                fields = new List<PrtValue>();
            }
            //getters and setters


            #region Functions
            public class Anon_0 : Fun<Main>
            {

            }

            public class Anon_1 : Fun<Main>
            {

            }

            public static Anon_0 Anon_0_Fun;

            public static Anon_1 Anon_1_Fun;
            #endregion

            #region States
            public class Init : State<Main>
            {
                public Init(string name, Fun<Main> entryFun, Fun<Main> exitFun, bool hasNullTransition, StateTemperature temperature) 
                    :base (name, entryFun, exitFun, hasNullTransition, temperature)
                {

                }
            }

            public class Fail : State<Main>
            {
                public Fail(string name, Fun<Main> entryFun, Fun<Main> exitFun, bool hasNullTransition, StateTemperature temperature) 
                    :base (name, entryFun, exitFun, hasNullTransition, temperature)
                {

                }
            }

            public static Init Init_State;
            public static Fail Fail_State;
            #endregion

            static Main()
            {
                //initialize functions
                Anon_0_Fun = new Anon_0();
                Anon_1_Fun = new Anon_1();

                //initialize states 
                Init_State = new Init("Init", Anon_0_Fun, null, false, StateTemperature.Warm);
                Fail_State = new Fail("Fail", Anon_1_Fun, null, false, StateTemperature.Warm);

                //create transition and add them to the state
                Transition<Main> transition_1 = new Transition<Main>(null, Fail_State);

                //add transition
                Init_State.transitions.Add(dummy, transition_1);

            }

        }

    }

    
} 