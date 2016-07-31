using Z = Microsoft.Zing;
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Diagnostics;

public class Application
      : Z.StateImpl
{
    //Derived (P program depenedent) class ___MACHINE_<machine name>
    //This class would have to be generated
    class ___MACHINE_Real1 : ___MACHINE
    {
        //TODO: Do we need the three constructors below?
        //Verify that having diff number of parqameters in the constructor and 
        //base constructor would work - it should!
        //adding machineName argument to the constructor, to initialize
        //___machineName fiel
        //TODO: base constructor below calls the one for the ___MACHINE class first (not for Z.ZingClass).

        public ___MACHINE_Real1(Application application, string machineName) : base(application)
        {
            ___machineName = machineName;
        }
        //public ___MACHINE_Real1() { }
        //private ___MACHINE_Real1(___MACHINE c) : base(c) { }

        protected override short TypeId
        {
            get
            {
                return ___MACHINE.typeId;
            }
        }
        //TODO: verify typeId value as "1": shoud is be diff from typeId for ___MACHINE?
        private static readonly short typeId = 1;

        //machine name might be needed for tracing
        public string ___machineName;

        //For the following method, the only diff for a specific machine would be the name
        //of the specific class ___MACHINE_<machine name>
        //The derived method would be called ___<name>_Init_CalculateDeferredAndActionSet
        //Not ZingMethod in new compiler:
        public void ___Real1_Init_CalculateDeferredAndActionSet(Application app, Pointer This)
        {
            //public HeapElement LookupObject(Pointer ptr) returns
            //heap element given it's "pointer". Called when Zing code
            // dereferences a "Zing pointer".
            //LookupObject for common object is done in advance:
            var machine = (Z.Application.___MACHINE_Real1)app.LookupObject(This);
            var handle = (Z.Application.___SM_HANDLE)app.LookupObject(machine.___myHandle);
            //TODO(question): since ___StateStack is not a ZingClass, it is not clear
            //how to access regular C# class ___StateStack from handle
            //would this work?
            //RHS returns priv____stack:
            var stateStack = handle.___stack;
            //In old compiler:
            //var stateStack = (Z.Application.___StateStack)app.LookupObject(handle.___stack);

            //.zing code for this method with C# that implements it:
            //myHandle.stack.deferredSet = new SM_EVENT_SET;
            //Old compiler:
            //stateStack.___deferredSet = app.Allocate(new Z.Application.___SM_EVENT_SET(app));
            stateStack.___deferredSet = new HashSet<___SM_EVENT>();

            //myHandle.stack.actionSet = new SM_EVENT_SET;
            //Old compiler:
            //stateStack.___actionSet = app.Allocate(new Z.Application.___SM_EVENT_SET(app));
            stateStack.___actionSet = new HashSet<___SM_EVENT>();

            //myHandle.stack.AddStackDeferredSet(myHandle.stack.deferredSet);
            //In the old compiler, this call is a ZingMethod method call; 
            //in the new compiler, it's a call of a regular C# method:
            stateStack.___AddStackDeferredSet(stateStack.___deferredSet);

            //myHandle.stack.AddStackActionSet(myHandle.stack.actionSet);
            stateStack.___AddStackActionSet(stateStack.___actionSet);

            //myHandle.stack.es = new SM_EVENT_ARRAY[0];
            //Old compiler:
            //stateStack.___es = app.Allocate(new Z.Application.___SM_EVENT_ARRAY(app, 0));
            stateStack.___es = new List<___SM_EVENT>();

            //myHandle.stack.as = new ActionOrFun_ARRAY[0];
            //stateStack.___as = app.Allocate(new Z.Application.___ActionOrFun_ARRAY(app, 0));
            stateStack.___as = new List<___ActionOrFun>();
        }

        //This method depends on the full set of states for a particular machine;
        //below is an example for OneDummyMachine.p, which has just one state: Real1_init
        public void ___CalculateDeferredAndActionSet(Application app, Pointer This, Z.Pointer state)
        {
            if (state == application.globals.___Main____Real1_Init_SM_STATE)
            {
                ___Real1_Init_CalculateDeferredAndActionSet(app, This);
                return;

            }
            Debug.Assert(false, "Internal error");
        }

        //TODO: re-write ___ReentrancyHelper as template by using array of delegates
        //(aka array of function pointers) - see Log in OneNote.
        internal sealed class ___ReentrancyHelper : Z.ZingMethod
        {
            public ___ReentrancyHelper(Application app)
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
                    this.priv____locals = src.priv____locals;
                    this.priv____doPop = src.priv____doPop;
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
                                return priv____locals;
                            }
                        case 1:
                            {
                                return priv____doPop;
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
                                priv____locals = ((Z.Pointer)val);
                                return;
                            }
                        case 1:
                            {
                                priv____doPop = ((Boolean)val);
                                return;
                            }
                    }
                }
                public void WriteString(StateImpl state, BinaryWriter bw)
                {
                    bw.Write(state.GetCanonicalId(this.priv____locals));
                    bw.Write(this.priv____doPop);
                }
                public void TraverseFields(FieldTraverser ft)
                {
                    ft.DoTraversal(this.priv____locals);
                    ft.DoTraversal(this.priv____doPop);
                }
                public Z.Pointer priv____locals;
                public static int id____locals = 0;
                public Z.Pointer ___locals
                {
                    get
                    {
                        return priv____locals;
                    }
                    set
                    {
                        SetDirty();
                        priv____locals = value;
                    }
                }
                public bool priv____doPop;
                public static int id____doPop = 1;
                public bool ___doPop
                {
                    get
                    {
                        return priv____doPop;
                    }
                    set
                    {
                        SetDirty();
                        priv____doPop = value;
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
            public override bool RaiseZingException(int exception)
            {
                if ((handlerBlock == Blocks.None))
                {
                    return false;
                }
                this.CurrentException = exception;
                nextBlock = handlerBlock;
                return true;
            }
            //# of Blocks depends on # of anon functions in the machine
            //For OneDummyMAchine.p: B0 - B10
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
            //# of Blocks depends on # of anon functions in the machine
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
                                return new ZingSourceContext(0, 5398, 5399);
                            }
                        case Blocks.B1:
                            {
                                return new ZingSourceContext(0, 5398, 5399);
                            }
                        case Blocks.B2:
                            {
                                return new ZingSourceContext(0, 5386, 5392);
                            }
                        case Blocks.B3:
                            {
                                return new ZingSourceContext(0, 5352, 5381);
                            }
                        case Blocks.B4:
                            {
                                return new ZingSourceContext(0, 5352, 5381);
                            }
                        case Blocks.B5:
                            {
                                return new ZingSourceContext(0, 5352, 5381);
                            }
                        case Blocks.B6:
                            {
                                return new ZingSourceContext(0, 5327, 5350);
                            }
                        case Blocks.Enter:
                            {
                                return new ZingSourceContext(0, 4070, 4083);
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
            private static readonly short typeId = 10;
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
                    this.priv____actionFun = src.priv____actionFun;
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
                                return priv____actionFun;
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
                                priv____actionFun = ((Application.___ActionOrFun)val);
                                return;
                            }
                    }
                }
                public void WriteString(StateImpl state, BinaryWriter bw)
                {
                    bw.Write(((byte)this.priv____actionFun));
                }
                public void TraverseFields(FieldTraverser ft)
                {
                    ft.DoTraversal(this.priv____actionFun);
                }
                public Application.___ActionOrFun priv____actionFun;
                public static int id____actionFun = 0;
                public Application.___ActionOrFun ___actionFun
                {
                    get
                    {
                        return priv____actionFun;
                    }
                    set
                    {
                        SetDirty();
                        priv____actionFun = value;
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
                ___ReentrancyHelper clone = new ___ReentrancyHelper(((Application)myState));
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
            //Application.___ActionOrFun is an enum:
            public ___ReentrancyHelper(Application app, Pointer This, Application.___ActionOrFun ___actionFun)
            {
                application = app;
                nextBlock = Blocks.Enter;
                handlerBlock = Blocks.None;
                locals = new LocalVars(this);
                inputs = new InputVars(this);
                outputs = new OutputVars(this);
                this.This = This;
                inputs.___actionFun = ___actionFun;
            }
            public void Enter(Z.Process p)
            {
                var machine = (Z.Application.___MACHINE_Real1)application.LookupObject(This);
                var handle = (Z.Application.___SM_HANDLE)application.LookupObject(machine.___myHandle);
                var cont = (Z.Application.___Continuation)handle.___cont;
                //init:
                //doPop = false;
                locals.___doPop = false;

                //myHandle.cont.Reset();
                cont.___Reset(application);

                //if ((actionFun == ActionOrFun._Real1_ignore)) {
                if ((inputs.___actionFun == (((___ActionOrFun)1))))          //#1 is always "___ignore"
                {
                    //B60
                    //Skipped:
                    //trace("<FunctionLog> Machine Real1-{0} executing Function ignore\n", myHandle.instance);
                    //locals = null;
                    locals.___locals = 0;

                    //myHandle.cont.PushReturnTo(0, locals);
                    //Calling regular C# method:
                    cont.___PushReturnTo(0, locals.___locals);

                    //goto execute_ignore;
                    //new Block is needed: loop header starts
                    nextBlock = Blocks.B0;
                }
                //B55
                //if ((actionFun == ActionOrFun._Real1_AnonFun0)) {
                if ((inputs.___actionFun == (((___ActionOrFun)2))))          //#2 is Real1_AnonFun0
                {
                    //B54
                    //locals = new PRT_VALUE_ARRAY[1];
                    //old compiler: Allocate has HeapElement as its argument;
                    //locals.___locals = application.Allocate(new Z.Application.___PRT_VALUE_ARRAY(application, 1));
                    //Application.___PRT_VALUE_ARRAY has the required type; however, in the new compiler, this type
                    //is replaced with List<___PRT_VALUE>(), which is not a HeapElement type.
                    //TODO(question): how to convert new List<___PRT_VALUE>(1) to Z.Application.___PRT_VALUE_ARRAY
                    //or just a HeapElement?
                    //Here one way (exactly as in the old compiler): would it work for new List<___PRT_VALUE>(1)?
                    locals.___locals = application.Allocate(new Z.Application.___PRT_VALUE_ARRAY(application, 1));

                    //locals[0] = PRT_VALUE.PrtCloneValue(myHandle.currentArg);
                    ((Z.Application.___PRT_VALUE_ARRAY)application.LookupObject(locals.___locals))[(int)0] = (___PRT_VALUE)handle.___currentArg;

                    //myHandle.cont.PushReturnTo(0, locals);
                    cont.___PushReturnTo(0, locals.___locals);

                    //goto execute_AnonFun0;
                    //B24
                    //new Block is needed: loop header starts
                    nextBlock = Blocks.B2;
                }
                //B48
                //if ((actionFun == ActionOrFun._Real1_AnonFun1)) {
                if ((inputs.___actionFun == (((___ActionOrFun)3))))          //#3 is Real1_AnonFun1
                {
                    //locals = new PRT_VALUE_ARRAY[1];
                    //TODO: see previous TODO 
                    locals.___locals = application.Allocate(new Z.Application.___PRT_VALUE_ARRAY(application, 1));

                    //locals[0] = PRT_VALUE.PrtCloneValue(myHandle.currentArg);
                    ((Z.Application.___PRT_VALUE_ARRAY)application.LookupObject(locals.___locals))[(int)0] = (___PRT_VALUE)handle.___currentArg;

                    //myHandle.cont.PushReturnTo(0, locals);
                    cont.___PushReturnTo(0, locals.___locals);

                    //goto execute_AnonFun1;
                    //B16
                    nextBlock = Blocks.B4;
                }
                //B41
                //if ((actionFun == ActionOrFun._Real1_AnonFun2))
                if ((inputs.___actionFun == (((___ActionOrFun)4))))          //#4 is Real1_AnonFun2
                {
                    //locals = new PRT_VALUE_ARRAY[1];
                    //TODO: see previous TODO 
                    locals.___locals = application.Allocate(new Z.Application.___PRT_VALUE_ARRAY(application, 1));

                    //locals[0] = PRT_VALUE.PrtCloneValue(myHandle.currentArg);
                    ((Z.Application.___PRT_VALUE_ARRAY)application.LookupObject(locals.___locals))[(int)0] = (___PRT_VALUE)handle.___currentArg;

                    //myHandle.cont.PushReturnTo(0, locals);
                    cont.___PushReturnTo(0, locals.___locals);

                    //goto execute_AnonFun2;
                    //B8
                    nextBlock = Blocks.B6;
                }
                //B34
                //Debug.Assert(false, "Internal error");
                this.StateImpl.Exception = new Z.ZingAssertionFailureException(@"false", @"Internal error");
                nextBlock = Blocks.B0;
            }
            public void B0(Z.Process p)
            {
                var machine = (Z.Application.___MACHINE_Real1)application.LookupObject(This);
                //B31
                //execute_ignore:
                //while (!doPop)
                if (!locals.___doPop)
                {
                    //B30
                    //ignore(myHandle.cont);
                    machine.___ignore(application, machine, cont);
                    //doPop = ProcessContinuation();
                    Z.Application.___MACHINE.___ProcessContinuation callee = new Z.Application.___MACHINE.___ProcessContinuation(application);
                    callee.This = This;
                    p.Call(callee);
                    StateImpl.IsCall = true;
                    nextBlock = Blocks.B1;
                }
                else
                {
                    //B26
                    //TODO
                    p.Return(new ZingSourceContext(0, 5065, 5071), null);
                    StateImpl.IsReturn = true;
                }
            }
            public void B1(Z.Process p)
            {
                //return from ProcessContinuation:
                locals.___doPop = (((Z.Application.___MACHINE.___ProcessContinuation)p.LastFunctionCompleted)).outputs._Lfc_ReturnValue;
                p.LastFunctionCompleted = null;
                //to while loop head:
                nextBlock = Blocks.B0;
            }
            public void B2(Z.Process p)
            {
                //B23
                //execute_AnonFun0:
                //while (!doPop)
                if (!locals.___doPop)
                {
                    //B22
                    //AnonFun0(myHandle.cont);
                    Z.Application.___MACHINE_Real1.___AnonFun0(application, (((Z.Application.___SM_HANDLE)application.LookupObject((((Z.Application.___MACHINE_Real1)application.LookupObject(This))).___myHandle))).___cont);
                    //doPop = ProcessContinuation();
                    Z.Application.___MACHINE_Real1.___ProcessContinuation callee = new Z.Application.___MACHINE_Real1.___ProcessContinuation(application);
                    callee.This = This;
                    p.Call(callee);
                    StateImpl.IsCall = true;
                    nextBlock = Blocks.B3;
                }
                else
                {
                    //B18
                    //return;
                    //TODO
                    p.Return(new ZingSourceContext(0, 5172, 5178), null);
                    StateImpl.IsReturn = true;
                }
            }
            public void B3(Z.Process p)
            {
                //return from ___ProcessContinuation:
                locals.___doPop = (((Z.Application.___MACHINE_Real1.___ProcessContinuation)p.LastFunctionCompleted)).outputs._Lfc_ReturnValue;
                p.LastFunctionCompleted = null;

                //back to the while loop header:
                nextBlock = Blocks.B2;
            }
            public void B4(Z.Process p)
            {
                //B15
                //execute_AnonFun1:
                //while (!doPop)
                if (!locals.___doPop)
                {
                    //B14
                    //AnonFun1(myHandle.cont);
                    Z.Application.___MACHINE_Real1.___AnonFun1(application, (((Z.Application.___SM_HANDLE)application.LookupObject((((Z.Application.___MACHINE_Real1)application.LookupObject(This))).___myHandle))).___cont);

                    //doPop = ProcessContinuation();
                    Z.Application.___MACHINE_Real1.___ProcessContinuation callee = new Z.Application.___MACHINE_Real1.___ProcessContinuation(application);
                    callee.This = This;
                    p.Call(callee);
                    StateImpl.IsCall = true;
                    nextBlock = Blocks.B5;
                }
                else
                {
                    //B10
                    //return;
                    //TODO
                    p.Return(new ZingSourceContext(0, 5279, 5285), null);
                    StateImpl.IsReturn = true;
                }
            }
            public void B5(Z.Process p)
            {
                //return from ___ProcessContinuation:
                locals.___doPop = (((Z.Application.___MACHINE_Real1.___ProcessContinuation)p.LastFunctionCompleted)).outputs._Lfc_ReturnValue;
                p.LastFunctionCompleted = null;

                //back to the while loop header:
                nextBlock = Blocks.B4;
            }
            public void B6(Z.Process p)
            {
                //B7
                //while (!doPop)
                if (!locals.___doPop)
                {
                    //B6
                    //execute_AnonFun2:
                    //AnonFun1(myHandle.cont);
                    Z.Application.___MACHINE_Real1.___AnonFun2(application, (((Z.Application.___SM_HANDLE)application.LookupObject((((Z.Application.___MACHINE_Real1)application.LookupObject(This))).___myHandle))).___cont);

                    //doPop = ProcessContinuation();
                    Z.Application.___MACHINE_Real1.___ProcessContinuation callee = new Z.Application.___MACHINE_Real1.___ProcessContinuation(application);
                    callee.This = This;
                    p.Call(callee);
                    StateImpl.IsCall = true;
                    nextBlock = Blocks.B7;
                }
                else
                {
                    //B2
                    //return;
                    //TODO
                    p.Return(new ZingSourceContext(0, 5279, 5285), null);
                    StateImpl.IsReturn = true;
                }
            }
            public void B7(Z.Process p)
            {
                //return from ___ProcessContinuation:
                locals.___doPop = (((Z.Application.___MACHINE_Real1.___ProcessContinuation)p.LastFunctionCompleted)).outputs._Lfc_ReturnValue;
                p.LastFunctionCompleted = null;

                //back to the while loop header:
                nextBlock = Blocks.B6;
            }
        }

        internal void ___AnonFun0(Application app, ___Continuation entryCtxt)
        {
            ___StackFrame retTo_1;
            List<___PRT_VALUE> locals;

            //retTo_1 = entryCtxt.PopReturnTo();
            retTo_1 = app.___Continuation.___PopReturnTo();
            //locals = retTo_1.locals;
            locals = retTo_1.___locals;
            //if ((retTo_1.pc == 0)) {
            if (retTo_1.___pc != 0)
            {
                //B7
                this.StateImpl.Exception = new Z.ZingAssertionFailureException(@"false", @"Internal error");
            }
            //B8
            //entryCtxt.Return();
            entryCtxt.___Return();
            //return;
            return;
        }
        internal void ___AnonFun1(Application app, ___Continuation entryCtxt)
        {
            ___StackFrame retTo_1;
            List<___PRT_VALUE> locals;

            //retTo_1 = entryCtxt.PopReturnTo();
            retTo_1 = app.___Continuation.___PopReturnTo();
            //locals = retTo_1.locals;
            locals = retTo_1.___locals;
            //if ((retTo_1.pc == 0)) {
            if (retTo_1.___pc != 0)
            {
                //B7
                this.StateImpl.Exception = new Z.ZingAssertionFailureException(@"false", @"Internal error");
            }
            //B8
            //entryCtxt.Return();
            entryCtxt.___Return();
            //return;
            return;
        }
        internal void ___AnonFun2(Application app, ___Continuation entryCtxt)
        {
            ___StackFrame retTo_1;
            List<___PRT_VALUE> locals;

            //retTo_1 = entryCtxt.PopReturnTo();
            retTo_1 = app.___Continuation.___PopReturnTo();
            //locals = retTo_1.locals;
            locals = retTo_1.___locals;
            //if ((retTo_1.pc == 0)) {
            if (retTo_1.___pc != 0)
            {
                //B7
                this.StateImpl.Exception = new Z.ZingAssertionFailureException(@"false", @"Internal error");
            }
            //B8
            //entryCtxt.Return();
            entryCtxt.___Return();
            //return;
            return;
        }
    }

    //############################################# class ___Main################################################
    //P program-dependent:
    //Some parts of ___Main should be generated for all machines defined in the original P program
    //class ___Main has nothing to do with the "main" in C# where the execution starts;
    //it is a class local to Application. It has some utility stuff in it.
    //Execution of the P program, according to Zing rules, starts with the methods 
    //that have the "activate" attribute; in case of P image, this is a single method
    //___Main.___Run
    //___Main is a ZingClass, because it has ___CreateMachine method in it which is a ZingMethod;
    //___CreateMachine is a ZingMethod b/c it calls ___Start, which is also a ZingMethod

    internal class ___Main : Z.ZingClass
    {
        public ___Main(Application application) : base(application) { }
        public ___Main() { }
        private ___Main(___Main c) : base(c) { }
        protected override short TypeId
        {
            get
            {
                return ___Main.typeId;
            }
        }
        private static readonly short typeId = 15;

        public override void WriteString(StateImpl state, BinaryWriter bw)
        {
            bw.Write(this.TypeId);
        }
        public override void TraverseFields(FieldTraverser ft)
        {
            ft.DoTraversal(this.TypeId);
        }
        public override object Clone()
        {
            ___Main newObj = new ___Main(this);
            {
            }
            return newObj;
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

        //TODO (question): do we have to keep ___CreateMachine as ZingMethod?
        //Possible reason: "async o_Server.Start();"
        //In the derived Main, names of the methods would be ___CreateMachine_<machine name>
        //for each machine defined in the P program
        internal class ___CreateMachine_Real1 : Z.ZingMethod
        {
            public ___CreateMachine_Real1(Application app, string machineName)
            {
                application = app;
                nextBlock = Blocks.Enter;
                handlerBlock = Blocks.None;
                locals = new LocalVars(this);
                inputs = new InputVars(this);
                outputs = new OutputVars(this);

                inputs.___machineName = machineName;
            }
            //needed as a parameter for ___MACHINE_Real1 constructor:
            public string ___machineName;
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
                    this.priv____o_Real1 = src.priv____o_Real1;
                    //Skipping fairScheduler, fairChoice business
                    //this.priv____fairScheduler = src.priv____fairScheduler;
                    //this.priv____fairChoice = src.priv____fairChoice;
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
                                return priv____o_Real1;
                            }
                            //case 1:
                            //{
                            //    return priv____fairScheduler;
                            //}
                            //case 2:
                            //{
                            //    return priv____fairChoice;
                            //}
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
                                priv____o = ((Z.Pointer)val);
                                return;
                            }
                            //case 1:
                            //    {
                            //        {
                            //            priv____fairScheduler = ((Z.Pointer)val);
                            //            return;
                            //        }
                            //    }
                            //case 2:
                            //    {
                            //        {
                            //            priv____fairChoice = ((Z.Pointer)val);
                            //            return;
                            //        }
                            //    }
                    }
                }
                public void WriteString(StateImpl state, BinaryWriter bw)
                {
                    bw.Write(state.GetCanonicalId(this.priv____o));
                    //bw.Write(state.GetCanonicalId(this.priv____fairScheduler));
                    //bw.Write(state.GetCanonicalId(this.priv____fairChoice));
                }
                public void TraverseFields(FieldTraverser ft)
                {
                    ft.DoTraversal(this.priv____o_Real1);
                    //ft.DoTraversal(this.priv____fairScheduler);
                    //ft.DoTraversal(this.priv____fairChoice);
                }
                public Z.Pointer priv____o_Real1;
                public static int id____o_Real1 = 0;
                public Z.Pointer ___o_Real1
                {
                    get
                    {
                        return priv____o_Real1;
                    }
                    set
                    {
                        SetDirty();
                        priv____o_Real1 = value;
                    }
                }
                //public Z.Pointer priv____fairScheduler;
                //public static int id____fairScheduler = 1;
                //public Z.Pointer ___fairScheduler
                //{
                //    get
                //    {
                //        return priv____fairScheduler;
                //    }
                //    set
                //    {
                //        SetDirty();
                //        priv____fairScheduler = value;
                //    }
                //}
                //public Z.Pointer priv____fairChoice;
                //public static int id____fairChoice = 2;
                //public Z.Pointer ___fairChoice
                //{
                //    get
                //    {
                //        return priv____fairChoice;
                //    }
                //    set
                //    {
                //        SetDirty();
                //        priv____fairChoice = value;
                //    }
                //}
            }
            public enum Blocks : ushort
            {
                None = 0,
                Enter = 1,
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
            public override bool RaiseZingException(int exception)
            {
                if ((handlerBlock == Blocks.None))
                {
                    return false;
                }
                this.CurrentException = exception;
                nextBlock = handlerBlock;
                return true;
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
                        case Blocks.Enter:
                            {
                                //TODO(offsets)
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
            private static readonly short typeId = 16;
            //TODO: add ___machineName to inputs
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
                    this.priv____arg = src.priv____arg;
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
                        case 0:
                            {
                                return priv____arg;
                            }
                    }
                }
                public override void SetValue(int fi, object val)
                {
                    switch (fi)
                    {
                        default:
                            {
                                {
                                    Debug.Assert(false);
                                    return;
                                }
                            }
                        case 0:
                            {
                                {
                                    priv____arg = ((Z.Pointer)val);
                                    return;
                                }
                            }
                    }
                }
                public void WriteString(StateImpl state, BinaryWriter bw)
                {
                    bw.Write(state.GetCanonicalId(this.priv____arg));
                }
                public void TraverseFields(FieldTraverser ft)
                {
                    ft.DoTraversal(this.priv____arg);
                }
                public Z.Pointer priv____arg;
                public static int id____arg = 0;
                public Z.Pointer ___arg
                {
                    get
                    {
                        return priv____arg;
                    }
                    set
                    {
                        SetDirty();
                        priv____arg = value;
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
                                priv_ReturnValue = ((Z.Pointer)val);
                                return;
                            }
                    }
                }
                public void WriteString(StateImpl state, BinaryWriter bw)
                {
                    bw.Write(state.GetCanonicalId(this.priv_ReturnValue));
                }
                public void TraverseFields(FieldTraverser ft)
                {
                    ft.DoTraversal(this.priv_ReturnValue);
                }
                public Z.Pointer priv_ReturnValue;
                public static int id_ReturnValue = 1;
                public Z.Pointer _ReturnValue
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
                public Z.Pointer _Lfc_ReturnValue
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
                ___CreateMachine_Real1 clone = new ___CreateMachine_Real1(((Application)myState));
                clone.locals = new LocalVars(clone);
                clone.locals.CopyContents(locals);
                clone.inputs = new InputVars(clone);
                clone.inputs.CopyContents(inputs);
                clone.outputs = new OutputVars(clone);
                clone.outputs.CopyContents(outputs);
                clone.nextBlock = this.nextBlock;
                clone.handlerBlock = this.handlerBlock;
                clone.SavedAtomicityLevel = this.SavedAtomicityLevel;
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
                outputs.WriteString(state, bw);
                locals.WriteString(state, bw);
            }
            public override void TraverseFields(FieldTraverser ft)
            {
                ft.DoTraversal(typeId);
                ft.DoTraversal(nextBlock);
                inputs.TraverseFields(ft);
                outputs.TraverseFields(ft);
                locals.TraverseFields(ft);
            }
            //public ___CreateMachine_Real1(Application app, Z.Pointer ___arg)
            public ___CreateMachine_Real1(Application app, Z.Pointer ___arg)
            {
                application = app;
                nextBlock = Blocks.Enter;
                handlerBlock = Blocks.None;
                locals = new LocalVars(this);
                inputs = new InputVars(this);
                outputs = new OutputVars(this);
                inputs.___arg = ___arg;
            }
            public void Enter(Z.Process p)
            {
                //.zing: o_Real1 = new MACHINE_Real1;
                locals.___o_Real1 = application.Allocate(new Z.Application.___MACHINE_Real1(application, machineName));

                //TODO: in PingPong.zing, there's an additional stmt here (for the Server machine):
                //o_Client.server = PRT_VALUE.PrtMkDefaultValue(Main.type_1_PRT_TYPE);

                var machine = (Z.Application.___MACHINE)application.LookupObject(This);
                var handle = (Z.Application.___SM_HANDLE)application.LookupObject(machine.___myHandle);
                //.zing: o_Real1.myHandle = SM_HANDLE.Construct(Machine._Real1, Real1_instance, -1);
                locals.___o_Real1.___myHandle = handle.___Construct((___Machine)1, application.globals.___Main___Real1_instance, -1);

                //.zing: SM_HANDLE.enabled = (SM_HANDLE.enabled + o_Real1.myHandle);
                var enabledSet = (ZingSet)application.LookupObject(application.globals.___SM_HANDLE____enabled);
                enabledSet.Add(locals.___o_Real1.___myHandle);

                //TODO(offsets)
                application.Trace(new ZingSourceContext(0, 6951, 7015), null, @"<CreateLog> Created Machine Real1-{0}", application.globals.___Main____Real1_instance);

                //o_Real1.myHandle.currentArg = arg;
                locals.___o_Real1.___myHandle.___currentArg = inputs.___arg;

                //Real1_instance = (Real1_instance + 1);
                application.globals.___Main____Real1_instance = (application.globals.___Main____Real1_instance + 1);

                //async o_Real1.Start();
                //No yield after "async": this is machine creation which does not require yield after it, 
                //b/c if there's "send" in the created machine's entry function, yield would be inserted
                //after the send.
                //Calling ZingMethod ___Start:
                //Add ___machineName as an argument for ___Start (or have it as a ___Start constructor
                //argument)
                Z.Application.___MACHINE_Real1.___Start callee = new Z.Application.___MACHINE_Real1.___Start(application);
                callee.This = locals.___o_Real1;
                //TODO
                application.CreateProcess(application, callee, @"___MACHINE_Real1.___Start", new ZingSourceContext(0, 7097, 7112), null);

                //.zing: invokescheduler("map", o_Real1.myHandle.machineId);
                //We are ignoring invokescheduler for now; even when it would be inserted here,
                //no yield is needed
                //application.InvokeScheduler(@"map", locals.___o.___myHandle.___machineId);

                //return o_Real1.myHandle;
                outputs._ReturnValue = (((Z.Application.___MACHINE_Real1)application.LookupObject(locals.___o))).___myHandle;
                //TODO(offsets)
                p.Return(new ZingSourceContext(0, 7166, 7190), null);
                StateImpl.IsReturn = true;
            }
        }

        //P program-dependent;
        //Adds a specific event defined in P to the _eventSet parameter, by an explicit "if" stmt
        //for each existing event
        //Template below is only valid for OneDummyMachine.p: no defined events, only default ones
        //Converting to regular C# method
        //TODO (for more complex test with user-defined events): 
        //re-write this method as a template by using C# set compliment operator
        //and a universal set of all events in P program
        //TODO(question): what if argument eventSet is located on the Zing heap, i.e., is a Z.Pointer
        //(as in old compiler)? In that case, we would have to locate it with LookupObject 
        static internal sealed SM_EVENT_SET ___CalculateComplementOfEventSet(Application app, List<___SM_EVENT> eventSet)
        {
            //Z.Pointer returnEventSet;
            List<___SM_EVENT> returnEventSet;

            //returnEventSet = new SM_EVENT_SET;
            //at least null event belongs to this set:
            returnEventSet = new List<___SM_EVENT>(1);
            //if ((Main.null_SM_EVENT in eventSet)) {} else
            //{ returnEventSet = (returnEventSet + Main.null_SM_EVENT); }
            if (!(eventSet.IsMember(app.globals.___Main____null_SM_EVENT)))
            {
                returnEventSet.Add(app.globals.___Main____null_SM_EVENT);
            }

            //return returnEventSet;
            return returnEventSet;
        }
        //P program-dependent;
        //Returns type of the payload for a specific event, by an explicit "if" stmt
        //for each existing event
        //Template below is only valid for OneDummyMachine.p: no defined events, only default ones
        //Converting to regular C# method
        ////TODO (for more complex test with user-defined events): 
        //re-write this method as a template: use payload type as a field in the SM_EVENT class 
        static internal sealed PRT_TYPE ___PayloadOf(Application app, Z.Pointer e)
        {
            //if ((e == null)) {
            if (e == 0)
            {
                //return Main.type_0_PRT_TYPE;
                return app.globals.___Main____type_0_PRT_TYPE;
            }
            //((e.name == Event._halt))
            //TODO(quesiton): Is LookupObject needed here?
            //if ((((Z.Application.___SM_EVENT)app.LookupObject(e))).___name == (___Event)1)
            if (e.___name == (___Event)1)
            {
                //return Main.type_0_PRT_TYPE;
                return app.globals.___Main____type_0_PRT_TYPE;
            }
            this.StateImpl.Exception = new Z.ZingAssertionFailureException(@"false", @"Internal error");
        }
        //Keeping ___Run as ZingMethod: calls ___CreateMachine_XX, which is a ZingMethod
        //___Run in Main is P program dependent and will have to be generated
        //Note: this is the class to start execution ("Z.Activate())
        [Z.Activate()]
        internal sealed class ___Run : Z.ZingMethod
        {
            public ___Run(Application app)
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
                    this.priv____nullValue = src.priv____nullValue;
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
                                return priv____nullValue;
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
                                priv____nullValue = ((Z.Pointer)val);
                                return;
                            }
                    }
                }
                public void WriteString(StateImpl state, BinaryWriter bw)
                {
                    bw.Write(state.GetCanonicalId(this.priv____nullValue));
                }
                public void TraverseFields(FieldTraverser ft)
                {
                    ft.DoTraversal(this.priv____nullValue);
                }
                public Z.Pointer priv____nullValue;
                public static int id____nullValue = 0;
                public Z.Pointer ___nullValue
                {
                    get
                    {
                        return priv____nullValue;
                    }
                    set
                    {
                        SetDirty();
                        priv____nullValue = value;
                    }
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
            public override bool RaiseZingException(int exception)
            {
                if ((handlerBlock == Blocks.None))
                {
                    return false;
                }
                this.CurrentException = exception;
                nextBlock = handlerBlock;
                return true;
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
                    case Blocks.B0:
                        {
                            B0(p);
                            break;
                        }
                    default:
                        {
                            throw new ApplicationException();
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
                                return new ZingSourceContext(0, 8194, 8195);
                            }
                        case Blocks.Enter:
                            {
                                return new ZingSourceContext(0, 7741, 7803);
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
            private static readonly short typeId = 19;
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
                public void TraverseFields(FieldTraverser ft)
                {
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
                ___Run clone = new ___Run(((Application)myState));
                clone.locals = new LocalVars(clone);
                clone.locals.CopyContents(locals);
                clone.inputs = new InputVars(clone);
                clone.inputs.CopyContents(inputs);
                clone.outputs = new OutputVars(clone);
                clone.outputs.CopyContents(outputs);
                clone.nextBlock = this.nextBlock;
                clone.handlerBlock = this.handlerBlock;
                clone.SavedAtomicityLevel = this.SavedAtomicityLevel;
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
                outputs.WriteString(state, bw);
                locals.WriteString(state, bw);
            }
            public override void TraverseFields(FieldTraverser ft)
            {
                ft.DoTraversal(typeId);
                ft.DoTraversal(nextBlock);
                inputs.TraverseFields(ft);
                outputs.TraverseFields(ft);
                locals.TraverseFields(ft);
            }
            public void Enter(Z.Process p)
            {
                //dummy:
                //Main.halt_SM_EVENT = SM_EVENT.Construct(Event._halt, 1, false);
                //___SM_EVENT is now a regular C# class
                application.globals.___Main____halt_SM_EVENT = application.___SM_EVENT.___Construct((___Event)1, app.globals.___Main____type_0_PRT_TYPE, 1, false);

                //Main.null_SM_EVENT = null;
                application.globals.___Main____null_SM_EVENT = 0;

                //Stmt below depends on: 
                //init state name for a particular machine M;
                //actions and functions defined in this init state
                //TODO: such stmts should be generated for all machines defined in the source P program
                // - look at .cs generated for P with two machines for a template
                //.zing:Main.Real1_Init_SM_STATE = SM_STATE.Construct(State._Real1_Init, ActionOrFun._Real1_AnonFun1, ActionOrFun._Real1_AnonFun2, 0, false, StateTemperature.Warm);
                application.globals.___Main____Real1_Init_SM_STATE =
                    application.___SM_EVENT.___Construct((___State)1, (___ActionOrFun)3, (___ActionOrFun)4, 0, false, (___StateTemperature)1);

                //Real1_instance = 0;
                //application.globals.___Main____Real1_instance = 0;
                application.globals.___Main_instance = 0;

                //Main.type_0_PRT_TYPE = PRT_TYPE.PrtMkPrimitiveType(PRT_TYPE_KIND.PRT_KIND_NULL);
                application.globals.___Main____type_0_PRT_TYPE = application.___PRT_TYPE.___PrtMkPrimitiveType((___PRT_TYPE_KIND)8);

                //nullValue = PRT_VALUE.PrtMkDefaultValue(Main.type_0_PRT_TYPE);
                locals.___nullValue = application.___PRT_VALUE.___PrtMkDefaultValue(application.globals.___Main____type_0_PRT_TYPE);

                //Main.CreateMachine_Real1(nullValue);
                //Calling ZingMethod ___CreateMachineXX
                Z.Application.___Main.___CreateMachine callee = new Z.Application.___Main.___CreateMachine(application);
                callee.inputs.priv____arg = locals.___nullValue;
                p.Call(callee);
                StateImpl.IsCall = true;

                nextBlock = Blocks.B0;
            }
            public void Enter(Z.Process p)
            {
                p.LastFunctionCompleted = null;
                //TODO(offsets)
                p.Return(new ZingSourceContext(0, 8194, 8195), null);
                StateImpl.IsReturn = true;
            }
        }
    }   //for class ___Main

}
}