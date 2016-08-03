
using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;

namespace P.PRuntime
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
        public Fun<T> fun; // isPush <==> fun == null
        public State<T> to;

        public Transition(Fun<T> fun, State<T> to)
        {
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
                        throw new PrtAssumeFailureException();
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

            deferredSet = owner.stateStack.deferredSet;
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


            deferredSet = owner.stateStack.deferredSet;
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