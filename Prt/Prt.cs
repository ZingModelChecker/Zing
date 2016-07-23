using Z = Microsoft.Zing;
using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;

//TODO: insert Trace and Print method calls everywhere (copy from generated .cs)
//Do it after Z.Application._PrtValue._Print is implemented
//TODO: check and correct ZingException reporting, especially inside non ZingClasses.
//.zing "static activate void Run() {" compiles into [Z.Activate] annotation in Main.Run
//TODO: check all offset refs and make sure we don't need those in the template code,
//or replace those with other mechanisms (i.e., argument, as in Start)
namespace Microsoft.Prt
{
    public abstract class Machine
    {
        public Machine(Z.StateImpl application)
        {
        }

        public static int id_myHandle = 0;
        public int myHandle;


        internal sealed class Start
            : Z.ZingMethod
        {
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
            Blocks nextBlock;
            Blocks handlerBlock;
            public Start(Z.StateImpl app)
            {
                application = app;
                nextBlock = Blocks.Enter;
                handlerBlock = Blocks.None;
                locals = new LocalVars(this);
                inputs = new InputVars(this);
            }
            private Z.StateImpl application;
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
            public sealed class LocalVars
            {
                private Z.ZingMethod stackFrame;
                internal LocalVars(Z.ZingMethod zm)
                {
                    stackFrame = zm;
                }
                public override UndoableStorage MakeInstance()
                {
                    return new LocalVars(stackFrame);
                }
                public override void CopyContents(UndoableStorage usSrc)
                {
                    LocalVars src = (usSrc as LocalVars);
                    if ((src == null))
                    {
                        throw new ArgumentException(@"expecting instance of LocalVars as source");
                    }
                }
                public override object GetValue(int fi)
                {
                    switch (fi)
                    {
                        default:
                            {
                                Debug.Assert(false);
                                return null;
                            }
                    }
                }
                public override void SetValue(int fi, object val)
                {
                    switch (fi)
                    {
                        default:
                            {
                                Debug.Assert(false);
                                return;
                            }
                    }
                }
                public void WriteString(StateImpl state, BinaryWriter bw)
                {
                }
            }
            public enum Blocks : ushort
            {
                None = 0,
                Enter = 1,
                B0 = 2,
            };
            public LocalVars locals;
            public override UndoableStorage Locals
            {
                get
                {
                    return locals;
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
            public override void WriteOutputsString(StateImpl state, BinaryWriter bw)
            {
                outputs.WriteString(state, bw);
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
            //TODO(offsets): which numbers should be in ZingSourceContext, if we need those?
            public override Z.ZingSourceContext Context
            {
                get
                {
                    switch (nextBlock)
                    {
                        default:
                            {
                                throw new ApplicationException();
                            }
                        case Blocks.B0:
                            {
                                //TODO
                                return new ZingSourceContext(0, 1057, 1058);
                            }
                        case Blocks.Enter:
                            {
                                //TODO
                                return new ZingSourceContext(0, 6786, 6813);
                            }
                    }
                }
            }
            public override Z.ZingAttribute ContextAttribute
            {
                get
                {
                    switch (nextBlock)
                    {
                        default:
                            {
                                return null;
                            }
                    }
                }
            }
            public override int CompareTo(object obj)
            {
                return 0;
            }
            private static readonly short typeId = 3;
            //Adding machine init state name to input vars in new compiler:
            //TODO: add machineName to inputs
            public sealed class InputVars
              : UndoableStorage
            {
                internal ZingMethod stackFrame;
                internal InputVars(ZingMethod zm)
                {
                    stackFrame = zm;
                }
                public override UndoableStorage MakeInstance()
                {
                    return new InputVars(stackFrame);
                }
                public override void CopyContents(UndoableStorage usSrc)
                {
                    InputVars src = (usSrc as InputVars);
                    if ((src == null))
                    {
                        throw new ArgumentException(@"expecting instance of InputVars as source");
                    }
                    this.priv_initState = src.priv_initState;
                }
                public override object GetValue(int fi)
                {
                    switch (fi)
                    {
                        default:
                            {
                                Debug.Assert(false);
                                return null;
                            }
                        case 0:
                            {
                                return priv_initState;
                            }
                    }
                }
                public override void SetValue(int fi, object val)
                {
                    switch (fi)
                    {
                        default:
                            {
                                Debug.Assert(false);
                                return;
                            }
                        case 0:
                            {
                                priv_initState = ((Z.Pointer)val);
                                return;
                            }
                    }
                }
                public void WriteString(StateImpl state, BinaryWriter bw)
                {
                    bw.Write(state.GetCanonicalId(this.priv_initState));
                }
                public void TraverseFields(FieldTraverser ft)
                {
                    ft.DoTraversal(this.priv_initState);
                }
                public Z.Pointer priv_initState;
                public Z.Pointer initState
                {
                    get
                    {
                        return priv_initState;
                    }
                    set
                    {
                        SetDirty();
                        priv_initState = value;
                    }
                }
            }
            public InputVars inputs;
            public override UndoableStorage Inputs
            {
                get
                {
                    return inputs;
                }
            }
            public override ZingMethod Clone(Z.StateImpl myState, Z.Process myProcess, bool shallowCopy)
            {
                Start clone = new Start(((Application)myState));
                clone.locals = new LocalVars(clone);
                clone.locals.CopyContents(locals);
                clone.inputs = new InputVars(clone);
                clone.inputs.CopyContents(inputs);
                clone.nextBlock = this.nextBlock;
                clone.handlerBlock = this.handlerBlock;
                if ((this.Caller != null))
                {
                    if (shallowCopy)
                    {
                        clone.Caller = null;
                    }
                    else
                    {
                        clone.Caller = this.Caller.Clone(myState, myProcess, false);
                    }
                }
                else
                {
                    if ((myProcess != null))
                    {
                        myProcess.EntryPoint = this;
                    }
                }
                return clone;
            }
            public override void WriteString(StateImpl state, BinaryWriter bw)
            {
                bw.Write(typeId);
                bw.Write(((ushort)nextBlock));
                inputs.WriteString(state, bw);
                locals.WriteString(state, bw);
            }
            public override void TraverseFields(FieldTraverser ft)
            {
                ft.DoTraversal(typeId);
                ft.DoTraversal(nextBlock);
                inputs.TraverseFields(ft);
                locals.TraverseFields(ft);
            }
            public Start(Application app, Z.Pointer initState)
            {
                application = app;
                nextBlock = Blocks.Enter;
                handlerBlock = Blocks.None;
                locals = new LocalVars(this);
                inputs = new InputVars(this);
                inputs.initState = initState;
            }
            public void Enter(Z.Process p)
            {
                Z.Application.Machine.Run callee = new Z.Application.Machine.Run(application);
                callee.inputs.priv_state = this.inputs.priv_initState;
                p.Call(callee);
                StateImpl.IsCall = true;

                nextBlock = Blocks.B0;
            }
            public void B0(Z.Process p)
            {
                p.LastFunctionCompleted = null;

                //aux vars:
                var machine = (Z.Application.Machine)application.LookupObject(This);
                var handle = (Z.Application.MachineHandle)application.LookupObject(machine.myHandle);
                var currentEvent = handle.currentEvent;
                var haltedSet = (ZingSet)application.LookupObject(application.globals.MachineHandle_halted);
                var enabledSet = (ZingSet)application.LookupObject(application.globals.MachineHandle_enabled);

                //Checking if currentEvent is halt:
                if (currentEvent = application.globals.Main_haltEvent)
                {
                    // myHandle.stack = null;
                    handle.stack = 0;
                    //myHandle.buffer = null;
                    handle.buffer = 0;
                    //myHandle.currentArg = null;
                    handle.currentArg = 0;
                    //MachineHandle.halted = (MachineHandle.halted + myHandle);
                    haltedSet.Add(handle);
                    //MachineHandle.enabled = (MachineHandle.enabled - myHandle);
                    enabledSet.Remove(handle);

                    //return;  
                    //TODO(offsets) (question)
                    p.Return(new ZingSourceContext(0, 892, 898), null);
                    StateImpl.IsReturn = true;
                }
                else
                {
                    //Unhandled event - report Zinger exception:
                    //TODO(offsets)
                    //For this Trace call, only machine name is needed; we can get it by passing the machine name
                    //to the Machine class constructor that has machine name as an argument
                    application.Trace(new ZingSourceContext(0, 903, 990), null, @"<StateLog> Unhandled event exception by machine Real1-{0}", handle.instance);
                    this.StateImpl.Exception = new Z.ZingAssertionFailureException(@"false", @"Unhandled event exception by machine <mach name>");
                    //return;  
                    //TODO(offsets)
                    p.Return(new ZingSourceContext(0, 1057, 1058), null);
                    StateImpl.IsReturn = true;
                }
            }
        }

        //Keeping Run as a ZingMethod.
        //Not P-program-dependent
        internal sealed class Run
            : Z.ZingMethod
        {
            public Run(Application app)
            {
                application = app;
                nextBlock = Blocks.Enter;
                handlerBlock = Blocks.None;
                locals = new LocalVars(this);
                inputs = new InputVars(this);
                outputs = new OutputVars(this);
            }
            private Application application;
            public override StateImpl StateImpl
            {
                get
                {
                    return ((StateImpl)application);
                }
                set
                {
                    application = ((Application)value);
                }
            }
            public sealed class LocalVars
              : UndoableStorage
            {
                private ZingMethod stackFrame;
                internal LocalVars(ZingMethod zm)
                {
                    stackFrame = zm;
                }
                public override UndoableStorage MakeInstance()
                {
                    return new LocalVars(stackFrame);
                }
                public override void CopyContents(UndoableStorage usSrc)
                {
                    LocalVars src = (usSrc as LocalVars);
                    if ((src == null))
                    {
                        throw new ArgumentException(@"expecting instance of LocalVars as source");
                    }
                    this.priv_doPop = src.priv_doPop;
                    this.priv_hasNullTransitionOrAction = src.priv_hasNullTransitionOrAction;
                }
                public override object GetValue(int fi)
                {
                    switch (fi)
                    {
                        default:
                            {
                                Debug.Assert(false);
                                return null;
                            }
                        case 0:
                            {
                                return priv_doPop;
                            }
                        case 1:
                            {
                                return priv_hasNullTransitionOrAction;
                            }
                    }
                }
                public override void SetValue(int fi, object val)
                {
                    switch (fi)
                    {
                        default:
                            {
                                Debug.Assert(false);
                                return;
                            }
                        case 0:
                            {
                                priv_doPop = ((Boolean)val);
                                return;
                            }
                        case 1:
                            {
                                priv_hasNullTransitionOrAction = ((Boolean)val);
                                return;
                            }
                    }
                }
                public void WriteString(StateImpl state, BinaryWriter bw)
                {
                    bw.Write(this.priv_doPop);
                    bw.Write(this.priv_hasNullTransitionOrAction);
                }
                public void TraverseFields(FieldTraverser ft)
                {
                    ft.DoTraversal(this.priv_doPop);
                    ft.DoTraversal(this.priv_hasNullTransitionOrAction);
                }
                public bool priv_doPop;
                public static int id_doPop = 0;
                public bool doPop
                {
                    get
                    {
                        return priv_doPop;
                    }
                    set
                    {
                        SetDirty();
                        priv_doPop = value;
                    }
                }
                public bool priv_hasNullTransitionOrAction;
                public static int id_hasNullTransitionOrAction = 1;
                public bool hasNullTransitionOrAction
                {
                    get
                    {
                        return priv_hasNullTransitionOrAction;
                    }
                    set
                    {
                        SetDirty();
                        priv_hasNullTransitionOrAction = value;
                    }
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
            public LocalVars locals;
            public override UndoableStorage Locals
            {
                get
                {
                    return locals;
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
                }
            }
            public override ulong GetRunnableJoinStatements(Process p)
            {
                switch (nextBlock)
                {
                    default:
                        {
                            return ~((ulong)0);
                        }
                }
            }
            public override bool IsAtomicEntryBlock()
            {
                switch (nextBlock)
                {
                    default:
                        {
                            return false;
                        }
                }
            }
            public override bool ValidEndState
            {
                get
                {
                    switch (nextBlock)
                    {
                        default:
                            {
                                return false;
                            }
                    }
                }
            }
            public override Z.ZingSourceContext Context
            {
                get
                {
                    switch (nextBlock)
                    {
                        default:
                            {
                                throw new ApplicationException();
                            }
                        case Blocks.B0:
                            {
                                //TODO(offsets)
                                return new ZingSourceContext(0, 1397, 1398);
                            }
                    }
                }
            }
            public override Z.ZingAttribute ContextAttribute
            {
                get
                {
                    switch (nextBlock)
                    {
                        default:
                            {
                                return null;
                            }
                    }
                }
            }
            public override int CompareTo(object obj)
            {
                return 0;
            }
            private static readonly short typeId = 4;
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
            public override void WriteOutputsString(StateImpl state, BinaryWriter bw)
            {
                outputs.WriteString(state, bw);
            }
            public sealed class InputVars
              : UndoableStorage
            {
                internal ZingMethod stackFrame;
                internal InputVars(ZingMethod zm)
                {
                    stackFrame = zm;
                }
                public override UndoableStorage MakeInstance()
                {
                    return new InputVars(stackFrame);
                }
                public override void CopyContents(UndoableStorage usSrc)
                {
                    InputVars src = (usSrc as InputVars);
                    if ((src == null))
                    {
                        throw new ArgumentException(@"expecting instance of InputVars as source");
                    }
                    this.priv_state = src.priv_state;
                }
                public override object GetValue(int fi)
                {
                    switch (fi)
                    {
                        default:
                            {
                                Debug.Assert(false);
                                return null;
                            }
                        case 0:
                            {
                                return priv_state;
                            }
                    }
                }
                public override void SetValue(int fi, object val)
                {
                    switch (fi)
                    {
                        default:
                            {
                                Debug.Assert(false);
                                return;
                            }
                        case 0:
                            {
                                priv_state = ((Z.Pointer)val);
                                return;
                            }
                    }
                }
                public void WriteString(StateImpl state, BinaryWriter bw)
                {
                    bw.Write(state.GetCanonicalId(this.priv_state));
                }
                public void TraverseFields(FieldTraverser ft)
                {
                    ft.DoTraversal(this.priv_state);
                }
                public Z.Pointer priv_state;
                public static int id_state = 0;
                public Z.Pointer state
                {
                    get
                    {
                        return priv_state;
                    }
                    set
                    {
                        SetDirty();
                        priv_state = value;
                    }
                }

            }
            public sealed class OutputVars
              : UndoableStorage
            {
                private ZingMethod stackFrame;
                internal OutputVars(ZingMethod zm)
                {
                    stackFrame = zm;
                }
                public override UndoableStorage MakeInstance()
                {
                    return new OutputVars(stackFrame);
                }
                public override void CopyContents(UndoableStorage usSrc)
                {
                    OutputVars src = (usSrc as OutputVars);
                    if ((src == null))
                    {
                        throw new ArgumentException(@"expecting instance of OutputVars as source");
                    }
                }
                public override object GetValue(int fi)
                {
                    switch (fi)
                    {
                        default:
                            {
                                Debug.Assert(false);
                                return null;
                            }
                    }
                }
                public override void SetValue(int fi, object val)
                {
                    switch (fi)
                    {
                        default:
                            {
                                Debug.Assert(false);
                                return;
                            }
                    }
                }
                public void WriteString(StateImpl state, BinaryWriter bw) { }
                public void TraverseFields(FieldTraverser ft) { }
            }
            public InputVars inputs;
            public OutputVars outputs;
            public override UndoableStorage Inputs
            {
                get
                {
                    return inputs;
                }
            }
            public override UndoableStorage Outputs
            {
                get
                {
                    return outputs;
                }
            }
            public override ZingMethod Clone(Z.StateImpl myState, Z.Process myProcess, bool shallowCopy)
            {
                Run clone = new Run(((Application)myState));
                clone.locals = new LocalVars(clone);
                clone.locals.CopyContents(locals);
                clone.inputs = new InputVars(clone);
                clone.inputs.CopyContents(inputs);
                clone.outputs = new OutputVars(clone);
                clone.outputs.CopyContents(outputs);
                clone.nextBlock = this.nextBlock;
                clone.handlerBlock = this.handlerBlock;
                clone.SavedAtomicityLevel = this.SavedAtomicityLevel;
                clone.privThis = this.privThis;
                if ((this.Caller != null))
                {
                    if (shallowCopy)
                    {
                        clone.Caller = null;
                    }
                    else
                    {
                        clone.Caller = this.Caller.Clone(myState, myProcess, false);
                    }
                }
                else
                {
                    if ((myProcess != null))
                    {
                        myProcess.EntryPoint = this;
                    }
                }
                return clone;
            }
            public override void WriteString(StateImpl state, BinaryWriter bw)
            {
                bw.Write(typeId);
                bw.Write(((ushort)nextBlock));
                bw.Write(state.GetCanonicalId(privThis));
                inputs.WriteString(state, bw);
                outputs.WriteString(state, bw);
                locals.WriteString(state, bw);
            }
            public override void TraverseFields(FieldTraverser ft)
            {
                ft.DoTraversal(typeId);
                ft.DoTraversal(nextBlock);
                ft.DoTraversal(privThis);
                inputs.TraverseFields(ft);
                outputs.TraverseFields(ft);
                locals.TraverseFields(ft);
            }
            //TODO: why no "void" in old compiler?
            public void Run(Application app, Pointer This, Z.Pointer state)
            {

                application = app;
                nextBlock = Blocks.Enter;
                handlerBlock = Blocks.None;
                locals = new LocalVars(this);
                inputs = new InputVars(this);
                outputs = new OutputVars(this);
                this.This = This;
                inputs.state = state;
            }
            public void B5(Z.Process p)
            {
                var machine = (Z.Application.Machine)application.LookupObject(This);
                var handle = (Z.Application.MachineHandle)application.LookupObject(machine.myHandle);
                //After "while" loop:
                //myHandle.Pop();
                //This is not a ZingMethod call in the new compiler:
                handle.Pop();

                //Return from Run:
                //TODO(offsets)
                p.Return(new ZingSourceContext(0, 1397, 1398), null);
                StateImpl.IsReturn = true;
            }
            public void B4(Z.Process p)
            {
                var machine = (Z.Application.Machine)application.LookupObject(This);
                //Return from RunHelper:
                locals.doPop = (((machine.RunHelper)p.LastFunctionCompleted)).outputs._Lfc_ReturnValue;
                p.LastFunctionCompleted = null;

                //B1 is header of the "while" loop:
                nextBlock = Blocks.B1;
            }
            public void B3(Z.Process p)
            {
                var machine = (Z.Application.Machine)application.LookupObject(This);
                //Return from DequeueEvent:
                p.LastFunctionCompleted = null;

                //doPop = RunHelper(false);
                //Calling ZingMethod RunHelper:
                Z.Application.Machine.RunHelper callee = new Z.Application.Machine.RunHelper(application);
                callee.inputs.priv_start = false;
                callee.This = This;
                p.Call(callee);
                StateImpl.IsCall = true;

                nextBlock = Blocks.B4;

                //locals.doPop = machine.RunHelper(application, false);
            }
            public void B2(Z.Process p)
            {
                var machine = (Z.Application.Machine)application.LookupObject(This);
                var handle = (Z.Application.MachineHandle)application.LookupObject(machine.myHandle);
                //body of the "while" loop before DequeueEvent call:
                //hasNullTransitionOrAction = myHandle.stack.HasNullTransitionOrAction();
                //Calling regular C# method
                locals.hasNullTransitionOrAction = stateStack.HasNullTransitionOrAction(application);

                //myHandle.DequeueEvent(hasNullTransitionOrAction);
                //(TODO: check this code after implementing DequeueEvent) 
                //Calling ZingMethod:
                handle.DequeueEvent callee = new handle.DequeueEvent(application);
                callee.inputs.priv_hasNullTransition = locals.hasNullTransitionOrAction;
                callee.This = handle;
                p.Call(callee);
                StateImpl.IsCall = true;

                nextBlock = Blocks.B3;
            }
            public void B1(Z.Process p)
            {
                //while (!doPop) {
                if (!locals.doPop)
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
                locals.doPop = (((machine.RunHelper)p.LastFunctionCompleted)).outputs._Lfc_ReturnValue;
                p.LastFunctionCompleted = null;
                nextBlock = Blocks.B1;
            }
            public void Enter(Z.Process p)
            {
                var machine = (Z.Application.Machine)application.LookupObject(This);
                var handle = (Z.Application.MachineHandle)application.LookupObject(machine.myHandle);
                var stateStack = (Z.Application.StateStack)application.LookupObject(handle.stack);

                //myHandle.Push();
                //This is not a ZingMethod call in the new compiler:
                handle.Push(application);

                //myHandle.stack.state = state;
                stateStack.state = inputs.state;

                //doPop = RunHelper(true);
                //Calling ZingMethod RunHelper:
                Z.Application.Machine.RunHelper callee = new Z.Application.Machine.RunHelper(application);
                callee.inputs.priv_start = true;
                callee.This = This;
                p.Call(callee);
                StateImpl.IsCall = true;

                nextBlock = Blocks.B0;
            }
        }

        //public virtual void CalculateDeferredAndActionSet(Application app, Pointer This, Z.Pointer state) 
        //moved to the derived class Machine_<machine name>

        //RunHelper is not P program-dependent if no liveness
        internal sealed class RunHelper
                : Z.ZingMethod
        {
            public RunHelper(Application app)
            {
                application = app;
                nextBlock = Blocks.Enter;
                handlerBlock = Blocks.None;
                locals = new LocalVars(this);
                inputs = new InputVars(this);
                outputs = new OutputVars(this);
            }
            private Application application;
            public override StateImpl StateImpl
            {
                get
                {
                    return ((StateImpl)application);
                }
                set
                {
                    application = ((Application)value);
                }
            }
            public sealed class LocalVars
              : UndoableStorage
            {
                private ZingMethod stackFrame;
                internal LocalVars(ZingMethod zm)
                {
                    stackFrame = zm;
                }
                public override UndoableStorage MakeInstance()
                {
                    return new LocalVars(stackFrame);
                }
                public override void CopyContents(UndoableStorage usSrc)
                {
                    LocalVars src = (usSrc as LocalVars);
                    if ((src == null))
                    {
                        throw new ArgumentException(@"expecting instance of LocalVars as source");
                    }
                    this.priv_state = src.priv_state;
                    this.priv_transition = src.priv_transition;
                    this.priv_actionFun = src.priv_actionFun;
                }
                public override object GetValue(int fi)
                {
                    switch (fi)
                    {
                        default:
                            {
                                Debug.Assert(false);
                                return null;
                            }
                        case 0:
                            {
                                return priv_state;
                            }
                        case 1:
                            {
                                return priv_transition;
                            }
                        case 2:
                            {
                                return priv_actionFun;
                            }
                    }
                }
                public override void SetValue(int fi, object val)
                {
                    switch (fi)
                    {
                        default:
                            {
                                Debug.Assert(false);
                                return;
                            }
                        case 0:
                            {
                                priv_state = ((Z.Pointer)val);
                                return;
                            }
                        case 1:
                            {
                                priv_transition = ((Z.Pointer)val);
                                return;
                            }
                        case 2:
                            {
                                priv_actionFun = ((Application.ActionOrFun)val);
                                return;
                            }
                    }
                }
                public void WriteString(StateImpl state, BinaryWriter bw)
                {
                    bw.Write(state.GetCanonicalId(this.priv_state));
                    bw.Write(state.GetCanonicalId(this.priv_transition));
                    bw.Write(((byte)this.priv_actionFun));
                }
                public void TraverseFields(FieldTraverser ft)
                {
                    ft.DoTraversal(this.priv_state);
                    ft.DoTraversal(this.priv_transition);
                    ft.DoTraversal(this.priv_actionFun);
                }
                public Z.Pointer priv_state;
                public static int id_state = 0;
                public Z.Pointer state
                {
                    get
                    {
                        return priv_state;
                    }
                    set
                    {
                        SetDirty();
                        priv_state = value;
                    }
                }
                public Z.Pointer priv_transition;
                public static int id_transition = 1;
                public Z.Pointer transition
                {
                    get
                    {
                        return priv_transition;
                    }
                    set
                    {
                        SetDirty();
                        priv_transition = value;
                    }
                }
                public Application.ActionOrFun priv_actionFun;
                public static int id_actionFun = 2;
                public Application.ActionOrFun actionFun
                {
                    get
                    {
                        return priv_actionFun;
                    }
                    set
                    {
                        SetDirty();
                        priv_actionFun = value;
                    }
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
            public LocalVars locals;
            public override UndoableStorage Locals
            {
                get
                {
                    return locals;
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
            public override void WriteOutputsString(StateImpl state, BinaryWriter bw)
            {
                outputs.WriteString(state, bw);
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
                            B5(p);
                            break;
                        }
                    case Blocks.B8:
                        {
                            B6(p);
                            break;
                        }
                }
            }
            public override ulong GetRunnableJoinStatements(Process p)
            {
                switch (nextBlock)
                {
                    default:
                        {
                            return ~((ulong)0);
                        }
                }
            }
            public override bool IsAtomicEntryBlock()
            {
                switch (nextBlock)
                {
                    default:
                        {
                            return false;
                        }
                }
            }
            public override bool ValidEndState
            {
                get
                {
                    switch (nextBlock)
                    {
                        default:
                            {
                                return false;
                            }
                    }
                }
            }
            //TODO(offsets)
            public override Z.ZingSourceContext Context
            {
                get
                {
                    switch (nextBlock)
                    {
                        default:
                            {
                                throw new ApplicationException();
                            }
                        case Blocks.B0:
                            {
                                return new ZingSourceContext(0, 3315, 3316);
                            }
                        case Blocks.B1:
                            {
                                return new ZingSourceContext(0, 3315, 3316);
                            }
                        case Blocks.B2:
                            {
                                return new ZingSourceContext(0, 3300, 3311);
                            }
                        case Blocks.B3:
                            {
                                return new ZingSourceContext(0, 3300, 3311);
                            }
                        case Blocks.B4:
                            {
                                return new ZingSourceContext(0, 3270, 3299);
                            }
                        case Blocks.B5:
                            {
                                return new ZingSourceContext(0, 3232, 3269);
                            }
                        case Blocks.B6:
                            {
                                return new ZingSourceContext(0, 3232, 3269);
                            }
                        case Blocks.B6:
                            {
                                return new ZingSourceContext(0, 3232, 3269);
                            }
                        case Blocks.B6:
                            {
                                return new ZingSourceContext(0, 3232, 3269);
                            }
                        case Blocks.Enter:
                            {
                                return new ZingSourceContext(0, 6786, 6813);
                            }
                    }
                }
            }
            public override Z.ZingAttribute ContextAttribute
            {
                get
                {
                    switch (nextBlock)
                    {
                        default:
                            {
                                return null;
                            }
                    }
                }
            }
            public override int CompareTo(object obj)
            {
                return 0;
            }
            private static readonly short typeId = 8;
            public Z.Pointer privThis;
            public override Z.Pointer This
            {
                get
                {
                    return privThis;
                }
                set
                {
                    privThis = value;
                }
            }
            public sealed class InputVars
              : UndoableStorage
            {
                internal ZingMethod stackFrame;
                internal InputVars(ZingMethod zm)
                {
                    stackFrame = zm;
                }
                public override UndoableStorage MakeInstance()
                {
                    return new InputVars(stackFrame);
                }
                public override void CopyContents(UndoableStorage usSrc)
                {
                    InputVars src = (usSrc as InputVars);
                    if ((src == null))
                    {
                        throw new ArgumentException(@"expecting instance of InputVars as source");
                    }
                    this.priv_start = src.priv_start;
                }
                public override object GetValue(int fi)
                {
                    switch (fi)
                    {
                        default:
                            {
                                Debug.Assert(false);
                                return null;
                            }
                        case 0:
                            {
                                return priv_start;
                            }
                    }
                }
                public override void SetValue(int fi, object val)
                {
                    switch (fi)
                    {
                        default:
                            {
                                Debug.Assert(false);
                                return;
                            }
                        case 0:
                            {
                                priv_start = ((Boolean)val);
                                return;
                            }
                    }
                }
                public void WriteString(StateImpl state, BinaryWriter bw)
                {
                    bw.Write(this.priv_start);
                }
                public void TraverseFields(FieldTraverser ft)
                {
                    ft.DoTraversal(this.priv_start);
                }
                public bool priv_start;
                public static int id_start = 0;
                public bool start
                {
                    get
                    {
                        return priv_start;
                    }
                    set
                    {
                        SetDirty();
                        priv_start = value;
                    }
                }
            }
            public sealed class OutputVars
              : UndoableStorage
            {
                private ZingMethod stackFrame;
                internal OutputVars(ZingMethod zm)
                {
                    stackFrame = zm;
                }
                public override UndoableStorage MakeInstance()
                {
                    return new OutputVars(stackFrame);
                }
                public override void CopyContents(UndoableStorage usSrc)
                {
                    OutputVars src = (usSrc as OutputVars);
                    if ((src == null))
                    {
                        throw new ArgumentException(@"expecting instance of OutputVars as source");
                    }
                    this.priv_ReturnValue = src.priv_ReturnValue;
                }
                public override object GetValue(int fi)
                {
                    switch (fi)
                    {
                        default:
                            {
                                Debug.Assert(false);
                                return null;
                            }
                        case 1:
                            {
                                return priv_ReturnValue;
                            }
                    }
                }
                public override void SetValue(int fi, object val)
                {
                    switch (fi)
                    {
                        default:
                            {
                                Debug.Assert(false);
                                return;
                            }
                        case 1:
                            {
                                priv_ReturnValue = ((Boolean)val);
                                return;
                            }
                    }
                }
                public void WriteString(StateImpl state, BinaryWriter bw)
                {
                    bw.Write(this.priv_ReturnValue);
                }
                public void TraverseFields(FieldTraverser ft)
                {
                    ft.DoTraversal(this.priv_ReturnValue);
                }
                public bool priv_ReturnValue;
                public static int id_ReturnValue = 1;
                public bool _ReturnValue
                {
                    get
                    {
                        return priv_ReturnValue;
                    }
                    set
                    {
                        SetDirty();
                        priv_ReturnValue = value;
                    }
                }
                public bool _Lfc_ReturnValue
                {
                    get
                    {
                        return priv_ReturnValue;
                    }
                }
            }
            public InputVars inputs;
            public OutputVars outputs;
            public override UndoableStorage Inputs
            {
                get
                {
                    return inputs;
                }
            }
            public override UndoableStorage Outputs
            {
                get
                {
                    return outputs;
                }
            }
            public override ZingMethod Clone(Z.StateImpl myState, Z.Process myProcess, bool shallowCopy)
            {
                RunHelper clone = new RunHelper(((Application)myState));
                clone.locals = new LocalVars(clone);
                clone.locals.CopyContents(locals);
                clone.inputs = new InputVars(clone);
                clone.inputs.CopyContents(inputs);
                clone.outputs = new OutputVars(clone);
                clone.outputs.CopyContents(outputs);
                clone.nextBlock = this.nextBlock;
                clone.handlerBlock = this.handlerBlock;
                clone.SavedAtomicityLevel = this.SavedAtomicityLevel;
                clone.privThis = this.privThis;
                if ((this.Caller != null))
                {
                    if (shallowCopy)
                    {
                        clone.Caller = null;
                    }
                    else
                    {
                        clone.Caller = this.Caller.Clone(myState, myProcess, false);
                    }
                }
                else
                {
                    if ((myProcess != null))
                    {
                        myProcess.EntryPoint = this;
                    }
                }
                return clone;
            }
            public override void WriteString(StateImpl state, BinaryWriter bw)
            {
                bw.Write(typeId);
                bw.Write(((ushort)nextBlock));
                bw.Write(state.GetCanonicalId(privThis));
                inputs.WriteString(state, bw);
                outputs.WriteString(state, bw);
                locals.WriteString(state, bw);
            }
            public override void TraverseFields(FieldTraverser ft)
            {
                ft.DoTraversal(typeId);
                ft.DoTraversal(nextBlock);
                ft.DoTraversal(privThis);
                inputs.TraverseFields(ft);
                outputs.TraverseFields(ft);
                locals.TraverseFields(ft);
            }
            public RunHelper(Application app, Pointer This, bool start)
            {
                application = app;
                nextBlock = Blocks.Enter;
                handlerBlock = Blocks.None;
                locals = new LocalVars(this);
                inputs = new InputVars(this);
                outputs = new OutputVars(this);

                this.This = This;
                inputs.start = start;
            }
            public override bool BooleanReturnValue
            {
                get
                {
                    return outputs._ReturnValue;
                }
            }
            public void Enter(Z.Process p)
            {
                var machine = (Z.Application.Machine)application.LookupObject(This);
                var handle = (Z.Application.MachineHandle)application.LookupObject(machine.myHandle);
                var stateStack = (Z.Application.StateStack)application.LookupObject(handle.stack);
                var state = (Z.Application.State)stateStack.state;

                //init:
                //state = myHandle.stack.state;
                locals.state = state;
                if (inputs.start)
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
                var machine = (Z.Application.Machine)application.LookupObject(This);
                var handle = (Z.Application.MachineHandle)application.LookupObject(machine.myHandle);
                var stateStack = (Z.Application.StateStack)application.LookupObject(handle.stack);
                var state = (Z.Application.State)stateStack.state;

                //enter:
                //CalculateDeferredAndActionSet(state);
                machine.CalculateDeferredAndActionSet(application, state);

                //actionFun = state.entryFun;
                var localState = (Z.Application.State)application.LookupObject(locals.state);
                locals.actionFun = localState.entryFun;
                nextBlock = Blocks.B2;
            }
            public void B2(Z.Process p)
            {
                var machine = (Z.Application.Machine)application.LookupObject(This);
                var handle = (Z.Application.MachineHandle)application.LookupObject(machine.myHandle);
                var stateStack = (Z.Application.StateStack)application.LookupObject(handle.stack);

                //execute: ReentrancyHelper(actionFun);
                //Calling ZingMethod ReentrancyHelper:
                machine.ReentrancyHelper callee = new machine.ReentrancyHelper(application);
                callee.inputs.priv_actionFun = locals.actionFun;
                callee.This = This;
                p.Call(callee);
                StateImpl.IsCall = true;
                nextBlock = Blocks.B3;
            }
            public void B3(Z.Process p)
            {
                p.LastFunctionCompleted = null;

                var machine = (Z.Application.Machine)application.LookupObject(This);
                var handle = (Z.Application.MachineHandle)application.LookupObject(machine.myHandle);
                var reason = ((Z.Application.Continuation)handle.cont).reason;
                //if ((myHandle.cont.reason == ContinuationReason.Raise))
                if (reason == (ContinuationReason)3)
                {
                    //goto handle;
                    nextBlock = Blocks.B1;
                }
                else
                {
                    //myHandle.currentEvent = null;
                    handle.currentEvent = 0;

                    //myHandle.currentArg = PrtValue.PrtMkDefaultValue(Main.type_0_PrtType);
                    //Main_type_0_PrtType P is not program dependent
                    handle.currentArg = application.PrtValue.PrtMkDefaultValue(application, application.globals.Main_type_0_PrtType);
                    //if ((myHandle.cont.reason != ContinuationReason.Pop))
                    if (reason != (ContinuationReason)2)
                    {
                        //return false;
                        outputs._ReturnValue = false;
                        //TODO(offsets)
                        p.Return(new ZingSourceContext(0, 2619, 2631), null);
                        StateImpl.IsReturn = true;
                    }
                    else
                    {
                        var localState = (Z.Application.State)application.LookupObject(locals.state);
                        //ReentrancyHelper(state.exitFun);
                        Z.Application.Machine.ReentrancyHelper callee = new Z.Application.Machine.ReentrancyHelper(application);
                        callee.inputs.priv_actionFun = localState.exitFun;
                        callee.This = This;
                        p.Call(callee);
                        StateImpl.IsCall = true;

                        nextBlock = Blocks.B4;

                    }
                }
            }
            public void B4(Z.Process p)
            {
                p.LastFunctionCompleted = null;

                //return true;
                outputs._ReturnValue = true;
                //TODO(offsets)
                p.Return(new ZingSourceContext(0, 2692, 2703), null);
                StateImpl.IsReturn = true;
            }
            public void B1(Z.Process p)
            {
                var machine = (Z.Application.Machine)application.LookupObject(This);
                var handle = (Z.Application.MachineHandle)application.LookupObject(machine.myHandle);
                var stateStack = (Z.Application.StateStack)application.LookupObject(handle.stack);
                var state = (Z.Application.State)stateStack.state;
                var actionSet = (ZingSet)stateStack.actionSet;

                //handle:
                //if ((myHandle.currentEvent in myHandle.stack.actionSet))
                if (actionSet.IsMember(handle.currentEvent))
                {
                    //actionFun = myHandle.stack.Find(myHandle.currentEvent);
                    locals.actionFun = stateStack.Find(handle.currentEvent);
                    //goto execute;
                    nextBlock = Blocks.B2;
                }
                else
                {
                    //transition = state.FindPushTransition(myHandle.currentEvent);
                    locals.transition = state.FindPushTransition(handle.currentEvent);
                    //if ((transition != null)) {
                    if ((locals.transition != 0))
                    {
                        //Run(transition.to);
                        Z.Application.Machine.Run callee = new Z.Application.Machine.Run(application);
                        var localTransition = (Z.Application.Transition)application.LookupObject(locals.transition);
                        callee.inputs.priv_state = localTransition.to;
                        callee.This = This;
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

                var machine = (Z.Application.Machine)application.LookupObject(This);
                var handle = (Z.Application.MachineHandle)application.LookupObject(machine.myHandle);

                //if (myHandle.currentEvent = null) {return false;}
                if (handle.currentEvent = 0)
                {
                    outputs._ReturnValue = false;
                    //TODO(offsets)
                    p.Return(new ZingSourceContext(0, 2619, 2631), null);
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
                //ReentrancyHelper(state.exitFun);
                Z.Application.Machine.ReentrancyHelper callee = new Z.Application.Machine.ReentrancyHelper(application);
                callee.inputs.priv_actionFun = localState.exitFun;
                callee.This = This;
                p.Call(callee);
                StateImpl.IsCall = true;
                nextBlock = Blocks.B7;
            }
            public void B7(Z.Process p)
            {
                p.LastFunctionCompleted = null;

                //transition = state.FindTransition(myHandle.currentEvent);
                locals.transition = state.FindTransition(handle.currentEvent);
                //if ((transition == null)) { return true; }
                if ((locals.transition == 0))
                {
                    outputs._ReturnValue = true;
                    //TODO(offsets)
                    p.Return(new ZingSourceContext(0, 2619, 2631), null);
                    StateImpl.IsReturn = true;
                }
                else
                {
                    //ReentrancyHelper(transition.fun);
                    Z.Application.Machine.ReentrancyHelper callee = new Z.Application.Machine.ReentrancyHelper(application);
                    callee.inputs.priv_actionFun = locals.transition.fun;
                    callee.This = This;
                    p.Call(callee);
                    StateImpl.IsCall = true;
                    nextBlock = Blocks.B8;
                }
            }
            public void B8(Z.Process p)
            {
                p.LastFunctionCompleted = null;
                var machine = (Z.Application.Machine)application.LookupObject(This);
                var handle = (Z.Application.MachineHandle)application.LookupObject(machine.myHandle);
                var stateStack = (Z.Application.StateStack)application.LookupObject(handle.stack);
                var localTransition = (Z.Application.Transition)application.LookupObject(locals.transition);
                //myHandle.stack.state = transition.to;
                stateStack = localTransition.to;

                //state = myHandle.stack.state;
                locals.state = stateStack.state;

                //goto enter;
                nextBlock = Blocks.B0;
            }
        }

        //Not P program dependent:
        internal sealed class ProcessContinuation
            : Z.ZingMethod
        {
            public ProcessContinuation(Application app)
            {
                application = app;
                nextBlock = Blocks.Enter;
                handlerBlock = Blocks.None;
                locals = new LocalVars(this);
                inputs = new InputVars(this);
                outputs = new OutputVars(this);
            }
            private Application application;
            public override StateImpl StateImpl
            {
                get
                {
                    return ((StateImpl)application);
                }
                set
                {
                    application = ((Application)value);
                }
            }
            public sealed class LocalVars
              : UndoableStorage
            {
                private ZingMethod stackFrame;
                internal LocalVars(ZingMethod zm)
                {

                    stackFrame = zm;
                }
                public override UndoableStorage MakeInstance()
                {
                    return new LocalVars(stackFrame);
                }
                public override void CopyContents(UndoableStorage usSrc)
                {
                    LocalVars src = (usSrc as LocalVars);
                    if ((src == null))
                    {
                        throw new ArgumentException(@"expecting instance of LocalVars as source");
                    }
                    this.priv_doPop = src.priv_doPop;
                }
                public override object GetValue(int fi)
                {
                    switch (fi)
                    {
                        default:
                            {
                                Debug.Assert(false);
                                return null;
                            }
                        case 0:
                            {
                                return priv_doPop;
                            }
                    }
                }
                public override void SetValue(int fi, object val)
                {
                    switch (fi)
                    {
                        default:
                            {
                                Debug.Assert(false);
                                return;
                            }
                        case 0:
                            {
                                priv_doPop = ((Boolean)val);
                                return;
                            }
                    }
                }
                public void WriteString(StateImpl state, BinaryWriter bw)
                {
                    bw.Write(this.priv_doPop);
                }
                public void TraverseFields(FieldTraverser ft)
                {
                    ft.DoTraversal(this.priv_doPop);
                }
                public bool priv_doPop;
                public static int id_doPop = 0;
                public bool doPop
                {
                    get
                    {
                        return priv_doPop;
                    }
                    set
                    {
                        SetDirty();
                        priv_doPop = value;
                    }
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
            public LocalVars locals;
            public override UndoableStorage Locals
            {
                get
                {
                    return locals;
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
            public override void WriteOutputsString(StateImpl state, BinaryWriter bw)
            {
                outputs.WriteString(state, bw);
            }
            public override string ProgramCounter
            {
                get
                {
                    return nextBlock.ToString();
                }
            }
            private Blocks nextBlock
            {
                get
                {
                    return privNextBlock;
                }
                set
                {
                    if (((othersULE != null) && !othersULE.nextBlockChanged))
                    {
                        othersULE.nextBlockChanged = true;
                        othersULE.savedNextBlock = privNextBlock;
                    }
                    privNextBlock = value;
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
            public override ulong GetRunnableJoinStatements(Process p)
            {
                switch (nextBlock)
                {
                    default:
                        {
                            return ~((ulong)0);
                        }
                }
            }
            public override bool IsAtomicEntryBlock()
            {
                switch (nextBlock)
                {
                    default:
                        {
                            return false;
                        }
                }
            }
            public override bool ValidEndState
            {
                get
                {
                    switch (nextBlock)
                    {
                        default:
                            {
                                return false;
                            }
                    }
                }
            }
            //TODO
            public override Z.ZingSourceContext Context
            {
                get
                {
                    switch (nextBlock)
                    {
                        default:
                            {
                                throw new ApplicationException();
                            }
                        case Blocks.B0:
                            {
                                return new ZingSourceContext(0, 3978, 3979);
                            }
                        case Blocks.B1:
                            {
                                return new ZingSourceContext(0, 3978, 3979);
                            }
                        case Blocks.B2:
                            {
                                return new ZingSourceContext(0, 3957, 3969);
                            }
                        case Blocks.Enter:
                            {
                                return new ZingSourceContext(0, 6786, 6813);
                            }
                    }
                }
            }
            public override Z.ZingAttribute ContextAttribute
            {
                get
                {
                    switch (nextBlock)
                    {
                        default:
                            {
                                return null;
                            }
                    }
                }
            }
            public override int CompareTo(object obj)
            {
                return 0;
            }
            private static readonly short typeId = 9;
            public sealed class InputVars
              : UndoableStorage
            {
                internal ZingMethod stackFrame;
                internal InputVars(ZingMethod zm)
                {

                    {
                        ;
                    }
                    {
                        stackFrame = zm;
                    }
                }
                public override UndoableStorage MakeInstance()
                {
                    return new InputVars(stackFrame);
                }
                public override void CopyContents(UndoableStorage usSrc)
                {
                    InputVars src = (usSrc as InputVars);
                    if ((src == null))
                    {
                        throw new ArgumentException(@"expecting instance of InputVars as source");
                    }
                }
                public override object GetValue(int fi)
                {
                    switch (fi)
                    {
                        default:
                            {
                                {
                                    Debug.Assert(false);
                                    return null;
                                }
                            }
                    }
                }
                public override void SetValue(int fi, object val)
                {
                    switch (fi)
                    {
                        default:
                            {
                                Debug.Assert(false);
                                return;
                            }
                    }
                }
                public void WriteString(StateImpl state, BinaryWriter bw)
                {
                }
                public void TraverseFields(FieldTraverser ft)
                {
                }

            }
            public sealed class OutputVars
              : UndoableStorage
            {
                private ZingMethod stackFrame;
                internal OutputVars(ZingMethod zm)
                {
                    stackFrame = zm;
                }
                public override UndoableStorage MakeInstance()
                {
                    return new OutputVars(stackFrame);
                }
                public override void CopyContents(UndoableStorage usSrc)
                {
                    OutputVars src = (usSrc as OutputVars);
                    if ((src == null))
                    {
                        throw new ArgumentException(@"expecting instance of OutputVars as source");
                    }
                    this.priv_ReturnValue = src.priv_ReturnValue;
                }
                public override object GetValue(int fi)
                {
                    switch (fi)
                    {
                        default:
                            {
                                Debug.Assert(false);
                                return null;
                            }
                        case 0:
                            {
                                return priv_ReturnValue;
                            }
                    }
                }
                public override void SetValue(int fi, object val)
                {
                    switch (fi)
                    {
                        default:
                            {
                                Debug.Assert(false);
                                return;
                            }
                        case 0:
                            {
                                priv_ReturnValue = ((Boolean)val);
                                return;
                            }
                    }
                }
                public void WriteString(StateImpl state, BinaryWriter bw)
                {
                    bw.Write(this.priv_ReturnValue);
                }
                public void TraverseFields(FieldTraverser ft)
                {
                    ft.DoTraversal(this.priv_ReturnValue);
                }
                public bool priv_ReturnValue;
                public static int id_ReturnValue = 0;
                public bool _ReturnValue
                {
                    get
                    {
                        return priv_ReturnValue;
                    }
                    set
                    {
                        SetDirty();
                        priv_ReturnValue = value;
                    }
                }
                public bool _Lfc_ReturnValue
                {
                    get
                    {
                        return priv_ReturnValue;
                    }
                }
            }
            public InputVars inputs;
            public OutputVars outputs;
            public override UndoableStorage Inputs
            {
                get
                {
                    return inputs;
                }
            }
            public override UndoableStorage Outputs
            {
                get
                {
                    return outputs;
                }
            }
            public override ZingMethod Clone(Z.StateImpl myState, Z.Process myProcess, bool shallowCopy)
            {
                ProcessContinuation clone = new ProcessContinuation(((Application)myState));
                clone.locals = new LocalVars(clone);
                clone.locals.CopyContents(locals);
                clone.inputs = new InputVars(clone);
                clone.inputs.CopyContents(inputs);
                clone.outputs = new OutputVars(clone);
                clone.outputs.CopyContents(outputs);
                clone.nextBlock = this.nextBlock;
                clone.handlerBlock = this.handlerBlock;
                clone.SavedAtomicityLevel = this.SavedAtomicityLevel;
                clone.privThis = this.privThis;
                if ((this.Caller != null))
                {
                    if (shallowCopy)
                    {
                        clone.Caller = null;
                    }
                    else
                    {
                        clone.Caller = this.Caller.Clone(myState, myProcess, false);
                    }
                }
                else
                {
                    if ((myProcess != null))
                    {
                        myProcess.EntryPoint = this;
                    }
                }
                return clone;
            }
            public override void WriteString(StateImpl state, BinaryWriter bw)
            {
                bw.Write(typeId);
                bw.Write(((ushort)nextBlock));
                bw.Write(state.GetCanonicalId(privThis));
                inputs.WriteString(state, bw);
                outputs.WriteString(state, bw);
                locals.WriteString(state, bw);
            }
            public override void TraverseFields(FieldTraverser ft)
            {
                ft.DoTraversal(typeId);
                ft.DoTraversal(nextBlock);
                ft.DoTraversal(privThis);
                inputs.TraverseFields(ft);
                outputs.TraverseFields(ft);
                locals.TraverseFields(ft);
            }
            public ProcessContinuation(Application app, Pointer This)
            {
                application = app;
                nextBlock = Blocks.Enter;
                handlerBlock = Blocks.None;
                locals = new LocalVars(this);
                inputs = new InputVars(this);
                outputs = new OutputVars(this);
                this.This = This;
            }
            public override bool BooleanReturnValue
            {
                get
                {
                    return outputs._ReturnValue;
                }
            }
            public void Enter(Z.Process p)
            {
                var machine = (Z.Application.Machine)application.LookupObject(This);
                var handle = (Z.Application.MachineHandle)application.LookupObject(machine.myHandle);
                var cont = (Z.Application.Continuation)handle.cont;
                var reason = cont.reason;
                //if ((myHandle.cont.reason == ContinuationReason.Raise))
                if (reason == (ContinuationReason)0)     //ContinuationReason.Return
                {
                    //return true;
                    outputs._ReturnValue = true;
                    //TODO 
                    p.Return(new ZingSourceContext(0, 3424, 3435), null);
                    StateImpl.IsReturn = true;
                }
                if (reason == (ContinuationReason)2)   //ContinuationReason.Pop
                {
                    //return true;
                    outputs._ReturnValue = true;
                    //TODO 
                    p.Return(new ZingSourceContext(0, 3424, 3435), null);
                    StateImpl.IsReturn = true;
                }
                if (reason == (ContinuationReason)3) //ContinuationReason.Raise
                {
                    //return true;
                    outputs._ReturnValue = true;
                    //TODO 
                    p.Return(new ZingSourceContext(0, 3424, 3435), null);
                    StateImpl.IsReturn = true;
                }
                if (reason == (ContinuationReason)4)  //ContinuationReason.Receive
                {
                    //myHandle.DequeueEvent(false);
                    Z.Application.MachineHandle.DequeueEvent callee = new Z.Application.MachineHandle.DequeueEvent(application);
                    callee.inputs.priv_hasNullTransition = false;
                    callee.This = handle;
                    p.Call(callee);
                    StateImpl.IsCall = true;

                    nextBlock = Blocks.B0;
                }
                if (reason == (ContinuationReason)1)  //ContinuationReason.Nondet
                {
                    //No splitting into a new Block after nondet, since it is a local thing
                    //myHandle.cont.nondet = choose(bool);
                    application.SetPendingChoices(p, Microsoft.Zing.Application.GetChoicesForType(typeof(bool)));
                    cont.nondet = ((Boolean)application.GetSelectedChoiceValue(p));
                    //return false;
                    outputs._ReturnValue = false;
                    //TODO
                    p.Return(new ZingSourceContext(0, 3789, 3801), null);
                    StateImpl.IsReturn = true;
                }
                if (reason == (ContinuationReason)6)  //ContinuationReason.NewMachine
                {
                    //yield;
                    p.MiddleOfTransition = false;
                    //Finish the Block after asgn into MiddleOfTransition
                    nextBlock = Blocks.B1;
                }
                if (reason == (ContinuationReason)5)  //ContinuationReason.Send
                {
                    //yield;
                    p.MiddleOfTransition = false;
                    //Finish the Block after asgn into MiddleOfTransition
                    nextBlock = Blocks.B2;
                }
            }
            public void B0(Z.Process p)
            {
                //ContinuationReason.Receive after Dequeue call:
                p.LastFunctionCompleted = null;
                //return false;
                outputs._ReturnValue = false;
                //TODO
                p.Return(new ZingSourceContext(0, 3676, 3688), null);
                StateImpl.IsReturn = true;
            }
            public void B1(Z.Process p)
            {
                //ContinuationReason.NewMachine after yield:
                //return false;
                outputs._ReturnValue = false;
                //TODO
                p.Return(new ZingSourceContext(0, 3876, 3888), null);
                StateImpl.IsReturn = true;
            }
            public void B2(Z.Process p)
            {
                //ContinuationReason.Send after yield:
                //return false;
                outputs._ReturnValue = false;
                //TODO
                p.Return(new ZingSourceContext(0, 3957, 3969), null);
                StateImpl.IsReturn = true;
            }
        }

        //P program-dependent:
        //ZingMethod ReentrancyHelper is located in the generated Machine_<> class

        //not P program-dependent:
        //regular C# method now:
        public void ignore(Application app, Pointer This, Continuation entryCtxt)
        {
            //PrtValue_ARRAY locals;
            ArrayList<PrtValue> locals;
            //Alternative:
            //Z.Pointer locals;
            //StackFrame retTo_0;
            StackFrame retTo_0;
            //Alternative:
            //Z.Pointer retTo_0;

            //dummy:
            //retTo_0 = entryCtxt.PopReturnTo();
            retTo_0 = entryCtxt.PopReturnTo();
            //locals = retTo_0.locals;
            locals = retTo_0.locals;
            //if ((retTo_0.pc == 0)) { goto start;} ++++++++++++++++++++++++stopped here
            if (retTo_0.pc != 0)
            {
                //assert(false, "Internal error");
                //"this" is Machine class (unlike in old compiler, where it is Machine.ignore class -
                //hence, "in ignore" is added to the message)
                this.StateImpl.Exception = new Z.ZingAssertionFailureException(@"false", @"Internal error in ignore");
            }
            //start:
            //entryCtxt.Return();
            entryCtxt.Return(app);
            //return;
            return;
        }

        //P program-dependent (user-defined, have to be generated) AnonFunXXX (anon action and functions)
        //will be located in the derived Machine_<machine name> class
    }   //for class Machine

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
        public static Event ConstEvent;
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

    public class ActionOrFun
    {

    }

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

        static State Construct(State name, ActionOrFun entryFun, ActionOrFun exitFun, int numTransitions, bool hasNullTransition, StateTemperature temperature)
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

        Transition FindPushTransition(Event evt)
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

        Transition FindTransition(Event evt)
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
        StateStack stack;
        Continuation cont;
        EventBuffer buffer;
        int maxBufferSize;
        Machine machineName;
        int machineId;
        int instance;
        Event currentEvent;
        PrtValue currentArg;
        HashSet<Event> receiveSet;

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

        public void EnqueueEvent(Event e, PrtValue arg, MachineHandle source)
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
                    new ZingSourceContext(0, 12172, 12283),
                    null,
                    @"<EnqueueLog> {0}-{1} Machine has been halted and Event {2} is dropped",
                    this.machineName, this.instance, e.name);
            }
            else
            {
                if (arg != null)
                {
                    application.Trace(
                        new ZingSourceContext(0, 12542, 12686),
                        null,
                        @"<EnqueueLog> Enqueued Event < {0} > in Machine {1}-{2} by {3}-{4}",
                        e.name, this.machineName, this.instance, source.machineName, source.instance);
                }
                else
                {
                    application.Trace(
                        new ZingSourceContext(0, 12335, 12387),
                        null,
                        @"<EnqueueLog> Enqueued Event < {0}, ",
                        e.name);
                }

                this.buffer.EnqueueEvent(e, arg);
                if (this.maxBufferSize != -1 && this.buffer.eventBufferSize > this.maxBufferSize)
                {
                    application.Trace(new ZingSourceContext(0, 12811, 12921), null, @"<EXCEPTION> Event Buffer Size Exceeded {0} in Machine {1}-{2}",
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

            ActionOrFun Find(Event f)
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

            void AddStackDeferredSet(HashSet<Event> localDeferredSet)
            {
                if (next == null)
                {
                    return;
                }
                localDeferredSet.UnionWith(next.deferredSet);
            }

            void AddStackActionSet(HashSet<Event> localActionSet)
            {
                if (next == null)
                {
                    return;
                }
                localActionSet.UnionWith(next.actionSet);
            }

            bool HasNullTransitionOrAction()
            {
                if (state.hasNullTransition) return true;
                return actionSet.Contains(Event.NullEvent);
            }
        }
    }

    internal enum ContinuationReason : int
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
        StackFrame returnTo;
        ContinuationReason reason;
        MachineHandle id;
        PrtValue retVal;

        // The nondet field is different from the fields above because it is used 
        // by ReentrancyHelper to pass the choice to the nondet choice point.
        // Therefore, nondet should not be reinitialized in this class.
        bool nondet;

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
    //Prt.zing finishes here ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

    //PrtTypes.zing starts here: ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    public enum PrtTypeKind : int
    {
        PRT_KIND_ANY,
        PRT_KIND_BOOL,
        PRT_KIND_EVENT,
        PRT_KIND_REAL,
        PRT_KIND_INT,
        PRT_KIND_MAP,
        PRT_KIND_NMDTUP,
        PRT_KIND_NULL,
        PRT_KIND_SEQ,
        PRT_KIND_TUPLE,
    };

    public class PrtType
    {
        public PrtTypeKind typeKind;
        public int typeTag;
        public int arity;
        public List<string> fieldNames;
        public List<PrtType> fieldTypes;
        public PrtType innerType;
        public PrtType domType;
        public PrtType codType;

        public static PrtType BuildDefault(PrtTypeKind typeKind)
        {
            PrtType type = new PrtType();
            type.typeKind = typeKind;
            return type;
        }

        public static PrtType PrtMkPrimitiveType(PrtTypeKind primType)
        {
            PrtType type = PrtType.BuildDefault(primType);
            return type;
        }

        public static PrtType PrtMkMapType(PrtType domType, PrtType codType)
        {
            PrtType type = PrtType.BuildDefault(PrtTypeKind.PRT_KIND_MAP);
            type.domType = domType;
            type.codType = codType;
            return type;
        }

        public static PrtType PrtMkNmdTupType(int arity)
        {
            PrtType type = PrtType.BuildDefault(PrtTypeKind.PRT_KIND_NMDTUP);
            type.arity = arity;
            type.fieldNames = new List<string>(arity);
            type.fieldTypes = new List<PrtType>(arity);
            return type;
        }

        public static PrtType PrtMkSeqType(PrtType innerType)
        {
            PrtType type = PrtType.BuildDefault(PrtTypeKind.PRT_KIND_SEQ);
            type.innerType = innerType;
            return type;
        }

        public static PrtType PrtMkTupType(int arity)
        {
            PrtType type = PrtType.BuildDefault(PrtTypeKind.PRT_KIND_TUPLE);
            type.arity = arity;
            type.fieldTypes = new List<PrtType>(arity);
            return type;
        }
        public static void PrtSetFieldType(PrtType tupleType, int index, PrtType fieldType)
        {
            (tupleType.fieldTypes).Insert(index, fieldType);
        }

        public static void PrtSetFieldName(PrtType tupleType, int index, string fieldName)
        {
            (tupleType.fieldNames).Insert(index, fieldName);
        }
    };
    //PrtTypes.zing finishes here ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

    //PrtValues.zing starts here: ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

    public class PrtValue
    {
        public PrtType type;
        public bool bl;
        public Event ev;
        public int nt;
        public MachineHandle mach;
        public List<PrtValue> tuple;
        public PrtSeq seq;
        public PrtMap map;

        //TODO: Print method is skipped

        public static PrtValue PrtMkDefaultValue(PrtType type)
        {
            PrtValue value = new PrtValue();
            value.type = type;

            if (type.typeKind == PrtTypeKind.PRT_KIND_ANY)
            {
                value.type = PrtType.PrtMkPrimitiveType(PrtTypeKind.PRT_KIND_NULL);
            }
            else if (type.typeKind == PrtTypeKind.PRT_KIND_TUPLE ||
                     type.typeKind == PrtTypeKind.PRT_KIND_NMDTUP)
            {
                value.tuple = new List<PrtValue>(type.arity);
                var fieldTypesArray = (type.fieldTypes).ToArray();

                for (int i = 0; i <= type.arity; i++)
                {
                    value.tuple.Add(PrtMkDefaultValue((type.fieldTypes)[i]));
                }
            }
            else if (type.typeKind == PrtTypeKind.PRT_KIND_SEQ)
            {
                value.seq = PrtSeq.PrtMkDefaultSeq();
            }
            else if (type.typeKind == PrtTypeKind.PRT_KIND_MAP)
            {
                value.map = PrtMap.PrtMkDefaultMap();
            }
            return value;
        }

        public static PrtValue PrtCloneValue(PrtValue value)
        {
            PrtValue newValue = new PrtValue();
            newValue.type = value.type;
            newValue.bl = value.bl;
            newValue.ev = value.ev;
            newValue.nt = value.nt;
            newValue.mach = value.mach;
            newValue.tuple = value.tuple;
            newValue.seq = value.seq;
            newValue.map = value.map;

            if (value.type.typeKind == PrtTypeKind.PRT_KIND_TUPLE ||
                value.type.typeKind == PrtTypeKind.PRT_KIND_NMDTUP)
            {
                newValue.tuple = new List<PrtValue>(value.type.arity);
                foreach (PrtValue elem in value.tuple)
                {
                    (newValue.tuple).Add(PrtCloneValue(elem));
                }
            }
            else if (value.type.typeKind == PrtTypeKind.PRT_KIND_SEQ)
            {
                newValue.seq = value.seq.Clone();
            }
            else if (value.type.typeKind == PrtTypeKind.PRT_KIND_MAP)
            {
                newValue.map = value.map.Clone();
            }
            return newValue;
        }

        public static bool PrtInhabitsType(PrtValue value, PrtType type)
        {
            PrtTypeKind tkind = type.typeKind;
            PrtTypeKind vkind = value.type.typeKind;
            bool isNullValue = PrtIsNullValue(value);

            if (tkind == PrtTypeKind.PRT_KIND_ANY)
                return true;

            if (tkind == PrtTypeKind.PRT_KIND_BOOL)
                return vkind == PrtTypeKind.PRT_KIND_BOOL;

            if (tkind == PrtTypeKind.PRT_KIND_EVENT)
                return (vkind == PrtTypeKind.PRT_KIND_EVENT || isNullValue);

            if (tkind == PrtTypeKind.PRT_KIND_REAL)
                return (vkind == PrtTypeKind.PRT_KIND_REAL || isNullValue);

            if (tkind == PrtTypeKind.PRT_KIND_INT)
                return vkind == PrtTypeKind.PRT_KIND_INT;

            if (tkind == PrtTypeKind.PRT_KIND_MAP)
            {
                if (vkind != PrtTypeKind.PRT_KIND_MAP)
                {
                    return false;
                }
                return value.map.InhabitsType(type);
            }

            if (tkind == PrtTypeKind.PRT_KIND_NMDTUP)
            {
                if (vkind != PrtTypeKind.PRT_KIND_NMDTUP)
                {
                    return false;
                }

                if (type.arity != value.type.arity)
                {
                    return false;
                }

                for (int i = 0; i < type.arity; i++)
                {
                    if ((type.fieldNames)[i] != (value.type.fieldNames)[i])
                    {
                        return false;
                    }
                    if (!PrtInhabitsType((value.tuple)[i], (type.fieldTypes)[i]))
                    {
                        return false;
                    }
                }
                return true;
            }

            if (tkind == PrtTypeKind.PRT_KIND_TUPLE)
            {
                if (vkind != PrtTypeKind.PRT_KIND_TUPLE)
                {
                    return false;
                }

                if (type.arity != value.type.arity)
                {
                    return false;
                }

                for (int i = 0; i < type.arity; i++)
                {
                    if (!PrtInhabitsType((value.tuple)[i], (type.fieldTypes)[i]))
                    {
                        return false;
                    }
                }
                return true;
            }

            if (tkind == PrtTypeKind.PRT_KIND_SEQ)
            {
                if (vkind != PrtTypeKind.PRT_KIND_SEQ)
                {
                    return false;
                }
                return value.seq.InhabitsType(type);
            }
            //TODO: tracing
            //trace("Invalid tkind value : {0}", tkind);
            Debug.Assert(false);
            return false;
        }

        public static bool PrtIsNullValue(PrtValue value)
        {
            PrtTypeKind kind = value.type.typeKind;
            Debug.Assert(kind != PrtTypeKind.PRT_KIND_ANY, "Value must have a concrete type");

            if (kind == PrtTypeKind.PRT_KIND_EVENT)
                return value.ev == null;

            if (kind == PrtTypeKind.PRT_KIND_REAL)
                return value.mach == null;

            if (kind == PrtTypeKind.PRT_KIND_NULL)
                return true;

            return false;
        }

        public static PrtValue PrtCastValue(PrtValue value, PrtType type)
        {
            Debug.Assert(PrtInhabitsType(value, type), "value must be a member of type");
            value.type = type;
            return PrtCloneValue(value);
        }

        public static bool PrtIsEqualValue(PrtValue value1, PrtValue value2)
        {
            PrtType type1 = value1.type;
            PrtType type2 = value2.type;
            PrtTypeKind kind1 = type1.typeKind;
            PrtTypeKind kind2 = type2.typeKind;
            bool isNullValue1 = PrtIsNullValue(value1);
            bool isNullValue2 = PrtIsNullValue(value2);

            if (isNullValue1 && isNullValue2)
            {
                return true;
            }
            else if (kind1 != kind2)
            {
                return false;
            }
            else if (value1 == value2)
            {
                return true;
            }

            if (kind1 == PrtTypeKind.PRT_KIND_BOOL)
                return value1.bl == value2.bl;

            if (kind1 == PrtTypeKind.PRT_KIND_EVENT)
                return value1.ev == value2.ev;

            if (kind1 == PrtTypeKind.PRT_KIND_REAL)
                return value1.mach == value2.mach;

            if (kind1 == PrtTypeKind.PRT_KIND_INT)
                return value1.nt == value2.nt;

            if (kind1 == PrtTypeKind.PRT_KIND_MAP)
            {
                return value1.map.Equals(value2.map);
            }

            if (kind1 == PrtTypeKind.PRT_KIND_NMDTUP)
            {
                if (type1.arity != type2.arity)
                {
                    return false;
                }
                for (int i = 0; i < type1.arity; i++)
                {
                    if ((type1.fieldNames)[i] != (type2.fieldNames)[i])
                    {
                        return false;
                    }
                    if (!PrtIsEqualValue((value1.tuple)[i], (value2.tuple)[i]))
                    {
                        return false;
                    }
                }
                return true;
            }

            if (kind1 == PrtTypeKind.PRT_KIND_SEQ)
            {
                return value1.seq.Equals(value2.seq);
            }

            if (kind1 == PrtTypeKind.PRT_KIND_TUPLE)
            {
                if (type1.arity != type2.arity)
                {
                    return false;
                }
                for (int i = 0; i < type1.arity; i++)
                {
                    if (!PrtIsEqualValue((value1.tuple)[i], (value2.tuple)[i]))
                    {
                        return false;
                    }
                }
                return true;
            }
            Debug.Assert(false);
            return false;
        }

        public static void PrtPrimSetBool(PrtValue prmVal, bool value)
        {
            prmVal.bl = value;
        }

        public static bool PrtPrimGetBool(PrtValue prmVal)
        {
            return prmVal.bl;
        }

        public static void PrtPrimSetEvent(PrtValue prmVal, Event value)
        {
            prmVal.ev = value;
        }

        public static Event PrtPrimGetEvent(PrtValue prmVal)
        {
            return prmVal.ev;
        }

        static void PrtPrimSetInt(PrtValue prmVal, int value)
        {
            prmVal.nt = value;
        }

        static int PrtPrimGetInt(PrtValue prmVal)
        {
            return prmVal.nt;
        }

        static void PrtPrimSetMachine(PrtValue prmVal, MachineHandle value)
        {
            prmVal.mach = value;
        }

        static MachineHandle PrtPrimGetMachine(PrtValue prmVal)
        {
            return prmVal.mach;
        }

        static void PrtTupleSet(PrtValue tuple, int index, PrtValue value)
        {
            (tuple.tuple)[index] = PrtCloneValue(value);
        }

        static PrtValue PrtTupleGet(PrtValue tuple, int index)
        {
            return PrtValue.PrtCloneValue((tuple.tuple)[index]);
        }

        static void PrtSeqSet(PrtValue seq, PrtValue index, PrtValue value)
        {
            seq.seq.Set(index.nt, value);
        }

        static void PrtSeqInsert(PrtValue seq, PrtValue index, PrtValue value)
        {
            seq.seq.Insert(index.nt, value);
        }

        static void PrtSeqRemove(PrtValue seq, PrtValue index)
        {
            seq.seq.Remove(index.nt);
        }

        static PrtValue PrtSeqGet(PrtValue seq, PrtValue index)
        {
            return seq.seq.Get(index.nt);
        }

        static PrtValue PrtSeqGetNoClone(PrtValue seq, PrtValue index)
        {
            return seq.seq.GetNoClone(index.nt);
        }

        static int PrtSeqSizeOf(PrtValue seq)
        {
            return (seq.seq).Count();
        }

        static void PrtMapSet(PrtValue map, PrtValue key, PrtValue value)
        {
            map.map.Set(key, value);
        }

        static void PrtMapInsert(PrtValue map, PrtValue key, PrtValue value)
        {
            Debug.Assert(!map.map.Exists(key), "key must not exist in map");
            map.map.Set(key, value);
        }

        static void PrtMapRemove(PrtValue map, PrtValue key)
        {
            map.map.Remove(key);
        }

        static PrtValue PrtMapGet(PrtValue map, PrtValue key)
        {
            return map.map.Get(key);
        }

        static PrtValue PrtMapGetNoClone(PrtValue map, PrtValue key)
        {
            return map.map.GetNoClone(key);
        }

        static PrtValue PrtMapGetKeys(PrtValue map)
        {
            return map.map.GetKeys(map.type.domType);
        }

        static PrtValue PrtMapGetValues(PrtValue map)
        {
            return map.map.GetValues(map.type.codType);
        }

        static bool PrtMapExists(PrtValue map, PrtValue key)
        {
            return map.map.Exists(key);
        }

        static int PrtMapSizeOf(PrtValue map)
        {
            return map.map.SizeOf();
        }
    };

    public class PrtSeq
    {
        public int size;
        public List<PrtValue> contents;

        public static PrtSeq PrtMkDefaultSeq()
        {
            PrtSeq seq = new PrtSeq();
            seq.size = 0;
            seq.contents = new List<PrtValue>(0);
            return seq;
        }

        public PrtSeq Clone()
        {
            PrtSeq seq = new PrtSeq();
            seq.size = this.size;
            seq.contents = new List<PrtValue>(this.size);
            for (int i = 0; i < this.size; i++)
            {
                (seq.contents)[i] = PrtValue.PrtCloneValue((this.contents)[i]);
            }
            return seq;
        }

        public void Set(int index, PrtValue value)
        {
            Debug.Assert(0 <= index && index < this.size, "index out of bound");
            (this.contents)[index] = PrtValue.PrtCloneValue(value);
        }

        public void Insert(int index, PrtValue value)
        {
            List<PrtValue> newContents = new List<PrtValue>(this.size + 1);
            Debug.Assert(0 <= index && index <= this.size, "index out of bound");
            for (int i = 0; i < this.size + 1; i++)
            {
                if (i < index)
                {
                    newContents[i] = (this.contents)[i];
                }
                else if (i == index)
                {
                    newContents[i] = PrtValue.PrtCloneValue(value);
                }
                else
                {
                    newContents[i] = (this.contents)[i - 1];
                }
            }
            this.contents = newContents;
            this.size = this.size + 1;
        }

        public void Remove(int index)
        {
            Debug.Assert(0 <= index && index < this.size, "index out of bound");
            for (int i = index; i < this.size - 1; i++)
            {
                (this.contents)[i] = (this.contents)[i + 1];
            }
            this.size = this.size - 1;
        }

        public PrtValue Get(int index)
        {
            Debug.Assert(0 <= index && index < this.size, "index out of bound");
            return PrtValue.PrtCloneValue((this.contents)[index]);
        }

        public PrtValue GetNoClone(int index)
        {
            Debug.Assert(0 <= index && index < this.size, "index out of bound");
            return (this.contents)[index];
        }

        public int SizeOf()
        {
            return this.size;
        }

        public bool InhabitsType(PrtType type)
        {
            for (int i = 0; i < this.size; i++)
            {
                if (!PrtValue.PrtInhabitsType((this.contents)[i], type.innerType))
                {
                    return false;
                }
            }
            return true;
        }

        public bool Equals(PrtSeq seq)
        {
            if (this.size != seq.size)
            {
                return false;
            }
            for (int i = 0; i < this.size; i++)
            {
                if (!PrtValue.PrtIsEqualValue((this.contents)[i], (seq.contents)[i]))
                {
                    return false;
                }
            }
            return true;
        }
    }

    public class PrtMap
    {
        public int size;
        public List<PrtValue> keys;
        public List<PrtValue> values;

        public static PrtMap PrtMkDefaultMap()
        {
            PrtMap map = new PrtMap();
            map.size = 0;
            map.keys = new List<PrtValue>(0);
            map.values = new List<PrtValue>(0);
            return map;
        }

        public PrtMap Clone()
        {
            PrtMap map = new PrtMap();
            map.size = this.size;
            map.keys = new List<PrtValue>(this.size);
            map.values = new List<PrtValue>(this.size);
            for (int i = 0; i < this.size; i++)
            {
                (map.keys)[i] = PrtValue.PrtCloneValue((this.keys)[i]);
                (map.values)[i] = PrtValue.PrtCloneValue((this.values)[i]);
            }
            return map;
        }

        public void Set(PrtValue key, PrtValue value)
        {
            for (int i = 0; i < this.size; i++)
            {
                if (PrtValue.PrtIsEqualValue((this.keys)[i], key))
                {
                    (this.values)[i] = PrtValue.PrtCloneValue(value);
                    return;
                }
            }

            List<PrtValue> newKeys = new List<PrtValue>(this.size + 1);
            List<PrtValue> newValues = new List<PrtValue>(this.size + 1);
            for (int i = 0; i < this.size; i++)
            {
                newKeys[i] = (this.keys)[i];
                newValues[i] = (this.values)[i];
            }
            newKeys[this.size] = PrtValue.PrtCloneValue(key);
            newValues[this.size] = PrtValue.PrtCloneValue(value);

            this.keys = newKeys;
            this.values = newValues;
            this.size = this.size + 1;
        }

        public void Remove(PrtValue key)
        {
            for (int i = 0; i < this.size; i++)
            {
                if (PrtValue.PrtIsEqualValue((this.keys)[i], key))
                {
                    List<PrtValue> newKeys = new List<PrtValue>(this.size - 1);
                    List<PrtValue> newValues = new List<PrtValue>(this.size - 1);

                    for (int j = 0; j < this.size; j++)
                    {
                        if (j < i)
                        {
                            newKeys[i] = (this.keys)[i];
                            newValues[i] = (this.values)[i];
                        }
                        else if (j > i)
                        {
                            newKeys[j - 1] = (this.keys)[j];
                            newValues[j - 1] = (this.values)[j];
                        }
                    }

                    this.keys = newKeys;
                    this.values = newValues;
                    this.size = this.size - 1;
                    return;
                }
            }
            Debug.Assert(false, "key not found");
        }

        public PrtValue Get(PrtValue key)
        {
            for (int i = 0; i < this.size; i++)
            {
                if (PrtValue.PrtIsEqualValue((this.keys)[i], key))
                {
                    return PrtValue.PrtCloneValue((this.values)[i]);
                }
            }
            Debug.Assert(false, "key not found");
            return null;
        }

        public PrtValue GetNoClone(PrtValue key)
        {
            for (int i = 0; i < this.size; i++)
            {
                if (PrtValue.PrtIsEqualValue((this.keys)[i], key))
                {
                    return (this.values)[i];
                }
            }
            Debug.Assert(false, "key not found");
            return null;
        }

        public PrtValue GetKeys(PrtType domType)
        {
            PrtSeq seq = new PrtSeq();
            seq.size = this.size;
            seq.contents = new List<PrtValue>(this.size);
            for (int i = 0; i < this.size; i++)
            {
                (seq.contents)[i] = PrtValue.PrtCloneValue((this.keys)[i]);
            }
            PrtType seqType = PrtType.PrtMkSeqType(domType);
            PrtValue retVal = PrtValue.PrtMkDefaultValue(seqType);
            retVal.seq = seq;
            return retVal;
        }

        public PrtValue GetValues(PrtType codType)
        {
            PrtSeq seq = new PrtSeq();
            seq.size = this.size;
            seq.contents = new List<PrtValue>(this.size);
            for (int i = 0; i < this.size; i++)
            {
                (seq.contents)[i] = PrtValue.PrtCloneValue((this.values)[i]);
            }
            PrtType seqType = PrtType.PrtMkSeqType(codType);
            PrtValue retVal = PrtValue.PrtMkDefaultValue(seqType);
            retVal.seq = seq;
            return retVal;
        }

        public bool Exists(PrtValue key)
        {
            for (int i = 0; i < this.size; i++)
            {
                if (PrtValue.PrtIsEqualValue((this.keys)[i], key))
                {
                    return true;
                }
            }
            return false;
        }

        public int SizeOf()
        {
            return this.size;
        }

        public bool IsSameMapping(PrtValue key, PrtValue value)
        {
            for (int i = 0; i < this.size; i++)
            {
                if (PrtValue.PrtIsEqualValue((this.keys)[i], key))
                {
                    return PrtValue.PrtIsEqualValue((this.values)[i], value);
                }
            }
            return false;
        }

        public bool InhabitsType(PrtType type)
        {
            for (int i = 0; i < this.size; i++)
            {
                if (!PrtValue.PrtInhabitsType((this.keys)[i], type.domType))
                {
                    return false;
                }
                if (!PrtValue.PrtInhabitsType((this.values)[i], type.codType))
                {
                    return false;
                }
            }
            return true;
        }

        public bool Equals(PrtMap map)
        {
            if (this.size != map.size)
            {
                return false;
            }
            for (int i = 0; i < this.size; i++)
            {
                if (!map.IsSameMapping((this.keys)[i], (this.values)[i]))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
