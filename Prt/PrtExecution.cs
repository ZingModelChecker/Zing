using Z = Microsoft.Zing;
using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;

namespace Microsoft.Prt
{
    /*    
      class Application : PStateImpl {
        // one list for each machine and monitor type
        List<A> A_list;
        List<B> B_list;
         ...
         // implement AllMachines, AllMonitors
         

        // What is the design of the constructor?
        public Application() { ... } 

        Each event becomes a static field in Application class
      
        public static Event A = new Event(...);

        Each static function  B becomes a class and static field in Application class

        // Can static functions be called from monitors
        // If yes, the type parameter must be BaseMachine; if no, it can be Machine
        public class B_Fun : Fun<BaseMachine> {
            // implement the abstract methods in Fun
        }

        public static B_Fun B = new B_Fun();  // static field declaration in Application

        Each machine becomes a class in Application class

        public class Foo : Machine {
            public Foo(int instance): base(instance, numFields, maxBufferSize) {
                // initialize fields
            }

            Create getter/setter for each field so that code in functions looks nice

            Each function A in machine Foo becomes a class and a static field

            public class A_Fun : Fun<Foo> {
                // implement the abstract methods in Fun
            }
            public static A_Fun A = new A_Fun();

            Each state X in machine Foo becomes a static field
            
            public static State X = new State(...);

            static {
                // Create transitions
                // Wire up the states and transitions
                // Put the appropriate funs in states and transitions 
                // Presumably the static fields containing funs have already been initialized
            }
        }
     */

    public abstract class PStateImpl : Z.StateImpl
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
    }

    public abstract class Root
    {
        public abstract string Name
        {
            get;
        }

        public List<PrtValue> fields;
        public Event currentEvent;
        public PrtValue currentArg;
        public Continuation cont;
    }

    public abstract class BaseMachine : Root
    {
        public bool halted;
        public bool enabled;
    }

    public abstract class BaseMonitor : Root
    {
        public bool hot;
    }

    public abstract class Monitor<T> : BaseMonitor
    {
        public abstract State<T> StartState
        {
            get;
        }

        public abstract void Invoke();
    }

    public abstract class Machine<T> : BaseMachine where T: Machine<T> 
    {
        public StateStack<T> stack;
        public EventBuffer<T> buffer;
        public int maxBufferSize;
        public int instance;
        public HashSet<Event> receiveSet;

        public Machine(int instance, int maxBufferSize)
        {
            halted = false;
            enabled = true;
            stack = null;
            fields = new List<PrtValue>();
            cont = new Continuation();
            buffer = new EventBuffer<T>();
            this.maxBufferSize = maxBufferSize;
            this.instance = instance;
            currentEvent = null;
            currentArg = PrtValue.NullValue;
            receiveSet = new HashSet<Event>();
        }

        public void Push(State<T> s)
        {
            StateStack<T> ss = new StateStack<T>();
            ss.next = this.stack;
            ss.state = s;
            this.stack = ss;
        }

        public void Pop()
        {
            this.stack = this.stack.next;
        }

        public abstract State<T> StartState
        {
            get;
        }

        public void EnqueueEvent(PStateImpl application, Event e, PrtValue arg, Machine<T> source)
        {
            PrtType prtType;

            if (e == null)
            {
                throw new PrtIllegalEnqueueException("Enqueued event must be non-null");
            }

            //assertion to check if argument passed inhabits the payload type.
            prtType = e.payload;

            if ((arg.type.typeKind == PrtTypeKind.PRT_KIND_NULL) 
                || (prtType.typeKind != PrtTypeKind.PRT_KIND_NULL && !PrtValue.PrtInhabitsType(arg, prtType)))
            {
                throw new PrtInhabitsTypeException(String.Format("Type of payload <{0}> does not match the expected type <{1}> with event <{2}>", arg.type.ToString(), prtType.ToString(), e.name));
            }
            
            if (halted)
            {
                application.Trace(null, null,
                    @"<EnqueueLog> {0}-{1} Machine has been halted and Event {2} is dropped",
                    this.Name, this.instance, e.name);
            }
            else
            {
                if (arg != null)
                {
                    application.Trace(null, null,
                        @"<EnqueueLog> Enqueued Event < {0} > in {1}-{2} by {3}-{4}",
                        e.name, this.Name, this.instance, source.Name, source.instance);
                }
                else
                {
                    application.Trace(null, null,
                        @"<EnqueueLog> Enqueued Event <{0}, {1}> in {2}-{3} by {4}-{5}",
                        e.name, arg.ToString(), this.Name, this.instance, source.Name, source.instance);
                }

                this.buffer.EnqueueEvent(e, arg);
                if (this.maxBufferSize != -1 && this.buffer.eventBufferSize > this.maxBufferSize)
                {
                    throw new PrtMaxBufferSizeExceededException(
                        String.Format(@"<EXCEPTION> Event Buffer Size Exceeded {0} in Machine {1}-{2}",
                        this.maxBufferSize, this.Name, this.instance));
                }
                if (!enabled && this.buffer.IsEnabled((T)this))
                {
                    enabled = true;
                }
                if (enabled)
                {
                    //application.invokescheduler("enabled", machineId, source.machineId);
                }
            }
        }

        public enum DequeueEventReturnStatus { SUCCESS, NULL, BLOCKED };

        public DequeueEventReturnStatus DequeueEvent(PStateImpl application, bool hasNullTransition)
        {
            currentEvent = null;
            currentArg = null;
            buffer.DequeueEvent((T)this);
            if (currentEvent != null)
            {
                if (currentArg == null)
                {
                    throw new PrtInternalException("Internal error: currentArg is null");
                }
                if (!enabled)
                {
                    throw new PrtInternalException("Internal error: Tyring to execute blocked machine");
                }
                
                application.Trace(null, null,
                    "<DequeueLog> Dequeued Event < {0}, {1} > at Machine {2}-{3}\n", 
                    currentEvent.name, currentArg.ToString(), Name, instance);
                receiveSet = new HashSet<Event>();
                return DequeueEventReturnStatus.SUCCESS;
            }
            else if (hasNullTransition || receiveSet.Contains(currentEvent))
            {
                if (!enabled)
                {
                    throw new PrtInternalException("Internal error: Tyring to execute blocked machine");
                }
                application.Trace(null, null,
                    "<NullTransLog> Null transition taken by Machine {0}-{1}\n", 
                    Name, instance);
                currentArg = PrtValue.NullValue;
                receiveSet = new HashSet<Event>();
                return DequeueEventReturnStatus.NULL;
            }
            else
            {
                //invokescheduler("blocked", machineId);
                if (!enabled)
                {
                    throw new Z.ZingAssumeFailureException();
                }
                enabled = false;
                if (application.Deadlock)
                {
                    throw new PrtDeadlockException("Deadlock detected");
                }
                return DequeueEventReturnStatus.BLOCKED;
            }
        }

        public abstract class PrtExecutorFun
        {
            public abstract PStateImpl StateImpl
            {
                get;
                set;
            }

            public abstract ushort NextBlock
            {
                get;
                set;
            }

            public abstract string ProgramCounter
            {
                get;
            }

            public abstract void Dispatch(Z.Process p);
        }

        internal sealed class Start : PrtExecutorFun
        {
            private static readonly short typeId = 0;

            private PStateImpl application;
            private Machine<T> machine;

            // locals
            private Blocks nextBlock;

            public Start(PStateImpl app, Machine<T> machine)
            {
                application = app;
                this.machine = machine;
                nextBlock = Blocks.Enter;
            }

            public override PStateImpl StateImpl
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

            private enum Blocks : ushort
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

            /*
            public override Z.ZingMethod Clone(PStateImpl application, Z.Process myProcess, bool shallowCopy)
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

            public override void WriteString(PStateImpl state, BinaryWriter bw)
            {
                bw.Write(typeId);
                bw.Write(((ushort)nextBlock));
            }
            */

            private void Enter(Z.Process p)
            {
                Machine<T>.Run callee = new Machine<T>.Run(application, machine, machine.StartState);
                p.Call(callee);
                StateImpl.IsCall = true;

                nextBlock = Blocks.B0;
            }

            private void B0(Z.Process p)
            {
                p.LastFunctionCompleted = null;

                var currentEvent = machine.currentEvent;

                //Checking if currentEvent is halt:
                if (currentEvent == Event.HaltEvent)
                {
                    machine.stack = null;
                    machine.buffer = null;
                    machine.currentArg = null;
                    machine.halted = true;
                    machine.enabled = false;

                    p.Return(null, null);
                    StateImpl.IsReturn = true;
                }
                else
                {
                    application.Trace(
                        null, null, 
                        @"<StateLog> Unhandled event exception by machine Real1-{0}", 
                        machine.instance);
                    this.StateImpl.Exception = new PrtUnhandledEventException("Unhandled event exception by machine <mach name>");
                    p.Return(null, null);
                    StateImpl.IsReturn = true;
                }
            }
        }

        internal sealed class Run : PrtExecutorFun
        {
            private static readonly short typeId = 1;

            private PStateImpl application;
            private Machine<T> machine;

            // inputs
            private State<T> state;
            
            // locals
            private Blocks nextBlock;
            private bool doPop;

            public Run(PStateImpl app, Machine<T> machine)
            {
                application = app;
                nextBlock = Blocks.Enter;
                this.machine = machine;
                this.state = null;
            }

            public Run(PStateImpl app, Machine<T> machine, State<T> state)
            {
                application = app;
                nextBlock = Blocks.Enter;
                this.machine = machine;
                this.state = state;
            }

            public override PStateImpl StateImpl
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

            private enum Blocks : ushort
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

            /*
            public override Z.ZingMethod Clone(PStateImpl application, Z.Process myProcess, bool shallowCopy)
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

            public override void WriteString(PStateImpl state, BinaryWriter bw)
            {
                bw.Write(typeId);
                bw.Write((ushort)nextBlock);
            }
            */

            private void B5(Z.Process p)
            {
                machine.Pop();
                p.Return(null, null);
                StateImpl.IsReturn = true;
            }

            private void B4(Z.Process p)
            {
                doPop = ((Machine<T>.RunHelper)p.LastFunctionCompleted).ReturnValue;
                p.LastFunctionCompleted = null;

                //B1 is header of the "while" loop:
                nextBlock = Blocks.B1;
            }

            private void B3(Z.Process p)
            {
                Machine<T>.RunHelper callee = new Machine<T>.RunHelper(application, machine, false);
                p.Call(callee);
                StateImpl.IsCall = true;

                nextBlock = Blocks.B4;
            }

            private void B2(Z.Process p)
            {
                var stateStack = machine.stack;
                var hasNullTransitionOrAction = stateStack.HasNullTransitionOrAction();
                DequeueEventReturnStatus status;
                try
                {
                    status = machine.DequeueEvent(application, hasNullTransitionOrAction);
                }
                catch(PrtException ex)
                {
                    application.Exception = ex;
                    p.Return(null, null);
                    StateImpl.IsReturn = true;
                    return;
                }

                if (status == DequeueEventReturnStatus.BLOCKED)
                {
                    p.MiddleOfTransition = false;
                    nextBlock = Blocks.B2;
                }
                else if (status == DequeueEventReturnStatus.SUCCESS)
                {
                    nextBlock = Blocks.B3;
                }
                else
                {
                    p.MiddleOfTransition = false;
                    nextBlock = Blocks.B3;
                }
            }

            private void B1(Z.Process p)
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

            private void B0(Z.Process p)
            {
                //Return from RunHelper:
                doPop = ((Machine<T>.RunHelper)p.LastFunctionCompleted).ReturnValue;
                p.LastFunctionCompleted = null;
                nextBlock = Blocks.B1;
            }

            private void Enter(Z.Process p)
            {
                machine.Push(state);

                Machine<T>.RunHelper callee = new Machine<T>.RunHelper(application, machine, true);
                p.Call(callee);
                StateImpl.IsCall = true;

                nextBlock = Blocks.B0;
            }
        }

        internal sealed class RunHelper : PrtExecutorFun
        {
            private static readonly short typeId = 2;

            private PStateImpl application;
            private Machine<T> machine;

            // inputs
            private bool start;

            // locals
            private Blocks nextBlock;
            private State<T> state;
            private Fun<T> fun;
            private Transition<T> transition;

            // output
            private bool _ReturnValue;
            public bool ReturnValue
            {
                get
                {
                    return _ReturnValue;
                }
            }

            public RunHelper(PStateImpl app, Machine<T> machine, bool start)
            {
                application = app;
                nextBlock = Blocks.Enter;
                this.machine = machine;
                this.start = start;
            }

            public override PStateImpl StateImpl
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
           
            /*
            public override Z.ZingMethod Clone(PStateImpl application, Z.Process myProcess, bool shallowCopy)
            {
                RunHelper clone = new RunHelper(application, this.machine, this.start);
                clone.nextBlock = this.nextBlock;
                clone.state = this.state;
                clone.transition = this.transition;
                clone.fun = this.fun;
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

            public override void WriteString(PStateImpl state, BinaryWriter bw)
            {
                bw.Write(typeId);
                bw.Write(((ushort)nextBlock));
            }
            */

            private void Enter(Z.Process p)
            {
                var stateStack = machine.stack;
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
                var stateStack = machine.stack;

                //enter:
                stateStack.CalculateDeferredAndActionSet();

                fun = state.entryFun;
                nextBlock = Blocks.B2;
            }

            private void B2(Z.Process p)
            {
                var stateStack = machine.stack;

                Machine<T>.ReentrancyHelper callee = new Machine<T>.ReentrancyHelper(application, machine, fun);
                p.Call(callee);
                StateImpl.IsCall = true;
                nextBlock = Blocks.B3;
            }

            private void B3(Z.Process p)
            {
                p.LastFunctionCompleted = null;

                var reason = machine.cont.reason;
                if (reason == ContinuationReason.Raise)
                {
                    //goto handle;
                    nextBlock = Blocks.B1;
                }
                else
                {
                    machine.currentEvent = null;
                    machine.currentArg = PrtValue.NullValue;
                    if (reason != ContinuationReason.Pop)
                    {
                        _ReturnValue = false;
                        p.Return(null, null);
                        StateImpl.IsReturn = true;
                    }
                    else
                    {
                        Machine<T>.ReentrancyHelper callee = new Machine<T>.ReentrancyHelper(application, machine, state.exitFun);
                        p.Call(callee);
                        StateImpl.IsCall = true;

                        nextBlock = Blocks.B4;
                    }
                }
            }

            private void B4(Z.Process p)
            {
                p.LastFunctionCompleted = null;

                _ReturnValue = true;
                p.Return(null, null);
                StateImpl.IsReturn = true;
            }

            private void B1(Z.Process p)
            {
                var stateStack = machine.stack;
                var state = stateStack.state;
                var actionSet = stateStack.actionSet;

                //handle:
                if (actionSet.Contains(machine.currentEvent))
                {
                    fun = stateStack.Find(machine.currentEvent);
                    //goto execute;
                    nextBlock = Blocks.B2;
                }
                else
                {
                    transition = state.FindPushTransition(machine.currentEvent);
                    if (transition != null)
                    {
                        Machine<T>.Run callee = new Machine<T>.Run(application, machine, transition.to);
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

            private void B5(Z.Process p)
            {
                p.LastFunctionCompleted = null;

                if (machine.currentEvent == null)
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

            private void B6(Z.Process p)
            {
                Machine<T>.ReentrancyHelper callee = new Machine<T>.ReentrancyHelper(application, machine, state.exitFun);
                p.Call(callee);
                StateImpl.IsCall = true;

                nextBlock = Blocks.B7;
            }

            private void B7(Z.Process p)
            {
                p.LastFunctionCompleted = null;

                transition = state.FindTransition(machine.currentEvent);
                if (transition == null)
                {
                    _ReturnValue = true;
                    p.Return(null, null);
                    StateImpl.IsReturn = true;
                }
                else
                {
                    Machine<T>.ReentrancyHelper callee = new Machine<T>.ReentrancyHelper(application, machine, transition.fun);
                    p.Call(callee);
                    StateImpl.IsCall = true;
                    nextBlock = Blocks.B8;
                }
            }

            private void B8(Z.Process p)
            {
                p.LastFunctionCompleted = null;
                var stateStack = machine.stack;
                stateStack.state = transition.to;
                state = stateStack.state;

                //goto enter;
                nextBlock = Blocks.B0;
            }
        }

        internal sealed class ReentrancyHelper : PrtExecutorFun
        {
            private static readonly short typeId = 3;

            private PStateImpl application;
            private Machine<T> machine;
            private Fun<T> fun;

            // inputs
            private PrtValue payload;

            // locals
            private Blocks nextBlock;

            // output
            private PrtValue _ReturnValue;

            public PrtValue ReturnValue
            {
                get
                {
                    return _ReturnValue;
                }
            }

            public ReentrancyHelper(PStateImpl app, Machine<T> machine, Fun<T> fun, PrtValue payload)
            {
                this.application = app;
                this.machine = machine;
                this.fun = fun;
                this.payload = payload;
            }

            public override PStateImpl StateImpl
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
                }
            }

            /*
            public override Z.ZingMethod Clone(PStateImpl application, Z.Process myProcess, bool shallowCopy)
            {
                ReentrancyHelper clone = new ReentrancyHelper(application, this.machine, this.fun, this.payload);
                clone.nextBlock = this.nextBlock;
                clone.fun = this.fun;
                clone.payload = this.payload;
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

            public override void WriteString(PStateImpl state, BinaryWriter bw)
            {
                bw.Write(typeId);
                bw.Write(((ushort)nextBlock));
            }
            */

            private void Enter(Z.Process p)
            {
                machine.cont.Reset();
                fun.PushFrame((T)machine, payload);

                nextBlock = Blocks.B0;
            }

            private void B0(Z.Process p)
            {
                try
                {
                    fun.Execute(application, (T)machine);
                }
                catch(PrtException ex)
                {
                    application.Exception = ex;
                }
                Machine<T>.ProcessContinuation callee = new ProcessContinuation(application, machine);
                p.Call(callee);
                StateImpl.IsCall = true;
                nextBlock = Blocks.B1;
            }

            private void B1(Z.Process p)
            {
                var doPop = ((Machine<T>.ProcessContinuation)p.LastFunctionCompleted).ReturnValue;
                p.LastFunctionCompleted = null;

                if (doPop)
                {
                    if (machine.cont.retLocals == null)
                    {
                        _ReturnValue = payload;
                    }
                    else
                    {
                        _ReturnValue = machine.cont.retLocals[0];
                    }
                    p.Return(null, null);
                    StateImpl.IsReturn = true;
                }
                else
                {
                    nextBlock = Blocks.B0;
                }
            }
        }

        internal sealed class ProcessContinuation : PrtExecutorFun
        {
            private static readonly short typeId = 4;

            private PStateImpl application;
            private Machine<T> machine;

            // locals
            private Blocks nextBlock;

            // output
            private bool _ReturnValue;

            public bool ReturnValue
            {
                get { return _ReturnValue; }
            }

            public ProcessContinuation(PStateImpl app, Machine<T> machine)
            {
                application = app;
                this.machine = machine;
                nextBlock = Blocks.Enter;
            }

            public override PStateImpl StateImpl
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
                B0 = 2
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

            /*
            public override Z.ZingMethod Clone(PStateImpl application, Z.Process myProcess, bool shallowCopy)
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

            public override void WriteString(PStateImpl state, BinaryWriter bw)
            {
                bw.Write(typeId);
                bw.Write(((ushort)nextBlock));
            }
            */

            private void Enter(Z.Process p)
            {
                var cont = machine.cont;
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
                    DequeueEventReturnStatus status;
                    try
                    {
                        status = machine.DequeueEvent(application, false);
                    }
                    catch (PrtException ex)
                    {
                        application.Exception = ex;
                        p.Return(null, null);
                        StateImpl.IsReturn = true;
                        return;
                    }

                    if (status == DequeueEventReturnStatus.BLOCKED)
                    {
                        p.MiddleOfTransition = false;
                        nextBlock = Blocks.Enter;
                    }
                    else if (status == DequeueEventReturnStatus.SUCCESS)
                    {
                        nextBlock = Blocks.B0;
                    }
                    else
                    {
                        p.MiddleOfTransition = false;
                        nextBlock = Blocks.B0;
                    }
                }
                if (reason == ContinuationReason.Nondet)
                {
                    application.SetPendingChoices(p, new object[] { false, true });
                    cont.nondet = ((Boolean)application.GetSelectedChoiceValue(p));
                    nextBlock = Blocks.B0;
                }
                if (reason == ContinuationReason.NewMachine)
                {
                    //yield;
                    p.MiddleOfTransition = false;
                    nextBlock = Blocks.B0;
                }
                if (reason == ContinuationReason.Send)
                {
                    //yield;
                    p.MiddleOfTransition = false;
                    nextBlock = Blocks.B0;
                }
            }

            private void B0(Z.Process p)
            {
                // ContinuationReason.Receive
                _ReturnValue = false;
                p.Return(null, null);
                StateImpl.IsReturn = true;
            }
        }
    }

    public abstract class Fun<T>
    {
        public abstract string Name
        {
            get;
        }

        public abstract void PushFrame(T parent, params PrtValue[] args);

        public abstract void Execute(PStateImpl application, T parent);
    }

    public class Event
    {
        public static Event NullEvent;
        public static Event HaltEvent;
        public string name;
        public PrtType payload;
        public int maxInstances;
        public bool doAssume;

        public Event(string name, PrtType payload, int mInstances, bool doAssume)
        {
            this.name = name;
            this.payload = payload;
            this.maxInstances = mInstances;
            this.doAssume = doAssume;
        }
    };

    public class Transition<T>
    {
        public Event evt;
        public Fun<T> fun; // isPush <==> fun == null
        public State<T> to;

        public Transition(Event evt, Fun<T> fun, State<T> to)
        {
            this.evt = evt;
            this.fun = fun;
            this.to = to;
        }
    };

    public enum StateTemperature
    {
        Cold,
        Warm,
        Hot
    };

    public class State<T>
    {
        public string name;
        public Fun<T> entryFun;
        public Fun<T> exitFun;
        public Dictionary<Event, Transition<T>> transitions;
        public Dictionary<Event, Fun<T>> dos;
        public bool hasNullTransition;
        public StateTemperature temperature;
        public HashSet<Event> deferredSet;

        public State(string name, Fun<T> entryFun, Fun<T> exitFun, bool hasNullTransition, StateTemperature temperature)
        {
            this.name = name;
            this.entryFun = entryFun;
            this.exitFun = exitFun;
            this.transitions = new Dictionary<Event, Transition<T>>();
            this.dos = new Dictionary<Event, Fun<T>>();
            this.hasNullTransition = hasNullTransition;
            this.temperature = temperature;
        }

        public Transition<T> FindPushTransition(Event evt)
        {
            if (transitions.ContainsKey(evt))
            {
                Transition<T> transition = transitions[evt];
                if (transition.fun == null)
                    return transition;
            }
            return null;
        }

        public Transition<T> FindTransition(Event evt)
        {
            if (transitions.ContainsKey(evt))
            {
                return transitions[evt];
            }
            else
            {
                return null;
            }
        }
    };

    public class EventNode
    {
        public EventNode next;
        public EventNode prev;
        public Event e;
        public PrtValue arg;
    }

    public class EventBuffer<T> where T: Machine<T>
    {
        public EventNode head;
        public int eventBufferSize;

        public EventBuffer()
        {
            EventNode node = new EventNode();
            node.next = node;
            node.prev = node;
            node.e = null;
            head = node;
            eventBufferSize = 0;
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
                        throw new Z.ZingAssumeFailureException();
                    }
                    else
                    {
                        throw new PrtMaxEventInstancesExceededException(
                            String.Format(@"< Exception > Attempting to enqueue event {0} more than max instance of {1}\n", e.name, e.maxInstances));
                    }
                }
                else
                {
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

        public void DequeueEvent(T owner)
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

        public bool IsEnabled(T owner)
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

    public class StateStack<T>
    {
        public State<T> state;
        public HashSet<Event> deferredSet;
        public HashSet<Event> actionSet;
        public StateStack<T> next;

        public Fun<T> Find(Event f)
        {
            if (state.dos.ContainsKey(f))
            {
                return state.dos[f];
            }
            else
            {
                return next.Find(f);
            }
        }

        public void CalculateDeferredAndActionSet()
        {
            deferredSet = new HashSet<Event>();
            if (next != null)
            {
                deferredSet.UnionWith(next.deferredSet);
            }
            deferredSet.UnionWith(state.deferredSet);
            deferredSet.ExceptWith(state.dos.Keys);
            deferredSet.ExceptWith(state.transitions.Keys);

            actionSet = new HashSet<Event>();
            if (next != null)
            {
                actionSet.UnionWith(next.actionSet);
            }
            actionSet.ExceptWith(state.deferredSet);
            actionSet.UnionWith(state.dos.Keys);
            actionSet.ExceptWith(state.transitions.Keys);
        }

        public bool HasNullTransitionOrAction()
        {
            if (state.hasNullTransition) return true;
            return actionSet.Contains(Event.NullEvent);
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
        public BaseMachine id;
        public PrtValue retVal;
        public List<PrtValue> retLocals;

        // The nondet field is different from the fields above because it is used 
        // by ReentrancyHelper to pass the choice to the nondet choice point.
        // Therefore, nondet should not be reinitialized in this class.
        public bool nondet;

        public Continuation()
        {
            returnTo = null;
            reason = ContinuationReason.Return;
            id = null;
            retVal = null;
            nondet = false;
            retLocals = null;
        }

        public void Reset()
        {
            this.returnTo = null;
            this.reason = ContinuationReason.Return;
            this.id = null;
            this.retVal = null;
            this.nondet = false;
            this.retLocals = null;
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

        public void Return(List<PrtValue> retLocals)
        {
            this.returnTo = null;
            this.reason = ContinuationReason.Return;
            this.id = null;
            this.retVal = PrtValue.NullValue;
            this.retLocals = retLocals;
        }

        public void ReturnVal(PrtValue val, List<PrtValue> retLocals)
        {
            this.returnTo = null;
            this.reason = ContinuationReason.Return;
            this.id = null;
            this.retVal = val;
            this.retLocals = retLocals;
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

        void NewMachine(int ret, List<PrtValue> locals, BaseMachine o)
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
}