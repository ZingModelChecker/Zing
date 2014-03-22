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

        public ZingBounds(Int32 dpt, Int32 dly)
        {
            Depth = dpt;
            Delay = dly;
        }

        public ZingBounds()
        {
            Depth = 0;
            Delay = 0;
        }
    }
    public class ZingBoundedSearch
    {
        #region Bounds
        public int depthCutOff;
        public int delayCutOff;
        public int iterativeDepthCutoff;
        public int iterativeDepthIncrement;
        public int iterativeDelayCutOff;
        public int iterativeDelayIncrement;
        #endregion

        #region Contructor
        public ZingBoundedSearch(int idepth, int idelay, int cdepth, int cdelay)
        {
            delayCutOff = cdelay;
            depthCutOff = cdepth;
            iterativeDepthIncrement = idepth;
            iterativeDelayIncrement = idelay;
            iterativeDelayCutOff = 0;
            iterativeDepthCutoff = 0;
        }
        #endregion

        #region Functions
        public bool checkIfFinalCutOffReached()
        {
            if (Options.IsSchedulerDecl)
            {
                if (iterativeDelayCutOff >= delayCutOff)
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
                if (iterativeDepthCutoff >= depthCutOff)
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
                if (currBounds.Delay >= iterativeDelayCutOff)
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
                if (currBounds.Depth >= iterativeDepthCutoff)
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
