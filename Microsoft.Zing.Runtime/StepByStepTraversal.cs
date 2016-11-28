/* Step by Step Traversal */

using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;

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
            base(s, StateType.TerminalState, pred, bt)
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
            if (ZingerConfiguration.FingerprintSingleTransitionStates)
            {
                // Fingerprint with probability p
                if (ZingerUtilities.rand.NextDouble() <= ZingerConfiguration.NonChooseProbability)
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
            Predecessor.Successor = this;
        }

        public override void Reset()
        {
        }

        public override TraversalInfo GetNextSuccessorUnderDelayZeroForRW()
        {
            return null;
        }

        public override TraversalInfo GetNextSuccessorUniformRandomly()
        {
            return null;
        }

        public override TraversalInfo GetDelayedSuccessor()
        {
            return null;
        }
        public override TraversalInfo GetNextSuccessor()
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

        public ushort NumChoices { get { return numChoices; } }

        public ChooseState(StateImpl s,
            TraversalInfo predecessor, Via bt)
            : base(s, StateType.ChooseState, predecessor, bt)
        {
            numChoices = s.NumChoices;
            hasMultipleSuccessors = s.NumChoices > 1;

            stateImpl = s;

            receipt = s.CheckIn();
            if (!ZingerConfiguration.DoRandomSampling)
            {
                if (ZingerConfiguration.FingerprintSingleTransitionStates)
                {
                    if (this.NumChoices > 1)
                    {
                        this.fingerprint = s.Fingerprint;
                        this.IsFingerPrinted = true;
                    }
                    else
                    {
                        // Fingerprint with probability p
                        if (ZingerUtilities.rand.NextDouble() <= ZingerConfiguration.NonChooseProbability)
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
            if (!ZingerConfiguration.DoRandomSampling)
            {
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
            }
        }

        
        internal override void deOrphanize(StateImpl s)
        {
            Debug.Assert(numChoices == s.NumChoices);
            receipt = s.CheckIn();
            Predecessor.Successor = this;
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

        #region Random Walk

        public override TraversalInfo GetNextSuccessorUniformRandomly()
        {
            var nextChoice = ZingerUtilities.rand.Next(0, numChoices);
            return RunChoice(nextChoice);
        }

        public override TraversalInfo GetNextSuccessorUnderDelayZeroForRW()
        {
            return GetNextSuccessorUniformRandomly();
        }

        #endregion Random Walk

        public override TraversalInfo GetDelayedSuccessor()
        {
            if (currChoice >= numChoices)
                return null;

            //if the final cutoff is already exceeded then return null !
            if (ZingerConfiguration.BoundChoices && ZingerConfiguration.zBoundedSearch.FinalChoiceCutOff < zBounds.ChoiceCost)
                return null;

            TraversalInfo retVal = RunChoice(currChoice++);

            //increment the choice cost for choice bounding.
            retVal.zBounds.ChoiceCost = zBounds.ChoiceCost + 1;
            //increment the delay budget when using delaying explorers
            if (ZingerConfiguration.DoDelayBounding)
                retVal.zBounds.IncrementDelayCost();

            return retVal;
        }
        public override TraversalInfo GetNextSuccessor()
        {
            if (currChoice >= numChoices)
                return null;

            //if the final cutoff is already exceeded then return null !
            if (ZingerConfiguration.BoundChoices && ZingerConfiguration.zBoundedSearch.FinalChoiceCutOff < zBounds.ChoiceCost)
                return null;

            TraversalInfo retVal = RunChoice(currChoice++);

            if (doDelay)
            {
                //increment the choice cost for choice bounding.
                retVal.zBounds.ChoiceCost = zBounds.ChoiceCost + 1;
                //increment the delay budget when using delaying explorers
                if (ZingerConfiguration.DoDelayBounding)
                    retVal.zBounds.IncrementDelayCost();
            }
            else
            {
                doDelay = true;
            }

            return retVal;
        }

        protected override void Replay(TraversalInfo succ, Via bt)
        {
            ViaChoose vc = (ViaChoose)bt;

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

        public ExecutionState(StateImpl s, TraversalInfo predecessor, Via bt)
            : base(s, StateType.ExecutionState, predecessor, bt)
        {
            Debug.Assert(ProcessInfo != null &&
                ProcessInfo.Length == NumProcesses);
            hasMultipleSuccessors = NumSuccessors() > 1;
            stateImpl = s;
            currProcess = 0;
            receipt = s.CheckIn();

#if true
            //dont fingerprint during random sampling
            if (!ZingerConfiguration.DoRandomSampling)
            {
                if (ZingerConfiguration.FingerprintSingleTransitionStates)
                {
                    if (this.NumProcesses > 1)
                    {
                        this.fingerprint = s.Fingerprint;
                        this.IsFingerPrinted = true;
                    }
                    else
                    {
                        // Fingerprint with probability p
                        if (ZingerUtilities.rand.NextDouble() <= ZingerConfiguration.NonChooseProbability)
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

            if (!ZingerConfiguration.DoRandomSampling)
            {
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
        }

        internal override void deOrphanize(StateImpl s)
        {
            Debug.Assert(NumProcesses == s.NumProcesses);
            receipt = s.CheckIn();
            Predecessor.Successor = this;
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

        public override TraversalInfo GetSuccessorN(int n)
        {
            if (n >= NumProcesses)
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

        #region RandomWalk

        public override TraversalInfo GetNextSuccessorUniformRandomly()
        {
            return GetNextSuccessor();
        }

        public override TraversalInfo GetDelayedSuccessor()
        {
            Contract.Assert(ZingerConfiguration.DoDelayBounding);
            ZingDBScheduler.Delay(ZingDBSchedState);
            int nextProcess;
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
                return null;

            return RunProcess(nextProcess);
        }

        public override TraversalInfo GetNextSuccessorUnderDelayZeroForRW()
        {
            Contract.Assert(ZingerConfiguration.DoDelayBounding);

            int nextProcess = -1;
            //get the runnable process
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
                return null;

            return RunProcess(nextProcess);
        }

        #endregion RandomWalk

        public override TraversalInfo GetNextSuccessor()
        {
            if (ZingerConfiguration.DoDelayBounding)
            {
                //
                // if we have delayed the scheduler for the current state more than maxdelay then we should
                // not call delay on this state again because we have explored all its successors
                //
                if ((doDelay && ZingDBScheduler.MaxDelayReached(ZingDBSchedState)))
                {
                    return null;
                }

                int nextProcess = -1;
                if (doDelay)
                {
                    ZingDBScheduler.Delay(ZingDBSchedState);
                }
                while ((nextProcess = ZingDBScheduler.Next(ZingDBSchedState)) != -1)
                {
                    if (ProcessInfo[nextProcess].Status == ProcessStatus.Completed)
                    {
                        ZingDBScheduler.Finish(ZingDBSchedState, nextProcess);
                        continue;
                    }
                    else if (ProcessInfo[nextProcess].Status != ProcessStatus.Runnable)
                    {
                        ZingDBScheduler.Delay(ZingDBSchedState);
                        continue;
                    }
                    else
                    {
                        break;
                    }
                }

                //have explored all succ
                if (nextProcess == -1)
                {
                    return null;
                }

                TraversalInfo retVal = RunProcess(nextProcess);
                if (!doDelay)
                    doDelay = true;
                else
                    retVal.zBounds.IncrementDelayCost();

                return retVal;
            }
            else if (ZingerConfiguration.DoPreemptionBounding)
            {
                int executeProcess;
                if (doDelay)
                {
                    if (!preemptionBounding.preempted)
                    {
                        preemptionBounding.preempted = true;
                        zBounds.IncrementPreemptionCost();
                    }
                    executeProcess = preemptionBounding.GetNextProcessToExecute();
                    if (executeProcess == -1)
                        return null;
                }
                else
                {
                    doDelay = true;
                    executeProcess = preemptionBounding.currentProcess;
                    if (ProcessInfo[executeProcess].Status != ProcessStatus.Runnable)
                    {
                        while (ProcessInfo[executeProcess].Status != ProcessStatus.Runnable)
                        {
                            executeProcess = preemptionBounding.GetNextProcessToExecute();
                            if (executeProcess == -1)
                            {
                                return null;
                            }
                        }
                    }
                }

                return RunProcess(executeProcess);
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

        protected override void Replay(TraversalInfo succ, Via bt)
        {
            ViaExecute ve = (ViaExecute)bt;

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