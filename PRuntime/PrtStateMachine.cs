using System;
using System.Collections;
using System.Collections.Generic;

namespace P.PRuntime
{
    #region P state machines Implementation

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

    public abstract class PrtSMMethod
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

        public abstract void Dispatch(PrtStateMachine m);
    }

    public abstract class Machine<T> : BaseMachine where T : Machine<T>
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

        public void PushState(State<T> s)
        {
            StateStack<T> ss = new StateStack<T>();
            ss.next = this.stack;
            ss.state = s;
            this.stack = ss;
        }

        public void PopState()
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

        internal sealed class Start : PrtSMMethod
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

            public override void Dispatch(PrtStateMachine p)
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

            private void Enter(PrtStateMachine p)
            {
                Machine<T>.Run callee = new Machine<T>.Run(application, machine, machine.StartState);
                p.Call(callee);
                StateImpl.IsCall = true;

                nextBlock = Blocks.B0;
            }

            private void B0(PrtStateMachine p)
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

        internal sealed class Run : PrtSMMethod
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

            public override void Dispatch(PrtStateMachine p)
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

            private void B5(PrtStateMachine p)
            {
                machine.Pop();
                p.Return(null, null);
                StateImpl.IsReturn = true;
            }

            private void B4(PrtStateMachine p)
            {
                doPop = ((Machine<T>.RunHelper)p.LastFunctionCompleted).ReturnValue;
                p.LastFunctionCompleted = null;

                //B1 is header of the "while" loop:
                nextBlock = Blocks.B1;
            }

            private void B3(PrtStateMachine p)
            {
                Machine<T>.RunHelper callee = new Machine<T>.RunHelper(application, machine, false);
                p.Call(callee);
                StateImpl.IsCall = true;

                nextBlock = Blocks.B4;
            }

            private void B2(PrtStateMachine p)
            {
                var stateStack = machine.stack;
                var hasNullTransitionOrAction = stateStack.HasNullTransitionOrAction();
                DequeueEventReturnStatus status;
                try
                {
                    status = machine.DequeueEvent(application, hasNullTransitionOrAction);
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

            private void B1(PrtStateMachine p)
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

            private void B0(PrtStateMachine p)
            {
                //Return from RunHelper:
                doPop = ((Machine<T>.RunHelper)p.LastFunctionCompleted).ReturnValue;
                p.LastFunctionCompleted = null;
                nextBlock = Blocks.B1;
            }

            private void Enter(PrtStateMachine p)
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
            private PrtValue payload;

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

            public override void Dispatch(PrtStateMachine p)
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
                clone.payload = this.payload.Clone();
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

            private void Enter(PrtStateMachine p)
            {
                var stateStack = machine.stack;
                this.state = stateStack.state;

                if (start)
                {
                    payload = machine.currentArg;
                    nextBlock = Blocks.B0;
                }
                else
                {
                    nextBlock = Blocks.B1;
                }
            }

            public void B0(PrtStateMachine p)
            {
                var stateStack = machine.stack;

                //enter:
                stateStack.CalculateDeferredAndActionSet();

                fun = state.entryFun;
                nextBlock = Blocks.B2;
            }

            private void B2(PrtStateMachine p)
            {
                var stateStack = machine.stack;

                Machine<T>.ReentrancyHelper callee = new Machine<T>.ReentrancyHelper(application, machine, fun, payload);
                p.Call(callee);
                StateImpl.IsCall = true;
                nextBlock = Blocks.B3;
            }

            private void B3(PrtStateMachine p)
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
                        Machine<T>.ReentrancyHelper callee = new Machine<T>.ReentrancyHelper(application, machine, state.exitFun, null);
                        p.Call(callee);
                        StateImpl.IsCall = true;

                        nextBlock = Blocks.B4;
                    }
                }
            }

            private void B4(PrtStateMachine p)
            {
                p.LastFunctionCompleted = null;

                _ReturnValue = true;
                p.Return(null, null);
                StateImpl.IsReturn = true;
            }

            private void B1(PrtStateMachine p)
            {
                var stateStack = machine.stack;
                var state = stateStack.state;
                var actionSet = stateStack.actionSet;

                //handle:
                payload = machine.currentArg;
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

            private void B5(PrtStateMachine p)
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

            private void B6(PrtStateMachine p)
            {
                Machine<T>.ReentrancyHelper callee = new Machine<T>.ReentrancyHelper(application, machine, state.exitFun, null);
                p.Call(callee);
                StateImpl.IsCall = true;

                nextBlock = Blocks.B7;
            }

            private void B7(PrtStateMachine p)
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
                    Machine<T>.ReentrancyHelper callee = new Machine<T>.ReentrancyHelper(application, machine, transition.fun, payload);
                    p.Call(callee);
                    StateImpl.IsCall = true;
                    nextBlock = Blocks.B8;
                }
            }

            private void B8(PrtStateMachine p)
            {
                payload = ((Machine<T>.ReentrancyHelper)p.LastFunctionCompleted).ReturnValue;
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

            public override void Dispatch(PrtStateMachine p)
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

            private void Enter(PrtStateMachine p)
            {
                machine.cont.Reset();
                fun.PushFrame((T)machine, payload);

                nextBlock = Blocks.B0;
            }

            private void B0(PrtStateMachine p)
            {
                try
                {
                    fun.Execute(application, (T)machine);
                }
                catch (PrtException ex)
                {
                    application.Exception = ex;
                }
                Machine<T>.ProcessContinuation callee = new ProcessContinuation(application, machine);
                p.Call(callee);
                StateImpl.IsCall = true;
                nextBlock = Blocks.B1;
            }

            private void B1(PrtStateMachine p)
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

            public override void Dispatch(PrtStateMachine p)
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

            private void Enter(PrtStateMachine p)
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

            private void B0(PrtStateMachine p)
            {
                // ContinuationReason.Receive
                _ReturnValue = false;
                p.Return(null, null);
                StateImpl.IsReturn = true;
            }
        }
    }
#endregion
    /// <summary>
    /// This class represents a dynamic instance of a state machine in Prt.
    /// </summary>
    [Serializable]
    public class PrtStateMachine
    {
        // <summary>
        // Constructor called when the state machine is created
        // </summary>
        public PrtStateMachine(PStateImpl state, PrtSMMethod entryPoint, string name, uint id)
        {
            this.StateImpl = state;
            this.entryPoint = entryPoint;
            this.name = name;
            this.id = id;

            this.Call(entryPoint);
        }

        // <summary>
        // Private constructor for cloning only
        // </summary>
        private PrtStateMachine(PStateImpl stateObj, string myName, uint myId)
        {
            StateImpl = stateObj;
            name = myName;
            id = myId;
        }

        [NonSerialized]
        internal readonly PStateImpl StateImpl;

        // <summary>
        // The friendly name of the state machine.
        // </summary>
        private string name;
        public string Name { get { return name; } }

        // <summary>
        // Identifier of the state machine process.
        // </summary>
        private uint id;
        public uint Id { get { return id; } }

        // <summary>
        // The initial point of execution for the state machine.
        // </summary>
        [NonSerialized]
        private PrtSMMethod entryPoint;
        public PrtSMMethod EntryPoint
        {
            get { return entryPoint; }
            set { entryPoint = value; }
        }

        public enum PrtSMStatus
        {
            Enabled,        // The state machine is enabled
            Blocked,        // The state machine is blocked on a dequeue or receive
            Halted,         // The state machine has halted
        };

        /// <summary>
        /// The state machine yields control
        /// </summary>
        private bool doYield;
        public bool DoYield
        {
            get
            {
                return doYield;
            }

            set
            {
                doYield = value;
            }
        }

        // IsPreemptible tells RunProcess whether interleaving is allowed
        public bool IsPreemptible
        {
            get { return doYield; }
        }

        /// <summary>
        /// Still need to figure out how this is going to be used
        /// </summary>
        internal bool choicePending;

        /// <summary>
        /// Current status of the state-machine should be set appropriately.
        /// </summary>
        private PrtSMStatus currentStatus;
        public PrtSMStatus CurrentStatus
        {
            get
            {
                return currentStatus;
            }

            set
            {
                currentStatus = value;
            }
        }

        [NonSerialized]
        private PrtSMMethod topOfStack;

        public PrtSMMethod TopOfStack { get { return topOfStack; } }

        [NonSerialized]
        private PrtSMMethod savedTopOfStack;

        private void doPush(PrtSMMethod method)
        {
            topOfStack = method;
            if (stackULEs == null)
                return;
            stackULEs.Push(new UndoPush(this));
        }

        private PrtSMMethod doPop()
        {
            if (stackULEs != null)
            {
                if (stackULEs.Count > 0 && stackULEs.Peek() is UndoPush)
                    stackULEs.Pop();
                else
                {
                    Debug.Assert(topOfStack == savedTopOfStack);
                    stackULEs.Push(new UndoPop(this, topOfStack));
                    savedTopOfStack = topOfStack.Caller;
                }
            }
            ZingMethod oldTop = topOfStack;
            topOfStack = topOfStack.Caller;
            return oldTop;
        }

        public void Call(PrtSMMethod method)
        {
            doPush(method);
        }

        public void Return(ZingSourceContext context, ZingAttribute contextAttribute)
        {
            ZingMethod returningMethod = doPop();

            this.atomicityLevel = returningMethod.SavedAtomicityLevel;

            // Keep a ref to the completed function so the caller can access
            // the return value and output parameters.

            if (topOfStack != null)
                lastFunctionCompleted = returningMethod;
            else
            {
                lastFunctionCompleted = null;
                middleOfTransition = false;
            }

            if (this.topOfStack == null && ZingerConfiguration.ExecuteTraceStatements && (this.name != null && this.name.Length != 0))
            {
                if (ZingerConfiguration.DegreeOfParallelism == 1)
                {
                    this.StateImpl.ReportEvent(new TerminateProcessEvent(context, contextAttribute));
                }
                else
                {
                    this.StateImpl.ReportEvent(new TerminateProcessEvent(context, contextAttribute, this.MyThreadId));
                }
            }
        }

        #region some predicate nonsense

        private static bool[] runningPredicateMethod = new bool[ZingerConfiguration.DegreeOfParallelism];

        internal static bool[] RunningPredicateMethod
        {
            get { return runningPredicateMethod; }
        }

        public class PredicateContextIndexer
        {
            public ZingAttribute[] predicateContext;

            public PredicateContextIndexer(int num)
            {
                this.predicateContext = new ZingAttribute[num];
            }

            public ZingAttribute this[int index]
            {
                get { return this.predicateContext[index]; }
                set { this.predicateContext[index] = value; }
            }
        }

        public static PredicateContextIndexer PredicateContext = new PredicateContextIndexer(ZingerConfiguration.DegreeOfParallelism);

        /*
        public static ZingAttribute PredicateContext
        {
            get { return predicateContext; }
            set { predicateContext = value; }
        }
        */

        public bool CallPredicateMethod(ZingMethod predicateMethod)
        {
            if (runningPredicateMethod[MyThreadId])
            {
                Debugger.Break();
                throw new Exception("Predicate !");
            }

            Process dummyProc = new Process(this.StateImpl, predicateMethod, string.Empty, 0);

            Exception savedException = this.StateImpl.Exception;
            this.StateImpl.Exception = null;

            while (dummyProc.TopOfStack != null)
            {
                runningPredicateMethod[MyThreadId] = true;
                this.StateImpl.RunBlocks(dummyProc);
                runningPredicateMethod[MyThreadId] = false;

                if (this.StateImpl.Exception != null)
                {
                    if (savedException == null)
                    {
                        this.StateImpl.Exception = new Exception("Predicate");
                        throw this.StateImpl.Exception;
                    }
                    else
                    {
                        // If we already have a pending exception on the state, just
                        // return false and restore the original exception.
                        this.StateImpl.Exception = savedException;
                        return false;
                    }
                }

                if (dummyProc.choicePending)
                    throw new Exception("Predicate");
            }
            this.StateImpl.Exception = savedException;
            return predicateMethod.BooleanReturnValue;
        }

        #endregion some predicate nonsense

        //
        // Find a stack frame capable of handling the exception, peeling off
        // stack frames as necessary to find someone. If nobody has a handler
        // in place, then report an unhandled exception as a Zing exception.
        //
        [SuppressMessage("Microsoft.Design", "CA1030:UseEventsWhereAppropriate")]
        public void RaiseZingException(int exception)
        {
            this.lastFunctionCompleted = null;

            doPop();

            while (topOfStack != null)
            {
                this.atomicityLevel = this.topOfStack.SavedAtomicityLevel;
                // If we find a handler, we're done
                if (this.topOfStack.RaiseZingException(exception))
                    return;

                doPop();
            }

            throw new ZingUnhandledExceptionException(exception);
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        public void RunNextBlock()
        {
            try
            {
                if (ZingerConfiguration.ExecuteTraceStatements)
                    Process.currentProcess[MyThreadId] = this;
                // Save the source context. This seems to be off by
                // one block if a ZingAssertionFailureException occurs
                if (Process.AssertionFailureCtx[MyThreadId] == null)
                {
                    Process.AssertionFailureCtx[MyThreadId] = new ZingSourceContext();
                }

                topOfStack.Context.CopyTo(Process.AssertionFailureCtx[MyThreadId]);
                Process.LastProcess[MyThreadId] = this;
                topOfStack.Dispatch(this);
            }
            catch (ZingException e)
            {
                this.StateImpl.Exception = e;
                (this.StateImpl.Exception as ZingException).myThreadId = MyThreadId;
            }
            catch (DivideByZeroException)
            {
                this.StateImpl.Exception = new ZingDivideByZeroException();
                (this.StateImpl.Exception as ZingException).myThreadId = MyThreadId;
            }
            catch (OverflowException)
            {
                this.StateImpl.Exception = new ZingOverflowException();
                (this.StateImpl.Exception as ZingException).myThreadId = MyThreadId;
            }
            catch (IndexOutOfRangeException)
            {
                this.StateImpl.Exception = new ZingIndexOutOfRangeException();
                (this.StateImpl.Exception as ZingException).myThreadId = MyThreadId;
            }
            catch (Exception e)
            {
                if (Debugger.IsAttached)
                    Debugger.Break();

                this.StateImpl.Exception =
                    new ZingUnexpectedFailureException("Unhandled exception in the Zing runtime", e);
                (this.StateImpl.Exception as ZingException).myThreadId = MyThreadId;
            }
            finally
            {
                // Add check for other ZingExceptions which are not thrown and append the serial number
                if (this.StateImpl.Exception != null && (this.StateImpl.Exception is ZingException))
                {
                    (this.StateImpl.Exception as ZingException).myThreadId = MyThreadId;
                }
                Process.currentProcess[MyThreadId] = null;
            }
        }

        [NonSerialized]
        private ZingMethod lastFunctionCompleted;

        //the following is used during application of effects
        public void UpdateDirectLastFunctionCompleted(ZingMethod method)
        {
            lastFunctionCompleted = method;
        }

        public ZingMethod LastFunctionCompleted
        {
            get { return lastFunctionCompleted; }
            set
            {
                Debug.Assert(value == null);
                if ((stackULEs != null) && (stackULEs.Count == 0))
                {
                    stackULEs.Push(new UndoResetLastFunctionCompleted(this, lastFunctionCompleted));
                }
                //value has to be null here!
                lastFunctionCompleted = value;
            }
        }

        /// <summary>
        /// Field indicating the thread Id when used during parallel exploration
        /// </summary>
        private int myThreadId = 0;

        public int MyThreadId
        {
            get { return myThreadId; }
            set { myThreadId = value; }
        }

        

        internal object Clone(StateImpl myState, bool shallowCopy)
        {
            Process clone = new Process(myState, Name, Id);

            clone.atomicityLevel = this.atomicityLevel;
            clone.middleOfTransition = this.middleOfTransition;
            clone.backTransitionEncountered = this.backTransitionEncountered;
            clone.choicePending = this.choicePending;
            // For parallel exploration
            clone.myThreadId = myState.MySerialNum;

            // Recursively clone the entire stack
            if (this.topOfStack != null)
                clone.topOfStack = this.topOfStack.Clone(myState, clone, shallowCopy);

            if (this.lastFunctionCompleted != null)
            {
                Debug.Fail("cannot happen anymore (xyc)");
                clone.lastFunctionCompleted = this.lastFunctionCompleted.Clone(myState, clone, true);
            }

            return clone;
        }

        #region Process delta data structures

        private class ProcessULE
        {
            // cloned components
            public int atomicityLevel;

            public bool middleOfTransition;
            public bool choicePending;

            // the following field added because of summarization
            public ZingMethod lastFunctionCompleted;

            // undoable components
            public Stack stackULEs;
        }

        private abstract class StackULE
        {
            protected Process process;

            protected StackULE(Process p)
            {
                process = p;
            }

            public void Undo()
            {
                doUndo();
            }

            protected abstract void doUndo();
        }

        private class UndoPush : StackULE
        {
            public UndoPush(Process p)
                : base(p) { }

            protected override void doUndo()
            {
                // to undo a push, we pop
                process.topOfStack = process.topOfStack.Caller;
            }
        }

        private class UndoPop : StackULE
        {
            private ZingMethod savedStackFrame;

            public UndoPop(Process p, ZingMethod theFrame)
                : base(p)
            {
                savedStackFrame = theFrame;
            }

            protected override void doUndo()
            {
                savedStackFrame.Caller = process.topOfStack;
                process.topOfStack = process.savedTopOfStack = savedStackFrame;
                savedStackFrame = null;
                process.topOfStack.DoRevert();
            }
        }

        private class UndoResetLastFunctionCompleted : StackULE
        {
            private ZingMethod savedLastFunctionCompleted;

            public UndoResetLastFunctionCompleted(Process p, ZingMethod l)
                : base(p)
            {
                savedLastFunctionCompleted = l;
            }

            protected override void doUndo()
            {
                process.lastFunctionCompleted = savedLastFunctionCompleted;
            }
        }

        private class UndoUpdate : StackULE
        {
            private object zingMethodULE;

            public UndoUpdate(Process p, object ule)
                : base(p)
            {
                zingMethodULE = ule;
            }

            protected override void doUndo()
            {
                object[] ules = new object[] { zingMethodULE };
                process.topOfStack.DoRollback(ules);
                zingMethodULE = null;
            }
        }

        private Stack stackULEs;

        #endregion Process delta data structures

        #region Private process delta methods

        private Stack checkInStackFrames()
        {
            // this is the first time we checked in
            if (stackULEs == null)
            {
                stackULEs = new Stack();
                for (savedTopOfStack = topOfStack;
                     savedTopOfStack != null;
                     savedTopOfStack = savedTopOfStack.Caller)
                    savedTopOfStack.DoCheckIn();

                savedTopOfStack = topOfStack;
                return null;
            }

            object zmULE = null;

            if (savedTopOfStack != null)
                zmULE = savedTopOfStack.DoCheckIn();

            // small optimization when no changes was made in the
            // current transition
            if (stackULEs.Count == 0 && zmULE == null)
            {
                Debug.Assert(savedTopOfStack == topOfStack);
                return null;
            }

            // the result
            Stack resStack = stackULEs;

            stackULEs = new Stack();

            ZingMethod stackFrame = topOfStack;

            // move newly pushed frames away from the result, save
            // them temporarily in stackULEs; while doing that, we
            // checkIn every newly pushed node
            while (resStack.Count > 0 && resStack.Peek() is UndoPush)
            {
                //object sfULE =
                // this would be the first time we check in these
                // freshly pushed nodes. so we discard their undo log
                // entries
                stackFrame.DoCheckIn();
                stackULEs.Push(resStack.Pop());
                stackFrame = stackFrame.Caller;
            }

            // everything below should be UndoPop's or UndoResetLastFunctionCompleted,
            // and if anything is to be saved, it should be right there at stackFrame
            Debug.Assert(resStack.Count == 0 || resStack.Peek() is UndoPop
                         || resStack.Peek() is UndoResetLastFunctionCompleted);
            Debug.Assert(savedTopOfStack == stackFrame);

            // insert zmULE between UndoPop objects and UndoPush
            // objects
            if (zmULE != null)
                resStack.Push(new UndoUpdate(this, zmULE));

            // move undoPush objects back into the result
            while (stackULEs.Count > 0)
                resStack.Push(stackULEs.Pop());

            savedTopOfStack = topOfStack;
            return resStack;
        }

        private void revertStackFrames()
        {
            Debug.Assert(stackULEs != null);

            if (savedTopOfStack != null)
                savedTopOfStack.DoRevert();
            while (stackULEs.Count > 0)
            {
                StackULE ule = (StackULE)stackULEs.Pop();
                ule.Undo();
            }
            Debug.Assert(savedTopOfStack == topOfStack);
        }

        private void rollbackStackFrames(Stack sules)
        {
            StackULE ule;

            Debug.Assert(stackULEs != null);
            Debug.Assert(stackULEs.Count == 0);

            if (sules == null)
            {
                savedTopOfStack = topOfStack;
                return;
            }
            while (sules.Count > 0)
            {
                ule = (StackULE)sules.Pop();
                ule.Undo();
            }
            savedTopOfStack = topOfStack;
        }

        #endregion Private process delta methods

        #region Public process delta methods

        public object DoCheckIn()
        {
            ProcessULE pULE = new ProcessULE();

            // cloned components
            pULE.atomicityLevel = atomicityLevel;
            pULE.middleOfTransition = middleOfTransition;
            pULE.choicePending = choicePending;
            pULE.lastFunctionCompleted = null;
            if (lastFunctionCompleted != null)
            {
                pULE.lastFunctionCompleted = lastFunctionCompleted.Clone(StateImpl, this, false);
            }

            // undoable ones
            pULE.stackULEs = checkInStackFrames();
            return pULE;
        }

        public void DoCheckout(object currentUle)
        {
            ProcessULE pULE = (ProcessULE)currentUle;

            // cloned components
            atomicityLevel = pULE.atomicityLevel;
            middleOfTransition = pULE.middleOfTransition;
            choicePending = pULE.choicePending;
            lastFunctionCompleted = pULE.lastFunctionCompleted;

            // undoable ones -- do nothing
        }

        public void DoRevert()
        {
            // cloned components -- do nothing

            // undoable ones
            revertStackFrames();
        }

        public void DoRollback(object[] uleList)
        {
            // cloned components -- do nothing

            // undoable ones
            int n = uleList.Length, i;

            for (i = 0; i < n; i++)
                rollbackStackFrames(((ProcessULE)uleList[i]).stackULEs);
        }

        #endregion Public process delta methods

        #region Fingerprinting

        private MemoryStream memStream;
        private BinaryWriter binWriter;

        /// <summary>
        ///  Compute the fingerprint of a process.
        ///      The current implementation computes this fingerprint nonincrementally.
        ///      But in the future this can be made incremental
        /// </summary>
        /// <param name="state"></param>
        /// <returns>Fingerprint of a process</returns>
        public Fingerprint ComputeFingerprint(StateImpl state)
        {
            if (memStream == null)
            {
                memStream = new MemoryStream();
                binWriter = new BinaryWriter(memStream);
            }
            binWriter.Seek(0, SeekOrigin.Begin);
            this.WriteString(state, binWriter);
            Fingerprint procPrint = StateImpl.FingerprintNonHeapBuffer(memStream.GetBuffer(), (int)memStream.Position);
            return procPrint;
            //return Fingerprint.ComputeFingerprint(memStream.GetBuffer(), (int) memStream.Position, 0);
        }

        internal void WriteString(StateImpl state, BinaryWriter bw)
        {
            for (ZingMethod m = topOfStack; m != null; m = m.Caller)
                m.WriteString(state, bw);

            if (lastFunctionCompleted != null)
            {
                Debug.Assert(state.Exception != null);
                bw.Write((ushort)0xcafe);
                lastFunctionCompleted.WriteOutputsString(state, bw);
            }

            // We write a unique delimiter at the end of each process to remove any
            // potential ambiguity from our generated string. We guarantee that the
            // type id of a stack frame will never be "0xface". Without this delimiter
            // it's at least theoretically possible that two distinct states could
            // yield the same string.

            bw.Write((ushort)0xface);
        }

        #endregion Fingerprinting
    }
}