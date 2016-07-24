using Z = Microsoft.Zing;
using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;

namespace Microsoft.Prt
{
    public abstract class Machine
    {
        State initState;

        public abstract void CalculateDeferredAndActionSet(State state);

        public Machine(State initState)
        {
            this.initState = initState;
        }

        public MachineHandle myHandle;

        internal sealed class Start : Z.ZingMethod
        {
            private static readonly short typeId = 3;

            public override object DoCheckInOthers()
            {
                throw new NotImplementedException();
            }
            public override void DoRevertOthers()
            {
                throw new NotImplementedException();
            }
            public override void DoRollbackOthers(object[] uleList)
            {
                throw new NotImplementedException();
            }

            private Z.StateImpl application;
            private Machine machine;

            // locals
            private Blocks nextBlock;

            public Start(Z.StateImpl app, Machine machine)
            {
                application = app;
                this.machine = machine;
                nextBlock = Blocks.Enter;
            }

            public override Z.StateImpl StateImpl
            {
                get
                {
                    return application;
                }
                set
                {
                    application = value;
                }
            }

            public enum Blocks : ushort
            {
                None = 0,
                Enter = 1,
                B0 = 2,
            };

            public override ushort NextBlock
            {
                get
                {
                    return ((ushort)nextBlock);
                }
                set
                {
                    nextBlock = ((Blocks)value);
                }
            }

            public override string ProgramCounter
            {
                get
                {
                    return nextBlock.ToString();
                }
            }

            public override void Dispatch(Z.Process p)
            {
                switch (nextBlock)
                {
                    case Blocks.Enter:
                        {
                            Enter(p);
                            break;
                        }
                    default:
                        {
                            throw new ApplicationException();
                        }
                    case Blocks.B0:
                        {
                            B0(p);
                            break;
                        }
                }
            }

            public override Z.ZingMethod Clone(Z.StateImpl application, Z.Process myProcess, bool shallowCopy)
            {
                Start clone = new Start(application, machine);
                clone.nextBlock = this.nextBlock;
                if (this.Caller != null)
                {
                    if (shallowCopy)
                    {
                        clone.Caller = null;
                    }
                    else
                    {
                        clone.Caller = this.Caller.Clone(application, myProcess, false);
                    }
                }
                else
                {
                    if (myProcess != null)
                    {
                        myProcess.EntryPoint = this;
                    }
                }
                return clone;
            }

            public override void WriteString(Z.StateImpl state, BinaryWriter bw)
            {
                bw.Write(typeId);
                bw.Write(((ushort)nextBlock));
            }

            public void Enter(Z.Process p)
            {
                Machine.Run callee = new Machine.Run(application, machine, machine.initState);
                p.Call(callee);
                StateImpl.IsCall = true;

                nextBlock = Blocks.B0;
            }
            public void B0(Z.Process p)
            {
                p.LastFunctionCompleted = null;

                var handle = machine.myHandle;
                var currentEvent = handle.currentEvent;
                var haltedSet = MachineHandle.halted;
                var enabledSet = MachineHandle.enabled;

                //Checking if currentEvent is halt:
                if (currentEvent == Event.HaltEvent)
                {
                    handle.stack = null;
                    handle.buffer = null;
                    handle.currentArg = null;
                    haltedSet.Add(handle);
                    enabledSet.Remove(handle);

                    p.Return(null, null);
                    StateImpl.IsReturn = true;
                }
                else
                {
                    application.Trace(
                        null, null, 
                        @"<StateLog> Unhandled event exception by machine Real1-{0}", 
                        handle.instance);
                    this.StateImpl.Exception = new Z.ZingAssertionFailureException(@"false", @"Unhandled event exception by machine <mach name>");
                    p.Return(null, null);
                    StateImpl.IsReturn = true;
                }
            }
        }

        internal sealed class Run : Z.ZingMethod
        {
            private static readonly short typeId = 4;

            private Z.StateImpl application;
            private Machine machine;

            // inputs
            private State state;
            
            // locals
            private Blocks nextBlock;
            private bool doPop;

            public Run(Z.StateImpl app, Machine machine, State state)
            {
                application = app;
                nextBlock = Blocks.Enter;
                this.machine = machine;
                this.state = state;
            }

            public override Z.StateImpl StateImpl
            {
                get
                {
                    return application;
                }
                set
                {
                    application = value;
                }
            }

            public enum Blocks : ushort
            {
                None = 0,
                Enter = 1,
                B0 = 2,
                B1 = 3,
                B2 = 4,
                B3 = 5,
                B4 = 6,
                B5 = 7,
            };
            
            public override void Dispatch(Z.Process p)
            {
                switch (nextBlock)
                {
                    case Blocks.Enter:
                        {
                            Enter(p);
                            break;
                        }
                    default:
                        {
                            throw new ApplicationException();
                        }
                    case Blocks.B0:
                        {
                            B0(p);
                            break;
                        }
                    case Blocks.B1:
                        {
                            B1(p);
                            break;
                        }
                    case Blocks.B2:
                        {
                            B2(p);
                            break;
                        }
                    case Blocks.B3:
                        {
                            B3(p);
                            break;
                        }
                    case Blocks.B4:
                        {
                            B4(p);
                            break;
                        }
                    case Blocks.B5:
                        {
                            B5(p);
                            break;
                        }
                }
            }

            public override int CompareTo(object obj)
            {
                return 0;
            }

            public override ushort NextBlock
            {
                get
                {
                    return ((ushort)nextBlock);
                }
                set
                {
                    nextBlock = ((Blocks)value);
                }
            }

            public override Z.ZingMethod Clone(Z.StateImpl application, Z.Process myProcess, bool shallowCopy)
            {
                Run clone = new Run(application, this.machine, this.state);
                clone.nextBlock = this.nextBlock;
                clone.doPop = this.doPop;
                if (this.Caller != null)
                {
                    if (shallowCopy)
                    {
                        clone.Caller = null;
                    }
                    else
                    {
                        clone.Caller = this.Caller.Clone(application, myProcess, false);
                    }
                }
                else
                {
                    if (myProcess != null)
                    {
                        myProcess.EntryPoint = this;
                    }
                }
                return clone;
            }

            public override void WriteString(Z.StateImpl state, BinaryWriter bw)
            {
                bw.Write(typeId);
                bw.Write((ushort)nextBlock);
            }

            public void B5(Z.Process p)
            {
                var handle = machine.myHandle;
                handle.Pop();
                p.Return(null, null);
                StateImpl.IsReturn = true;
            }

            public void B4(Z.Process p)
            {
                doPop = ((Machine.RunHelper)p.LastFunctionCompleted).ReturnValue;
                p.LastFunctionCompleted = null;

                //B1 is header of the "while" loop:
                nextBlock = Blocks.B1;
            }

            public void B3(Z.Process p)
            {
                //Return from DequeueEvent:
                p.LastFunctionCompleted = null;

                Machine.RunHelper callee = new Machine.RunHelper(application, machine, false);
                p.Call(callee);
                StateImpl.IsCall = true;

                nextBlock = Blocks.B4;
            }

            public void B2(Z.Process p)
            {
                var handle = machine.myHandle;
                var stateStack = handle.stack;
                var hasNullTransitionOrAction = stateStack.HasNullTransitionOrAction();
                // Shaz: Fix this 
                handle.DequeueEvent(hasNullTransitionOrAction);
                nextBlock = Blocks.B3;
            }

            public void B1(Z.Process p)
            {
                if (!doPop)
                {
                    nextBlock = Blocks.B2;
                }
                else
                {
                    nextBlock = Blocks.B5;
                }
            }

            public void B0(Z.Process p)
            {
                //Return from RunHelper:
                doPop = ((Machine.RunHelper)p.LastFunctionCompleted).ReturnValue;
                p.LastFunctionCompleted = null;
                nextBlock = Blocks.B1;
            }

            public void Enter(Z.Process p)
            {
                var handle = machine.myHandle;
                var stateStack = handle.stack;
                handle.Push();
                stateStack.state = state;

                Machine.RunHelper callee = new Machine.RunHelper(application, machine, true);
                p.Call(callee);
                StateImpl.IsCall = true;

                nextBlock = Blocks.B0;
            }
        }

        internal sealed class RunHelper : Z.ZingMethod
        {
            private static readonly short typeId = 8;

            private Z.StateImpl application;
            private Machine machine;

            // inputs
            private bool start;

            // locals
            private Blocks nextBlock;
            private State state;
            private ActionOrFun actionFun;
            private Transition transition;

            // output
            private bool _ReturnValue;
            public bool ReturnValue
            {
                get
                {
                    return _ReturnValue;
                }
            }

            public RunHelper(Z.StateImpl app, Machine machine, bool start)
            {
                application = app;
                nextBlock = Blocks.Enter;
                this.machine = machine;
                this.start = start;
            }

            public override Z.StateImpl StateImpl
            {
                get
                {
                    return application;
                }
                set
                {
                    application = value;
                }
            }

            public enum Blocks : ushort
            {
                None = 0,
                Enter = 1,
                B0 = 2,
                B1 = 3,
                B2 = 4,
                B3 = 5,
                B4 = 6,
                B5 = 7,
                B6 = 8,
                B7 = 9,
                B8 = 10,
            }

            public override ushort NextBlock
            {
                get
                {
                    return ((ushort)nextBlock);
                }
                set
                {
                    nextBlock = ((Blocks)value);
                }
            }

            public override string ProgramCounter
            {
                get
                {
                    return nextBlock.ToString();
                }
            }

            public override void Dispatch(Z.Process p)
            {
                switch (nextBlock)
                {
                    case Blocks.Enter:
                        {
                            Enter(p);
                            break;
                        }
                    default:
                        {
                            throw new ApplicationException();
                        }
                    case Blocks.B0:
                        {
                            B0(p);
                            break;
                        }
                    case Blocks.B1:
                        {
                            B1(p);
                            break;
                        }
                    case Blocks.B2:
                        {
                            B2(p);
                            break;
                        }
                    case Blocks.B3:
                        {
                            B3(p);
                            break;
                        }
                    case Blocks.B4:
                        {
                            B4(p);
                            break;
                        }
                    case Blocks.B5:
                        {
                            B5(p);
                            break;
                        }
                    case Blocks.B6:
                        {
                            B6(p);
                            break;
                        }
                    case Blocks.B7:
                        {
                            B7(p);
                            break;
                        }
                    case Blocks.B8:
                        {
                            B8(p);
                            break;
                        }
                }
            }
           
            public override int CompareTo(object obj)
            {
                return 0;
            }

            public override Z.ZingMethod Clone(Z.StateImpl application, Z.Process myProcess, bool shallowCopy)
            {
                RunHelper clone = new RunHelper(application, this.machine, this.start);
                clone.nextBlock = this.nextBlock;
                clone.state = this.state;
                clone.transition = this.transition;
                clone.actionFun = this.actionFun;
                if (this.Caller != null)
                {
                    if (shallowCopy)
                    {
                        clone.Caller = null;
                    }
                    else
                    {
                        clone.Caller = this.Caller.Clone(application, myProcess, false);
                    }
                }
                else
                {
                    if (myProcess != null)
                    {
                        myProcess.EntryPoint = this;
                    }
                }
                return clone;
            }

            public override void WriteString(Z.StateImpl state, BinaryWriter bw)
            {
                bw.Write(typeId);
                bw.Write(((ushort)nextBlock));
            }

            public void Enter(Z.Process p)
            {
                var handle = machine.myHandle;
                var stateStack = handle.stack;
                this.state = stateStack.state;

                if (start)
                {
                    nextBlock = Blocks.B0;
                }
                else
                {
                    nextBlock = Blocks.B1;
                }
            }

            public void B0(Z.Process p)
            {
                var handle = machine.myHandle;
                var stateStack = handle.stack;
                state = stateStack.state;

                //enter:
                machine.CalculateDeferredAndActionSet(state);

                actionFun = state.entryFun;
                nextBlock = Blocks.B2;
            }

            public void B2(Z.Process p)
            {
                var handle = machine.myHandle;
                var stateStack = handle.stack;

                Machine.ReentrancyHelper callee = new Machine.ReentrancyHelper(application, machine, actionFun);
                p.Call(callee);
                StateImpl.IsCall = true;
                nextBlock = Blocks.B3;
            }

            public void B3(Z.Process p)
            {
                p.LastFunctionCompleted = null;

                var handle = machine.myHandle;
                var reason = handle.cont.reason;
                if (reason == ContinuationReason.Raise)
                {
                    //goto handle;
                    nextBlock = Blocks.B1;
                }
                else
                {
                    handle.currentEvent = null;
                    handle.currentArg = PrtValue.PrtMkDefaultValue(PrtType.PrtMkPrimitiveType(PrtTypeKind.PRT_KIND_NULL));
                    if (reason != ContinuationReason.Pop)
                    {
                        _ReturnValue = false;
                        p.Return(null, null);
                        StateImpl.IsReturn = true;
                    }
                    else
                    {
                        Machine.ReentrancyHelper callee = new Machine.ReentrancyHelper(application, machine, state.exitFun);
                        p.Call(callee);
                        StateImpl.IsCall = true;

                        nextBlock = Blocks.B4;
                    }
                }
            }

            public void B4(Z.Process p)
            {
                p.LastFunctionCompleted = null;

                _ReturnValue = true;
                p.Return(null, null);
                StateImpl.IsReturn = true;
            }

            public void B1(Z.Process p)
            {
                var handle = machine.myHandle;
                var stateStack = handle.stack;
                var state = stateStack.state;
                var actionSet = stateStack.actionSet;

                //handle:
                if (actionSet.Contains(handle.currentEvent))
                {
                    actionFun = stateStack.Find(handle.currentEvent);
                    //goto execute;
                    nextBlock = Blocks.B2;
                }
                else
                {
                    transition = state.FindPushTransition(handle.currentEvent);
                    if (transition != null)
                    {
                        Machine.Run callee = new Machine.Run(application, machine, transition.to);
                        p.Call(callee);
                        StateImpl.IsCall = true;

                        nextBlock = Blocks.B5;
                    }
                    else
                    {
                        nextBlock = Blocks.B6;
                    }
                }
            }

            public void B5(Z.Process p)
            {
                p.LastFunctionCompleted = null;

                var handle = machine.myHandle;

                if (handle.currentEvent == null)
                {
                    _ReturnValue = false;
                    p.Return(null, null);
                    StateImpl.IsReturn = true;
                }
                else
                {
                    //goto handle;
                    nextBlock = Blocks.B1;
                }
            }

            public void B6(Z.Process p)
            {
                Machine.ReentrancyHelper callee = new Machine.ReentrancyHelper(application, machine, state.exitFun);
                p.Call(callee);
                StateImpl.IsCall = true;

                nextBlock = Blocks.B7;
            }

            public void B7(Z.Process p)
            {
                p.LastFunctionCompleted = null;

                var handle = machine.myHandle;
                transition = state.FindTransition(handle.currentEvent);
                if (transition == null)
                {
                    _ReturnValue = true;
                    p.Return(null, null);
                    StateImpl.IsReturn = true;
                }
                else
                {
                    Machine.ReentrancyHelper callee = new Machine.ReentrancyHelper(application, machine, transition.fun);
                    p.Call(callee);
                    StateImpl.IsCall = true;
                    nextBlock = Blocks.B8;
                }
            }

            public void B8(Z.Process p)
            {
                p.LastFunctionCompleted = null;
                var handle = machine.myHandle;
                var stateStack = handle.stack;
                stateStack.state = transition.to;
                state = stateStack.state;

                //goto enter;
                nextBlock = Blocks.B0;
            }
        }

        internal sealed class ReentrancyHelper : Z.ZingMethod
        {
            public ReentrancyHelper(Z.StateImpl app, Machine machine, ActionOrFun actionFun)
            {

            }
        }

        internal sealed class ProcessContinuation : Z.ZingMethod
        {
            private Z.StateImpl application;
            private Machine machine;

            // locals
            private Blocks nextBlock;

            // output
            private bool _ReturnValue;

            bool ReturnValue
            {
                get { return _ReturnValue; }
            }

            public ProcessContinuation(Z.StateImpl app, Machine machine)
            {
                application = app;
                this.machine = machine;
                nextBlock = Blocks.Enter;
            }

            public override Z.StateImpl StateImpl
            {
                get
                {
                    return application;
                }
                set
                {
                    application = value;
                }
            }

            public enum Blocks : ushort
            {
                None = 0,
                Enter = 1,
                B0 = 2,
                B1 = 3,
                B2 = 4,
            };

            public override ushort NextBlock
            {
                get
                {
                    return ((ushort)nextBlock);
                }
                set
                {
                    nextBlock = ((Blocks)value);
                }
            }

            public override string ProgramCounter
            {
                get
                {
                    return nextBlock.ToString();
                }
            }            

            public override void Dispatch(Z.Process p)
            {
                switch (nextBlock)
                {
                    case Blocks.Enter:
                        {
                            Enter(p);
                            break;
                        }
                    default:
                        {
                            throw new ApplicationException();
                        }
                    case Blocks.B0:
                        {
                            B0(p);
                            break;
                        }
                    case Blocks.B1:
                        {
                            B1(p);
                            break;
                        }
                    case Blocks.B2:
                        {
                            B2(p);
                            break;
                        }
                }
            }
            
            public override int CompareTo(object obj)
            {
                return 0;
            }

            private static readonly short typeId = 9;

            public override Z.ZingMethod Clone(Z.StateImpl application, Z.Process myProcess, bool shallowCopy)
            {
                ProcessContinuation clone = new ProcessContinuation(application, this.machine);
                clone.nextBlock = this.nextBlock;
                if (this.Caller != null)
                {
                    if (shallowCopy)
                    {
                        clone.Caller = null;
                    }
                    else
                    {
                        clone.Caller = this.Caller.Clone(application, myProcess, false);
                    }
                }
                else
                {
                    if (myProcess != null)
                    {
                        myProcess.EntryPoint = this;
                    }
                }
                return clone;
            }

            public override void WriteString(Z.StateImpl state, BinaryWriter bw)
            {
                bw.Write(typeId);
                bw.Write(((ushort)nextBlock));
            }

            public void Enter(Z.Process p)
            {
                var handle = machine.myHandle;
                var cont = handle.cont;
                var reason = cont.reason;
                if (reason == ContinuationReason.Return)
                {
                    _ReturnValue = true;
                    p.Return(null, null);
                    StateImpl.IsReturn = true;
                }
                if (reason == ContinuationReason.Pop)
                {
                    _ReturnValue = true;
                    p.Return(null, null);
                    StateImpl.IsReturn = true;
                }
                if (reason == ContinuationReason.Raise)
                {
                    _ReturnValue = true;
                    p.Return(null, null);
                    StateImpl.IsReturn = true;
                }
                if (reason == ContinuationReason.Receive)
                {
                    // Shaz: Fix me
                    var status = handle.DequeueEvent(false);

                    nextBlock = Blocks.B0;
                }
                if (reason == ContinuationReason.Nondet)
                {
                    //No splitting into a new Block after nondet, since it is a local thing
                    //myHandle.cont.nondet = choose(bool);
                    application.SetPendingChoices(p, new object[] { false, true });
                    cont.nondet = ((Boolean)application.GetSelectedChoiceValue(p));
                    _ReturnValue = false;
                    p.Return(null, null);
                    StateImpl.IsReturn = true;
                }
                if (reason == ContinuationReason.NewMachine)
                {
                    //yield;
                    p.MiddleOfTransition = false;
                    nextBlock = Blocks.B1;
                }
                if (reason == ContinuationReason.Send)
                {
                    //yield;
                    p.MiddleOfTransition = false;
                    nextBlock = Blocks.B2;
                }
            }

            public void B0(Z.Process p)
            {
                //ContinuationReason.Receive after Dequeue call:
                p.LastFunctionCompleted = null;
                _ReturnValue = false;
                p.Return(null, null);
                StateImpl.IsReturn = true;
            }

            public void B1(Z.Process p)
            {
                //ContinuationReason.NewMachine after yield:
                _ReturnValue = false;
                p.Return(null, null);
                StateImpl.IsReturn = true;
            }

            public void B2(Z.Process p)
            {
                //ContinuationReason.Send after yield:
                _ReturnValue = false;
                p.Return(null, null);
                StateImpl.IsReturn = true;
            }
        }

        public void ignore(Z.StateImpl application, Continuation entryCtxt)
        {
            StackFrame retTo;
            retTo = entryCtxt.PopReturnTo();
            if (retTo.pc != 0)
            {
                application.Exception = new Z.ZingAssertionFailureException(@"false", @"Internal error in ignore");
            }
            entryCtxt.Return();
        }
    }

    internal class MachineId
    {
        static int nextMachineId = 0;

        public static int GetNextId()
        {
            int ret = nextMachineId;
            nextMachineId = nextMachineId + 1;
            return ret;
        }
    };

    public class Event
    {
        public static Event NullEvent;
        public static Event HaltEvent;
        public string name;
        public PrtType payload;
        public int maxInstances;
        public bool doAssume;

        static Event Construct(string name, PrtType payload, int mInstances, bool doAssume)
        {
            Event ev = new Event();
            ev.name = name;
            ev.payload = payload;
            ev.maxInstances = mInstances;
            ev.doAssume = doAssume;
            return ev;
        }
    };

    public delegate void ActionOrFun(Z.StateImpl application, Continuation ctxt);

    public class Transition
    {
        public Event evt;
        public ActionOrFun fun; // isPush <==> fun == null
        public State to;

        static Transition Construct(Event evt, ActionOrFun fun, State to)
        {
            Transition transition = new Transition();
            transition.evt = evt;
            transition.fun = fun;
            transition.to = to;
            return transition;
        }
    };

    public class State
    {
        public State name;
        public ActionOrFun entryFun;
        public ActionOrFun exitFun;
        public List<Transition> transitions;
        public bool hasNullTransition;
        public StateTemperature temperature;

        public static State Construct(State name, ActionOrFun entryFun, ActionOrFun exitFun, int numTransitions, bool hasNullTransition, StateTemperature temperature)
        {
            State state = new State();
            state.name = name;
            state.entryFun = entryFun;
            state.exitFun = exitFun;
            state.transitions = new List<Transition>(numTransitions);
            state.hasNullTransition = hasNullTransition;
            state.temperature = temperature;
            return state;
        }

        public Transition FindPushTransition(Event evt)
        {
            foreach (Transition transition in transitions)
            {
                if (transition.evt == evt && transition.fun == null)
                {
                    return transition;
                }
            }
            return null;
        }

        public Transition FindTransition(Event evt)
        {
            foreach (Transition transition in transitions)
            {
                if (transition.evt == evt)
                {
                    return transition;
                }
            }
            return null;
        }
    };

    public class MachineHandle
    {
        public static HashSet<MachineHandle> halted = new HashSet<MachineHandle>();
        public static HashSet<MachineHandle> enabled = new HashSet<MachineHandle>();
        public static HashSet<MachineHandle> hot = new HashSet<MachineHandle>();
        public StateStack stack;
        public Continuation cont;
        public EventBuffer buffer;
        public int maxBufferSize;
        public Machine machineName;
        public int machineId;
        public int instance;
        public Event currentEvent;
        public PrtValue currentArg;
        public HashSet<Event> receiveSet;

        public MachineHandle Construct(Machine machine, int inst, int maxBufferSize)
        {
            MachineHandle handle = new MachineHandle();
            PrtType prtType;

            handle.stack = null;
            handle.cont = Continuation.Construct();
            handle.buffer = EventBuffer.Construct();
            handle.maxBufferSize = maxBufferSize;
            handle.machineName = machine;
            handle.machineId = MachineId.GetNextId();
            handle.instance = inst;
            handle.currentEvent = null;
            prtType = PrtType.PrtMkPrimitiveType(PrtTypeKind.PRT_KIND_NULL);
            handle.currentArg = PrtValue.PrtMkDefaultValue(prtType);
            handle.receiveSet = new HashSet<Event>();
            return handle;
        }

        public void Push()
        {
            StateStack s;

            s = new StateStack();
            s.next = this.stack;
            this.stack = s;
        }

        public void Pop()
        {
            this.stack = this.stack.next;
        }

        public void EnqueueEvent(Z.StateImpl application, Event e, PrtValue arg, MachineHandle source)
        {
            bool b;
            bool isEnabled;
            PrtType prtType;

            Debug.Assert(e != null, "Enqueued event must be non-null");
            //assertion to check if argument passed inhabits the payload type.
            prtType = e.payload;

            if (prtType.typeKind != PrtTypeKind.PRT_KIND_NULL)
            {
                b = PrtValue.PrtInhabitsType(arg, prtType);
                Debug.Assert(b, "Type of payload does not match the expected type with event");
            }
            else
            {
                Debug.Assert(arg.type.typeKind == PrtTypeKind.PRT_KIND_NULL, "Type of payload does not match the expected type with event");
            }
            if (halted.Contains(this))
            {
                //TODO: will this work?
                application.Trace(
                    null,
                    null,
                    @"<EnqueueLog> {0}-{1} Machine has been halted and Event {2} is dropped",
                    this.machineName, this.instance, e.name);
            }
            else
            {
                if (arg != null)
                {
                    application.Trace(
                        null,
                        null,
                        @"<EnqueueLog> Enqueued Event < {0} > in Machine {1}-{2} by {3}-{4}",
                        e.name, this.machineName, this.instance, source.machineName, source.instance);
                }
                else
                {
                    application.Trace(
                        null,
                        null,
                        @"<EnqueueLog> Enqueued Event < {0}, ",
                        e.name);
                }

                this.buffer.EnqueueEvent(e, arg);
                if (this.maxBufferSize != -1 && this.buffer.eventBufferSize > this.maxBufferSize)
                {
                    application.Trace(
                        null, null, 
                        @"<EXCEPTION> Event Buffer Size Exceeded {0} in Machine {1}-{2}",
                        this.maxBufferSize, this.machineName, this.instance);
                    Debug.Assert(false);
                }
                if (enabled.Contains(this))
                {
                    // do nothing because cannot change the status
                }
                else
                {
                    isEnabled = this.buffer.IsEnabled(this);
                    if (isEnabled)
                    {
                        enabled.Add(this);
                    }
                }
                if (enabled.Contains(this))
                {
                    // invokescheduler("enabled", machineId, source.machineId);
                }
            }
        }

        public enum DequeueEventReturnStatus { SUCCESS, NULL, BLOCKED };

        public DequeueEventReturnStatus DequeueEvent(bool hasNullTransition)
        {
            currentEvent = null;
            currentArg = null;
            buffer.DequeueEvent(this);
            if (currentEvent != null)
            {
                Debug.Assert(currentArg != null, "Internal error");
                Debug.Assert(MachineHandle.enabled.Contains(this), "Internal error");
                //trace("<DequeueLog> Dequeued Event < {0}, ", currentEvent.name); PRT_VALUE.Print(currentArg); trace(" > at Machine {0}-{1}\n", machineName, instance);
                receiveSet = new HashSet<Event>();
                return DequeueEventReturnStatus.SUCCESS;
            }
            else if (hasNullTransition || receiveSet.Contains(currentEvent))
            {
                Debug.Assert(MachineHandle.enabled.Contains(this), "Internal error");
                //trace("<NullTransLog> Null transition taken by Machine {0}-{1}\n", machineName, instance);
                var nullType = PrtType.PrtMkPrimitiveType(PrtTypeKind.PRT_KIND_NULL);
                currentArg = PrtValue.PrtMkDefaultValue(nullType);
                //FairScheduler.AtYieldStatic(this);
                //FairChoice.AtYieldOrChooseStatic();
                receiveSet = new HashSet<Event>();
                return DequeueEventReturnStatus.NULL;
            }
            else
            {
                //invokescheduler("blocked", machineId);
                //assume(this in SM_HANDLE.enabled);
                MachineHandle.enabled.Remove(this);
                Debug.Assert(MachineHandle.enabled.Count != 0 || MachineHandle.hot.Count == 0, "Deadlock");
                //FairScheduler.AtYieldStatic(this);
                //FairChoice.AtYieldOrChooseStatic();
                return DequeueEventReturnStatus.BLOCKED;
            }
        }

        public class EventNode
        {
            public EventNode next;
            public EventNode prev;
            public Event e;
            public PrtValue arg;
        }

        public class EventBuffer
        {
            public EventNode head;
            public int eventBufferSize;

            public static EventBuffer Construct()
            {
                EventNode node = new EventNode();
                node.next = node;
                node.prev = node;
                node.e = null;
                EventBuffer buffer = new EventBuffer();
                buffer.head = node;
                buffer.eventBufferSize = 0;
                return buffer;
            }
            public int CalculateInstances(Event e)
            {
                //No constructor in the old compiler:
                //EventNode elem = EventNode.Construct();  //instead of "new"
                int currInstances = 0;
                EventNode elem = this.head.next;
                while (elem != this.head)
                {
                    if (elem.e.name == e.name)
                    {
                        currInstances = currInstances + 1;
                    }
                    elem = elem.next;
                }
                Debug.Assert(currInstances <= e.maxInstances, "Internal error");
                return currInstances;
            }

            public void EnqueueEvent(Event e, PrtValue arg)
            {
                EventNode elem;
                int currInstances;

                if (e.maxInstances == -1)
                {
                    //Instead of "Allocate" in old compiler:
                    elem = new EventNode();
                    elem.e = e;
                    elem.arg = arg;
                    elem.prev = this.head.prev;
                    elem.next = this.head;
                    elem.prev.next = elem;
                    elem.next.prev = elem;
                    this.eventBufferSize = this.eventBufferSize + 1;
                }
                else
                {
                    currInstances = this.CalculateInstances(e);
                    if (currInstances == e.maxInstances)
                    {
                        if (e.doAssume)
                        {
                            //assume(false);
                            //TODO(question): is this a correct replacement?
                            Debug.Assert(false);
                        }
                        else
                        {
                            //.zing: trace("<Exception> Attempting to enqueue event {0} more than max instance of {1}\n", e.name, e.maxInstances);
                            //old compiler:
                            //application.Trace(new ZingSourceContext(0, 16127, 16235), null, @"<Exception> Attempting to enqueue event {0} more than max instance of {1}
                            //", (((Z.Application.Event) application.LookupObject(inputs.e))).name, (((Z.Application.Event) application.LookupObject(inputs.e))).maxInstances);
                            //this.StateImpl.Exception = new Z.ZingAssertionFailureException(@"false");
                            //TODO(question): what to replace with in the new compiler?
                            //assert(false);
                            Debug.Assert(false);
                        }
                    }
                    else
                    {
                        //Instead of "Allocate" in old compiler:
                        elem = new EventNode();
                        elem.e = e;
                        elem.arg = arg;
                        elem.prev = this.head.prev;
                        elem.next = this.head;
                        elem.prev.next = elem;
                        elem.next.prev = elem;
                        this.eventBufferSize = this.eventBufferSize + 1;
                    }
                }
            }

            public void DequeueEvent(MachineHandle owner)
            {
                HashSet<Event> deferredSet;
                HashSet<Event> receiveSet;
                EventNode iter;
                bool doDequeue;

                deferredSet = owner.stack.deferredSet;
                receiveSet = owner.receiveSet;

                iter = this.head.next;
                while (iter != this.head)
                {
                    if (receiveSet.Count == 0)
                    {
                        doDequeue = !deferredSet.Contains(iter.e);
                    }
                    else
                    {
                        doDequeue = receiveSet.Contains(iter.e);
                    }
                    if (doDequeue)
                    {
                        iter.next.prev = iter.prev;
                        iter.prev.next = iter.next;
                        owner.currentEvent = iter.e;
                        owner.currentArg = iter.arg;
                        this.eventBufferSize = this.eventBufferSize - 1;
                        return;
                    }
                    iter = iter.next;
                }
            }

            public bool IsEnabled(MachineHandle owner)
            {
                EventNode iter;
                HashSet<Event> deferredSet;
                HashSet<Event> receiveSet;
                bool enabled;


                deferredSet = owner.stack.deferredSet;
                receiveSet = owner.receiveSet;
                iter = this.head.next;
                while (iter != head)
                {
                    if (receiveSet.Count == 0)
                    {
                        enabled = !deferredSet.Contains(iter.e);
                    }
                    else
                    {
                        enabled = receiveSet.Contains(iter.e);
                    }
                    if (enabled)
                    {
                        return true;
                    }
                    iter = iter.next;
                }
                return false;
            }
        }

        public class StateStack
        {
            public State state;
            public HashSet<Event> deferredSet;
            public HashSet<Event> actionSet;
            public List<Event> events;
            public List<ActionOrFun> actions;
            public StateStack next;

            public ActionOrFun Find(Event f)
            {
                for (int i = 0; i < events.Count; i++)
                {
                    if (events[i] == f)
                    {
                        return actions[i];
                    }
                }
                return next.Find(f);
            }

            public void AddStackDeferredSet(HashSet<Event> localDeferredSet)
            {
                if (next == null)
                {
                    return;
                }
                localDeferredSet.UnionWith(next.deferredSet);
            }

            public void AddStackActionSet(HashSet<Event> localActionSet)
            {
                if (next == null)
                {
                    return;
                }
                localActionSet.UnionWith(next.actionSet);
            }

            public bool HasNullTransitionOrAction()
            {
                if (state.hasNullTransition) return true;
                return actionSet.Contains(Event.NullEvent);
            }
        }
    }

    public enum ContinuationReason : int
    {
        Return,
        Nondet,
        Pop,
        Raise,
        Receive,
        Send,
        NewMachine,
    };

    public class StackFrame
    {
        public int pc;
        public List<PrtValue> locals;
        public StackFrame next;
    }

    public class Continuation
    {
        public StackFrame returnTo;
        public ContinuationReason reason;
        public MachineHandle id;
        public PrtValue retVal;

        // The nondet field is different from the fields above because it is used 
        // by ReentrancyHelper to pass the choice to the nondet choice point.
        // Therefore, nondet should not be reinitialized in this class.
        public bool nondet;

        public static Continuation Construct()
        {
            Continuation cont = new Continuation();
            cont.returnTo = null;
            cont.reason = ContinuationReason.Return;
            cont.id = null;
            cont.retVal = null;
            cont.nondet = false;
            return cont;
        }

        public void Reset()
        {
            this.returnTo = null;
            this.reason = ContinuationReason.Return;
            this.id = null;
            this.retVal = null;
            this.nondet = false;
        }

        public StackFrame PopReturnTo()
        {
            StackFrame topOfStack;
            topOfStack = this.returnTo;
            this.returnTo = topOfStack.next;
            topOfStack.next = null;
            return topOfStack;
        }

        public void PushReturnTo(int ret, List<PrtValue> locals)
        {
            StackFrame tmp;
            tmp = new StackFrame();
            tmp.pc = ret;
            tmp.locals = locals;
            tmp.next = this.returnTo;
            this.returnTo = tmp;
        }

        public void Return()
        {
            PrtType nullType;
            this.returnTo = null;
            this.reason = ContinuationReason.Return;
            this.id = null;
            nullType = PrtType.PrtMkPrimitiveType(PrtTypeKind.PRT_KIND_NULL);
            this.retVal = PrtValue.PrtMkDefaultValue(nullType);
        }

        public void ReturnVal(PrtValue val)
        {
            this.returnTo = null;
            this.reason = ContinuationReason.Return;
            this.id = null;
            this.retVal = val;
        }

        public void Pop()
        {
            this.returnTo = null;
            this.reason = ContinuationReason.Pop;
            this.id = null;
            this.retVal = null;
        }

        public void Raise()
        {
            this.returnTo = null;
            this.reason = ContinuationReason.Raise;
            this.id = null;
            this.retVal = null;
        }

        public void Send(int ret, List<PrtValue> locals)
        {
            this.returnTo = null;
            this.reason = ContinuationReason.Send;
            this.id = null;
            this.retVal = null;
            this.PushReturnTo(ret, locals);
        }

        void NewMachine(int ret, List<PrtValue> locals, MachineHandle o)
        {
            this.returnTo = null;
            this.reason = ContinuationReason.NewMachine;
            this.id = o;
            this.retVal = null;
            this.PushReturnTo(ret, locals);
        }

        void Receive(int ret, List<PrtValue> locals)
        {
            this.returnTo = null;
            this.reason = ContinuationReason.Receive;
            this.id = null;
            this.retVal = null;
            this.PushReturnTo(ret, locals);
        }

        void Nondet(int ret, List<PrtValue> locals)
        {
            this.returnTo = null;
            this.reason = ContinuationReason.Nondet;
            this.id = null;
            this.retVal = null;
            this.PushReturnTo(ret, locals);
        }
    }

    //Skipping FairScheduler

    public enum GateStatus : int
    {
        Init,
        Selected,
        Closed,
    };

    public enum StateTemperature : int
    {
        Cold,
        Warm,
        Hot,
    };

    //Skipping FairChoice, FairCycle
}
