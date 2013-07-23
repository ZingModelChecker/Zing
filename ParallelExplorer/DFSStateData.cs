
using System;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;


namespace Microsoft.Zing
{

    [Serializable]
    public class DFSLiveState
    {

        private bool magicBit;

        public bool MagicBit
        {
            get { return magicBit; }
            set { magicBit = value; }
        }

        private int exploreIfDepthLowerThan;
        public int ExploreIfDepthLowerThan
        {
            get { return (exploreIfDepthLowerThan >> 1); }
            set
            {
                bool temp = (exploreIfDepthLowerThan % 2 != 0);
                exploreIfDepthLowerThan = value;
                exploreIfDepthLowerThan <<= 1;
                exploreIfDepthLowerThan |= temp ? 0x1 : 0x0;
            }
        }
        public bool CompletelyExplored
        {
            get { return (exploreIfDepthLowerThan % 2 != 0); }
            set { exploreIfDepthLowerThan |= (value ? 0x1 : 0x0); }
        }
        public DFSLiveState()
        { }
        public DFSLiveState(int ExploreIfDepthLowerThan, bool CompletelyExplored, bool magicBit)
        {
            this.MagicBit = magicBit;
            this.ExploreIfDepthLowerThan = ExploreIfDepthLowerThan;
            this.CompletelyExplored = CompletelyExplored;
        }
    }

    public class IDBDFSStackEntry
    {
        public TraversalInfo ti;
        public ushort DescendentsLeftToCover;
        public int ExploreIfDepthLowerThan;

        public IDBDFSStackEntry(TraversalInfo ti)
        {
            this.ti = ti;
            this.DescendentsLeftToCover = ti.NumSuccessors();
            this.ExploreIfDepthLowerThan = -1;
        }
    }


}