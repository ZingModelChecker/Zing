using Z = Microsoft.Zing;
using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;

namespace Microsoft.Prt
{
    public abstract class Machine
    {
        public abstract State StartState
        {
            get;
        }

        public abstract string Name
        {
            get;
        }

        public static HashSet<Machine> halted = new HashSet<Machine>();
        public static HashSet<Machine> enabled = new HashSet<Machine>();
        public static HashSet<Machine> hot = new HashSet<Machine>();

        public StateStack stack;
        public Continuation cont;
        public EventBuffer buffer;
        public int maxBufferSize;
        public int instance;
        public Event currentEvent;
        public PrtValue currentArg;
        public HashSet<Event> receiveSet;

        public Machine(int instance, int maxBufferSize)
        {
            stack = null;
            cont = new Continuation();
            buffer = new EventBuffer();
            this.maxBufferSize = maxBufferSize;
            this.instance = instance;
            currentEvent = null;
            currentArg = PrtValue.NullValue;
            receiveSet = new HashSet<Event>();
        }

        public void Push()
        {
            StateStack s = new StateStack();
            s.next = this.stack;
            this.stack = s;
        }

        public void Pop()
        {
            this.stack = this.stack.next;
        }

        public void EnqueueEvent(Z.StateImpl application, Event e, PrtValue arg, Machine source)
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
                    this.Name, this.instance, e.name);
            }
            else
            {
                if (arg != null)
                {
                    application.Trace(
                        null,
                        null,
                        @"<EnqueueLog> Enqueued Event < {0} > in Machine {1}-{2} by {3}-{4}",
                        e.name, this.Name, this.instance, source.Name, source.instance);
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
                        this.maxBufferSize, this.Name, this.instance);
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
                Debug.Assert(Machine.enabled.Contains(this), "Internal error");
                //trace("<DequeueLog> Dequeued Event < {0}, ", currentEvent.name); PRT_VALUE.Print(currentArg); trace(" > at Machine {0}-{1}\n", machineName, instance);
                receiveSet = new HashSet<Event>();
                return DequeueEventReturnStatus.SUCCESS;
            }
            else if (hasNullTransition || receiveSet.Contains(currentEvent))
            {
                Debug.Assert(Machine.enabled.Contains(this), "Internal error");
                //trace("<NullTransLog> Null transition taken by Machine {0}-{1}\n", machineName, instance);
                currentArg = PrtValue.NullValue;
                //FairScheduler.AtYieldStatic(this);
                //FairChoice.AtYieldOrChooseStatic();
                receiveSet = new HashSet<Event>();
                return DequeueEventReturnStatus.NULL;
            }
            else
            {
                //invokescheduler("blocked", machineId);
                //assume(this in SM_HANDLE.enabled);
                Machine.enabled.Remove(this);
                Debug.Assert(Machine.enabled.Count != 0 || Machine.hot.Count == 0, "Deadlock");
                //FairScheduler.AtYieldStatic(this);
                //FairChoice.AtYieldOrChooseStatic();
                return DequeueEventReturnStatus.BLOCKED;
            }
        }

        internal sealed class Start : Z.ZingMethod
        {
            private static readonly short typeId = 0;

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
                Machine.Run callee = new Machine.Run(application, machine, machine.StartState);
                p.Call(callee);
                StateImpl.IsCall = true;

                nextBlock = Blocks.B0;
            }

            public void B0(Z.Process p)
            {
                p.LastFunctionCompleted = null;

                var currentEvent = machine.currentEvent;
                var haltedSet = Machine.halted;
                var enabledSet = Machine.enabled;

                //Checking if currentEvent is halt:
                if (currentEvent == Event.HaltEvent)
                {
                    machine.stack = null;
                    machine.buffer = null;
                    machine.currentArg = null;
                    haltedSet.Add(machine);
                    enabledSet.Remove(machine);

                    p.Return(null, null);
                    StateImpl.IsReturn = true;
                }
                else
                {
                    application.Trace(
                        null, null, 
                        @"<StateLog> Unhandled event exception by machine Real1-{0}", 
                        machine.instance);
                    this.StateImpl.Exception = new Z.ZingAssertionFailureException(@"false", @"Unhandled event exception by machine <mach name>");
                    p.Return(null, null);
                    StateImpl.IsReturn = true;
                }
            }
        }

        internal sealed class Run : Z.ZingMethod
        {
            private static readonly short typeId = 1;

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
                machine.Pop();
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
                Machine.RunHelper callee = new Machine.RunHelper(application, machine, false);
                p.Call(callee);
                StateImpl.IsCall = true;

                nextBlock = Blocks.B4;
            }

            public void B2(Z.Process p)
            {
                var stateStack = machine.stack;
                var hasNullTransitionOrAction = stateStack.HasNullTransitionOrAction();
                var status = machine.DequeueEvent(hasNullTransitionOrAction);
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
                var stateStack = machine.stack;
                machine.Push();
                stateStack.state = state;

                Machine.RunHelper callee = new Machine.RunHelper(application, machine, true);
                p.Call(callee);
                StateImpl.IsCall = true;

                nextBlock = Blocks.B0;
            }
        }

        internal sealed class RunHelper : Z.ZingMethod
        {
            private static readonly short typeId = 2;

            private Z.StateImpl application;
            private Machine machine;

            // inputs
            private bool start;

            // locals
            private Blocks nextBlock;
            private State state;
            private Fun fun;
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
           
            public override Z.ZingMethod Clone(Z.StateImpl application, Z.Process myProcess, bool shallowCopy)
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

            public override void WriteString(Z.StateImpl state, BinaryWriter bw)
            {
                bw.Write(typeId);
                bw.Write(((ushort)nextBlock));
            }

            public void Enter(Z.Process p)
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

            public void B2(Z.Process p)
            {
                var stateStack = machine.stack;

                Machine.ReentrancyHelper callee = new Machine.ReentrancyHelper(application, machine, fun);
                p.Call(callee);
                StateImpl.IsCall = true;
                nextBlock = Blocks.B3;
            }

            public void B3(Z.Process p)
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

                transition = state.FindTransition(machine.currentEvent);
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
                var stateStack = machine.stack;
                stateStack.state = transition.to;
                state = stateStack.state;

                //goto enter;
                nextBlock = Blocks.B0;
            }
        }

        internal sealed class ReentrancyHelper : Z.ZingMethod
        {
            private static readonly short typeId = 3;

            private Z.StateImpl application;
            private Machine machine;
            private Fun fun;

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

            public ReentrancyHelper(Z.StateImpl app, Machine machine, Fun fun, PrtValue payload)
            {
                this.application = app;
                this.machine = machine;
                this.fun = fun;
                this.payload = payload;
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

            public override Z.ZingMethod Clone(Z.StateImpl application, Z.Process myProcess, bool shallowCopy)
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

            public override void WriteString(Z.StateImpl state, BinaryWriter bw)
            {
                bw.Write(typeId);
                bw.Write(((ushort)nextBlock));
            }

            public void Enter(Z.Process p)
            {
                machine.cont.Reset();
                fun.PushEventHandlerFrame(machine.cont, payload);

                nextBlock = Blocks.B0;
            }

            public void B0(Z.Process p)
            {
                fun.Execute(application, machine.cont);
                Machine.ProcessContinuation callee = new ProcessContinuation(application, machine);
                p.Call(callee);
                StateImpl.IsCall = true;

                nextBlock = Blocks.B1;
            }

            public void B1(Z.Process p)
            {
                var doPop = ((Machine.ProcessContinuation)p.LastFunctionCompleted).ReturnValue;
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

        internal sealed class ProcessContinuation : Z.ZingMethod
        {
            private static readonly short typeId = 4;

            private Z.StateImpl application;
            private Machine machine;

            // locals
            private Blocks nextBlock;

            // output
            private bool _ReturnValue;

            public bool ReturnValue
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
                    var status = machine.DequeueEvent(false);
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

    public abstract class Fun
    {
        public abstract void PushFunCallFrame(Continuation ctxt, params PrtValue[] args);

        public abstract void PushEventHandlerFrame(Continuation ctxt, PrtValue payload);

        public abstract void Execute(Z.StateImpl application, Continuation ctxt);
    }

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

    public delegate void Action(Z.StateImpl application, Continuation ctxt);

    public class Transition
    {
        public Event evt;
        public Fun fun; // isPush <==> fun == null
        public State to;

        public Transition(Event evt, Fun fun, State to)
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

    public class State
    {
        public State name;
        public Fun entryFun;
        public Fun exitFun;
        public Dictionary<Event, Transition> transitions;
        public Dictionary<Event, Fun> dos;
        public bool hasNullTransition;
        public StateTemperature temperature;
        public HashSet<Event> deferredSet;

        public static State Construct(State name, Fun entryFun, Fun exitFun, bool hasNullTransition, StateTemperature temperature)
        {
            State state = new State();
            state.name = name;
            state.entryFun = entryFun;
            state.exitFun = exitFun;
            state.transitions = new Dictionary<Event, Transition>();
            state.dos = new Dictionary<Event, Fun>();
            state.hasNullTransition = hasNullTransition;
            state.temperature = temperature;
            return state;
        }

        public Transition FindPushTransition(Event evt)
        {
            if (transitions.ContainsKey(evt))
            {
                Transition transition = transitions[evt];
                if (transition.fun == null)
                    return transition;
            }
            return null;
        }

        public Transition FindTransition(Event evt)
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

    public class EventBuffer
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

        public void DequeueEvent(Machine owner)
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

        public bool IsEnabled(Machine owner)
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
        public StateStack next;

        public Fun Find(Event f)
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
        public Machine id;
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

        void NewMachine(int ret, List<PrtValue> locals, Machine o)
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
