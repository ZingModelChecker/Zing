/* Step by Step Traversal */

using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using System.Collections.Generic;
using System.IO;



namespace Microsoft.Zing
{
    
    public class JiGlobal
    { 
        public static StateImpl JiState;
    }


   
    public sealed class TerminalState : TraversalInfo
    {
        public readonly Exception Error;
        public readonly bool IsErroneous;
        public readonly bool IsFailedAssumption;
        public readonly bool IsValidTermination;
        public readonly bool IsAborted;

        public TerminalState(StateImpl s, TraversalInfo pred, Via bt) :
            base (s, StateType.TerminalState, pred, bt) 
        {
            hasMultipleSuccessors = false;
            if (s.IsErroneous) 
            {
                IsErroneous = true;
                Error = s.Exception;
            }
            else if (s.IsFailedAssumption) 
            {
                IsFailedAssumption = true;
                Error = s.Exception;
            }
            else if (s.IsValidTermination) 
            {
                IsValidTermination = true;
            }
            
            stateImpl = s;

            receipt = s.CheckIn();

#if true
            if (Options.FingerprintSingleTransitionStates)
            {
                // Fingerprint with probability p
                if (ZingRNG.GetUniformRV() <= Options.NonChooseProbability)
                {
                    this.fingerprint = s.Fingerprint;
                    this.IsFingerPrinted = true;
                }
                else
                {
                    this.fingerprint = null;
                    this.IsFingerPrinted = false;
                }
            }
            else
            {
                this.fingerprint = s.Fingerprint;
                this.IsFingerPrinted = true;
            }
#endif
        }

        public TerminalState(StateImpl s, TraversalInfo pred, Via bt,
            bool MustFingerprint)
            : base(s, StateType.TerminalState, pred, bt)
        {
            hasMultipleSuccessors = false;
            if (s.IsErroneous)
            {
                IsErroneous = true;
                Error = s.Exception;
            }
            else if (s.IsFailedAssumption)
            {
                IsFailedAssumption = true;
                Error = s.Exception;
            }
            else if (s.IsValidTermination)
            {
                IsValidTermination = true;
            }

            stateImpl = s;
            receipt = s.CheckIn();

            if (MustFingerprint)
            {
                this.fingerprint = s.Fingerprint;
                this.IsFingerPrinted = true;
            }
            else
            {
                this.fingerprint = null;
                this.IsFingerPrinted = false;
            }
        }

        protected override void Replay(TraversalInfo succ, Via bt)
        {
            throw new ArgumentException("cannot replay from a terminal node");
        }

        internal override void deOrphanize(StateImpl s)
        {
            receipt = s.CheckIn();
            Predecessor.successor = this;
        }

        public override void Reset() {}

        public override TraversalInfo GetNextSuccessor(ZingBoundedSearch zbs)
        {
            return null;
        }

        public override TraversalInfo RandomSuccessor()
        {
            return null;
        }

        public override TraversalInfo GetSuccessorN(int n)
        {
            return null;
        }

        public override TraversalInfo GetSuccessorNForReplay(int n, bool MustFingerprint)
        {
            return null;
        }

        public override ushort NumSuccessors()
        {
            return 0;
        }
    }

    internal sealed class ChooseState : TraversalInfo
    {
        private ushort numChoices;
        private int currChoice;
        private List<int> randomChoiceSet;
        public ushort NumChoices { get { return numChoices; } }

        public ChooseState(StateImpl s,
            TraversalInfo predecessor, Via bt)
            : base (s, StateType.ChooseState, predecessor, bt)
        {
            numChoices = s.NumChoices;
            hasMultipleSuccessors = s.NumChoices > 1;

            stateImpl = s;

            
            receipt = s.CheckIn();

            if (Options.FingerprintSingleTransitionStates)
            {
                if (this.NumChoices > 1)
                {
                    this.fingerprint = s.Fingerprint;
                    this.IsFingerPrinted = true;
                }
                else
                {
                    // Fingerprint with probability p
                    if (ZingRNG.GetUniformRV() <= Options.NonChooseProbability)
                    {
                        this.fingerprint = s.Fingerprint;
                        this.IsFingerPrinted = true;
                    }
                    else
                    {
                        this.fingerprint = null;
                        this.IsFingerPrinted = false;
                    }
                }
            }
            else
            {
                this.fingerprint = s.Fingerprint;
                this.IsFingerPrinted = true;
            }

            if (Options.IsRandomSearch)
            {
                randomChoiceSet = new List<int>();
                //initialize the set
                for (int i = 0; i < numChoices; i++)
                {
                    randomChoiceSet.Add(i);
                }
            }
        }

        public ChooseState(StateImpl s, TraversalInfo predecessor,
            Via bt, bool MustFingerprint)
            : base(s, StateType.ChooseState, predecessor, bt)
        {
            numChoices = s.NumChoices;
            stateImpl = s;
            hasMultipleSuccessors = s.NumChoices > 1;
           
            receipt = s.CheckIn();


            if (MustFingerprint)
            {
                fingerprint = s.Fingerprint;
                this.IsFingerPrinted = true;
            }
            else
            {
                this.fingerprint = null;
                this.IsFingerPrinted = false;
            }

            if (Options.IsRandomSearch)
            {
                randomChoiceSet = new List<int>();
                //initialize the set
                for (int i = 0; i < numChoices; i++)
                {
                    randomChoiceSet.Add(i);
                }
            }
        }

        internal override void deOrphanize (StateImpl s)
        {
            Debug.Assert(numChoices == s.NumChoices);
            receipt = s.CheckIn();
            Predecessor.successor = this;
        }

        public TraversalInfo RunChoice(int n)
        {
            if (n >= numChoices)
                throw new ArgumentException("invalid successor (choose)");
            
            StateImpl s;
           
            s = reclaimState();
            s.RunChoice(n);

            JiGlobal.JiState = null;
            JiGlobal.JiState = s;

            ViaChoose v = new ViaChoose(n);
            TraversalInfo ti = TraversalInfo.MakeTraversalInfo(s, this, v);            
            return ti;
        }

        public TraversalInfo RunChoice(int n, bool MustFingerprint)
        {
            if (n >= numChoices)
            {
                throw new ArgumentException("invalid successor (choose)");
            }

            StateImpl s;
            
            s = reclaimState();
            s.RunChoice(n);

            JiGlobal.JiState = null;
            JiGlobal.JiState = s;

            ViaChoose v = new ViaChoose(n);
            TraversalInfo ti = TraversalInfo.MakeTraversalInfo(s, this, v, MustFingerprint);
            return ti;
        }

        public override TraversalInfo RandomSuccessor()
        {
            if (randomChoiceSet.Count == 0)
                return null;
            else
            {
                var currC = randomChoiceSet[TraversalInfo.randGen.Next(0, randomChoiceSet.Count)];
                randomChoiceSet.Remove(currC);
                return RunChoice(currC);
            }
        }

        public override TraversalInfo GetNextSuccessor(ZingBoundedSearch zbs)
        {
            if (Options.IsRandomSearch)
            {
                if (randomChoiceSet.Count == 0)
                    return null;
                else
                {
                    var currC = randomChoiceSet[TraversalInfo.randGen.Next(0, randomChoiceSet.Count)];
                    randomChoiceSet.Remove(currC);
                    return RunChoice(currC);
                }
            }
            else
            {
                

                if (Options.BoundChoices)
                {
                    if (currChoice >= numChoices)
                        return null;

                    if (doDelay)
                    {
                        Bounds.ChoiceCost = Bounds.ChoiceCost + 1;
                        numOfTimesCurrStateDelayed++;
                    }
                    else
                    {
                        doDelay = true;
                    }

                    if (zbs.checkIfIterativeCutOffReached(Bounds))
                        return this;

                }

                if (currChoice >= numChoices)
                    return null;

                return RunChoice(currChoice++);
                
            }
        }

        protected override void Replay(TraversalInfo succ, Via bt)
        {
            ViaChoose vc = (ViaChoose) bt;

            StateImpl s = this.reclaimState();
            s.RunChoice(vc.ChoiceNumber);
            succ.deOrphanize(s);
        }

        public override void Reset()
        {
            currChoice = 0;
        }

        public override TraversalInfo GetSuccessorN(int n)
        {
            if (n >= numChoices)
                return null;
                
            return RunChoice(n);
        }

        public override TraversalInfo GetSuccessorNForReplay(int n, bool MustFingerprint)
        {
            if (n >= numChoices)
            {
                return null;
            }

            return RunChoice(n, MustFingerprint);
        }

        public override ushort NumSuccessors()
        {
            return numChoices;
        }
    }

    public sealed class ExecutionState : TraversalInfo
    {
        private int currProcess;
        private List<int> RandomProcessSet;

        public ExecutionState(StateImpl s, TraversalInfo predecessor, Via bt)
            : base(s, StateType.ExecutionState, predecessor, bt)
        {
            Debug.Assert(ProcessInfo != null && 
                ProcessInfo.Length == NumProcesses);
            hasMultipleSuccessors = NumSuccessors() > 1;
            stateImpl = s;
            
            receipt = s.CheckIn();

#if true
            if (Options.FingerprintSingleTransitionStates)
            {
                if (this.NumProcesses > 1)
                {
                    this.fingerprint = s.Fingerprint;
                    this.IsFingerPrinted = true;
                }
                else
                {
                    // Fingerprint with probability p
                    if (ZingRNG.GetUniformRV() <= Options.NonChooseProbability)
                    {
                        this.fingerprint = s.Fingerprint;
                        this.IsFingerPrinted = true;
                    }
                    else
                    {
                        this.fingerprint = null;
                        this.IsFingerPrinted = false;
                    }
                }
            }
            else
            {
                this.fingerprint = s.Fingerprint;
                this.IsFingerPrinted = true;
            }
            if (Options.IsRandomSearch)
            {
                RandomProcessSet = new List<int>();
                for (int i = 0; i < NumProcesses; i++)
                {
                    if (ProcessInfo[i].Status == RUNNABLE)
                    {
                        RandomProcessSet.Add(i);
                    }
                }
            }
#endif
        }

        public ExecutionState(StateImpl s, TraversalInfo predecessor, Via bt,
            bool MustFingerprint)
            : base(s, StateType.ExecutionState, predecessor, bt)
        {
            Debug.Assert(ProcessInfo != null &&
                        ProcessInfo.Length == NumProcesses);

            stateImpl = s;
            hasMultipleSuccessors = NumSuccessors() > 1;
            receipt = s.CheckIn();


            if (MustFingerprint)
            {
                this.fingerprint = s.Fingerprint;
                this.IsFingerPrinted = true;
            }
            else
            {
                this.fingerprint = null;
                this.IsFingerPrinted = false;
            }

            if (Options.IsRandomSearch)
            {
                RandomProcessSet = new List<int>();
                for (int i = 0; i < NumProcesses; i++)
                {
                    if (ProcessInfo[i].Status == RUNNABLE)
                    {
                        RandomProcessSet.Add(i);
                    }
                }
            }

        }

        internal override void deOrphanize(StateImpl s)
        {
            Debug.Assert(NumProcesses == s.NumProcesses);
            receipt = s.CheckIn();
            Predecessor.successor = this;
        }

        public TraversalInfo RunProcess(int n)
        {
            StateImpl s;
            
            s = reclaimState();
            s.RunProcess(n);
           

            JiGlobal.JiState = null;
            JiGlobal.JiState = s;

            ViaExecute v = new ViaExecute(n);
            TraversalInfo ti = TraversalInfo.MakeTraversalInfo(s, this, v);
            return ti;
        }

        public TraversalInfo RunProcess(int n, bool MustFingerprint)
        {
            StateImpl s;
            
            s = reclaimState();
            s.RunProcess(n);

            JiGlobal.JiState = null;
            JiGlobal.JiState = s;

            ViaExecute v = new ViaExecute(n);
            TraversalInfo ti = TraversalInfo.MakeTraversalInfo(s, this, v, MustFingerprint);
            return ti;
        }

        public override ushort NumSuccessors() 
        {
            ushort i, cnt = 0;
                    
            for (i = 0; i < NumProcesses; i++)
                if (ProcessInfo[i].Status == RUNNABLE)
                    cnt++;

            return cnt;
        }

        /*
        public bool IsRunnable(int p)
        {
            Debug.Assert(p < NumProcesses);
            return (ProcessInfo[p].Status == RUNNABLE);
        }
        */
        public override TraversalInfo GetSuccessorN(int n)
        {
            if(n >= NumProcesses)
                return null;

            return RunProcess(n);
        }

        public override TraversalInfo GetSuccessorNForReplay(int n, bool MustFingerprint)
        {
            if (n >= NumProcesses)
            {
                return null;
            }

            return RunProcess(n, MustFingerprint);
        }

        public override TraversalInfo RandomSuccessor()
        {
            if (RandomProcessSet.Count == 0)
            {
                return null;
            }
            else
            {
                var currProcess = RandomProcessSet[TraversalInfo.randGen.Next(0, RandomProcessSet.Count)];
                RandomProcessSet.Remove(currProcess);
                return RunProcess(currProcess);
            }
        }

        public override TraversalInfo GetNextSuccessor(ZingBoundedSearch zbs)
        {
            if (Options.IsRandomSearch)
            {
                if(RandomProcessSet.Count == 0)
                {
                    return null;
                }
                else
                {
                    var currProcess = RandomProcessSet[TraversalInfo.randGen.Next(0, RandomProcessSet.Count)];
                    RandomProcessSet.Remove(currProcess);
                    return RunProcess(currProcess);
                }
            }
            else
            {
                if (Options.IsSchedulerDecl)
                {
                    int maxDelay = ZingDBScheduler.MaxDelay(ZingDBSchedState);
                    //
                    // if we have delayed the scheduler for the current state more than maxdelay then we should 
                    // not call delay on this state again because we have explored all its successors
                    //
                    if ((doDelay && numOfTimesCurrStateDelayed >= maxDelay) || (doDelay && ZingDBScheduler.IsSealed(ZingDBSchedState)))
                    {
                        return null;
                    }

                    int nextProcess = -1;
                    if (doDelay)
                    {
                        ZingDBScheduler.Delay(ZingDBSchedState);
                        Bounds.Delay = Bounds.Delay + 1;
                        numOfTimesCurrStateDelayed++;
                    }


                    while ((nextProcess = ZingDBScheduler.Next(ZingDBSchedState)) != -1)
                    {

                        if (ProcessInfo[nextProcess].Status != RUNNABLE)
                        {
                            ZingDBScheduler.Delay(ZingDBSchedState);
                            continue;
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (nextProcess == -1)
                    {
                        return null;
                    }

                    if (zbs.checkIfIterativeCutOffReached(Bounds))
                        return this;
                    else
                    {
                        doDelay = true;
                        return RunProcess(nextProcess);
                    }
                }
                else
                {
                    while (currProcess < NumProcesses &&
                        ProcessInfo[currProcess].Status != RUNNABLE)
                        currProcess++;

                    if (currProcess >= NumProcesses)
                        return null;

                    return RunProcess(currProcess++);
                }
            }
        }

        protected override void Replay(TraversalInfo succ, Via bt)
        {
            ViaExecute ve = (ViaExecute) bt;
            
            StateImpl s = this.reclaimState();
            s.RunProcess(ve.ProcessExecuted);
            succ.deOrphanize(s);
        }

        public override void Reset()
        {
            currProcess = 0;
        }
    }

    
}
