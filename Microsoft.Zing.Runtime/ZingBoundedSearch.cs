using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.Zing
{
    [Serializable]
    public class ZingBounds
    {
        public Int32 Depth;
        public Int32 Delay;
        public Int32 ChoiceCost;
        public ZingBounds(Int32 dpt, Int32 dly, Int32 cb)
        {
            Depth = dpt;
            Delay = dly;
            ChoiceCost = cb;
        }

        public ZingBounds()
        {
            Depth = 0;
            Delay = 0;
            ChoiceCost = 0;
        }
    }
    public class ZingBoundedSearch
    {
        #region Bounds
        public int depthCutOff;
        public int delayCutOff;
        public int choiceCutOff;
        public int iterativeDepthCutoff;
        public int iterativeDepthIncrement;
        public int iterativeDelayCutOff;
        public int iterativeDelayIncrement;
        public int iterativeChoiceIncrement;
        public int iterativeChoiceCutOff;
        #endregion

        #region Contructor
        public ZingBoundedSearch(int idepth, int idelay, int ichoice, int cdepth, int cdelay, int cchoice)
        {
            delayCutOff = cdelay;
            depthCutOff = cdepth;
            choiceCutOff = cchoice;
            iterativeDepthIncrement = idepth;
            iterativeDelayIncrement = idelay;
            iterativeChoiceIncrement = ichoice;
            iterativeChoiceCutOff = cchoice;
            iterativeDelayCutOff = 0;
            iterativeDepthCutoff = 0;
        }
        #endregion

        #region Functions
        public bool checkIfFinalCutOffReached()
        {
            if (Options.IsSchedulerDecl)
            {
                if ((iterativeDelayCutOff >= delayCutOff))
                {
                    return true;
                }
                else
                {
                    return false;

                }
            }
            else
            {
                if ((iterativeDepthCutoff >= depthCutOff))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public bool checkIfIterativeCutOffReached(ZingBounds currBounds)
        {
            if (Options.IsSchedulerDecl)
            {
                if ((currBounds.Delay >= iterativeDelayCutOff) || (Options.BoundChoices && currBounds.ChoiceCost >= iterativeChoiceCutOff))
                {
                    return true;
                }
                else
                {
                    return false;

                }
            }
            else
            {
                if ((currBounds.Depth >= iterativeDepthCutoff) || (Options.BoundChoices && currBounds.ChoiceCost >= iterativeChoiceCutOff))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }    
        }

        public void IncrementIterativeBound()
        {
            if(Options.IsSchedulerDecl)
            {
                iterativeDelayCutOff += iterativeDelayIncrement;
                iterativeDelayCutOff = Math.Min(delayCutOff, iterativeDelayCutOff);
                
            }
            else
            {
                iterativeDepthCutoff += iterativeDepthIncrement;
                iterativeDepthCutoff = Math.Min(depthCutOff, iterativeDepthCutoff);
            }
            if (Options.BoundChoices)
            {
                //iterativeChoiceCutOff += iterativeChoiceIncrement;
                iterativeChoiceCutOff = Math.Min(choiceCutOff, iterativeChoiceCutOff);
            }
        }
        #endregion
    }

    public class MaceLiveness
    {
        static public int ExhaustiveSearchBound = 15;
        static public int RandomWalkBound = 1000;
        static public int FinalBound = 100000;
    }
}
