/***********************************************************************************
 * Fingerprint.cs -- Implementation of Stern & Dill's probabilistic hash compaction *
 **********************************************************************************/
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace Microsoft.Zing
{
    /// <summary>
    /// This class supports the Zing state fingerprinting scheme.
    /// </summary>
    /// 
    [Serializable]
    public class Fingerprint : ICloneable, IComparable<Fingerprint>
    {
        internal int s0, s1;

        public Fingerprint()
        {
        }

        /// <summary>
        /// Constructor to create a fingerprint object from an array of integers.
        /// </summary>
        /// <param name="signature">The array of integers to create the fingerprint object from.</param>
        public Fingerprint(int[] signature)
        {
            s0 = signature[0];
            s1 = signature[1];
        }

        /// <summary>
        /// Constructor to create a fingerprint object from the individual signature ints.
        /// Avoids the allocation of a temporary array when cloning fingerprints.
        /// </summary>
        /// <param name="signature0">First signature value</param>
        /// <param name="signature1">Second signature value</param>
        public Fingerprint(int signature0, int signature1)
        {
            s0 = signature0;
            s1 = signature1;
        }

        internal int[] S 
        {
            get 
            {
                int[] ret = new int[2];
                ret[0] = s0;
                ret[1] = s1;
                return ret; 
            }
        }

        /// <summary>
        /// Checks for equality between two fingerprints
        /// </summary>
        /// <param name="obj">The other fingerprint to be compared.</param>
        /// <returns>Returns true if the fingerprints are equivalent.</returns>
        public override bool Equals(object obj)
        {
            Fingerprint s = obj as Fingerprint;
            bool ret = false;
            if(s != null)
            {
                ret =  s.s0 == s0 
                    && s.s1 == s1;
            }
            return ret;
        }

        public int CompareTo(Fingerprint other)
        {
            int cmp = s0.CompareTo(other.s0);
            if (cmp != 0)
                return cmp;

            return s1.CompareTo(other.s1);
        }

        /// <summary>
        /// XORs the integers representing the signature together and returns the result.
        /// </summary>
        /// <returns>The bitwise XOR of the integers in the signature.</returns>
        public override int GetHashCode()
        {
            return s0 ^ s1;
        }

        /// <summary>
        /// Function to compute the fingerprint of the concatenation of two
        /// buffers using only their fingerprints.
        /// </summary>
        /// <param name="fp">Fingerprint of the other buffer.</param>
        public void Concatenate(Fingerprint fp)
        {
            s0 ^= fp.s0;
            s1 ^= fp.s1;
        }

        /// <summary>
        /// Returns a clone of the fingerprint object.
        /// </summary>
        /// <returns>Returns the new clone.</returns>
        public object Clone()
        {
            return new Fingerprint(s0, s1);
        }

        /// <summary>
        /// Generates a human-readable version of the fingerprint.
        /// </summary>
        /// <returns>String containing the printable form of the fingerprint.</returns>
        public override string ToString()
        {
            return String.Format(CultureInfo.CurrentUICulture, "{0:x8}:{1:x8}", s0, s1);
        }
    }

    #region Stern & Dill probabilistic hash compression
    //Stern and Dill's probabilistic hash compression.	
    [Serializable]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class computeHASH
    {
        //static variables
        private static int gratelen = 1024000;
        private static int[] grate = new int[gratelen * 4];

        private static Random r = new Random(0);

        static computeHASH()
        {
            for (int i = 0; i < gratelen; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    grate[4 * i + j] = r.Next();
                }
            }
        }

        public static Fingerprint GetFingerprint(byte[] buffer, int len, int offset)
        {
            Fingerprint fp = new Fingerprint();
            if (((offset + len) * 8) * 4 > grate.Length)
            {
                string message = String.Format(CultureInfo.CurrentUICulture, "Grate length too short.  Recompile with length > {0}", (offset + len) * 8 * 4);
                Debug.Assert(false, message);
                throw new ArgumentException("buffer too large - recompile with a larger startHeapOffset");
            }
            byte x;
            int indexbase, idx;
            for (int i = 0; i < len; ++i)
            {
                indexbase = (i + offset) * 8;

                //  if the bit is on
                x = 1;
                for (int j = 0; j < 8; ++j)
                {
                    if ((buffer[i] & x) > 0)
                    {
                        idx = indexbase + j;
                        //    xor the line in the grate with my hash string
                        fp.s0 ^= grate[4 * idx + 0];
                        fp.s1 ^= grate[4 * idx + 1];
                        //  end if
                    }
                    //move the test bit over one
                    x <<= 1;
                    //end for each
                }
            }
            return fp;
        }
    

    }
    #endregion

    [Serializable]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class  TableEntryLIVEState
    {

        //instance members
        private TableEntryLIVEState next;
        public TableEntryLIVEState Next
        {
                get { return next; }
                set { next = value; }
        }

        [NonSerialized]
        private Fingerprint key;
        public Fingerprint Key
        {
            get { return key; }
        }
     
        private Object val;
        public Object Val
        {
            get { return val;}
            set { val = value;}
        }

        public TableEntryLIVEState(Fingerprint fp, Object value)
        {
            key = fp;
            val = value;
        }
        
        /// <summary>
        /// Computes an incremental fingerprint representation of a portion of the state vector.
        /// Will compute the fingerprint for byte i such that start \leq i \lt end.
        /// </summary>
        /// <param name="buffer">Array of bytes representing parts of the state vector.</param>
        /// <param name="len">The length of the buffer.</param>
        /// <param name="offset">The offset of the buffer in the global state vector.</param>
        
        public override bool Equals(object obj)
        {
            TableEntryLIVEState t = obj as TableEntryLIVEState;
            bool ret = false;
            if(t != null)
            {
                ret =  Key.Equals(t);
            }
            return ret;
        }


        public override int GetHashCode()
        {
            return Key.GetHashCode();
        }


        public Object Clone()
        {
            return new TableEntryLIVEState(this.Key, this.Val);
        }

    }


    [Serializable]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class DeadState
    {

        //instance members
        private DeadState next;
        public DeadState Next
        {
            get { return next; }
            set { next = value; }
        }

        [NonSerialized]
        private Fingerprint key;
        public Fingerprint Key
        {
            get { return key; }
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
        
        public bool MagicBit
        {
            get { return (exploreIfDepthLowerThan % 2 != 0); }
            set { exploreIfDepthLowerThan |= (value ? 0x1 : 0x0); }
        }


        public DeadState(Fingerprint fp,int eDV)
        {
            key = fp;
            ExploreIfDepthLowerThan = eDV;
            MagicBit = false;
        }

        /// <summary>
        /// Computes an incremental fingerprint representation of a portion of the state vector.
        /// Will compute the fingerprint for byte i such that start \leq i \lt end.
        /// </summary>
        /// <param name="buffer">Array of bytes representing parts of the state vector.</param>
        /// <param name="len">The length of the buffer.</param>
        /// <param name="offset">The offset of the buffer in the global state vector.</param>

        public override bool Equals(object obj)
        {
            DeadState t = obj as DeadState;
            bool ret = false;
            if (t != null)
            {
                ret = Key.Equals(t);
            }
            return ret;
        }


        public override int GetHashCode()
        {
            return Key.GetHashCode();
        }


        public Object Clone()
        {
            return new DeadState(this.Key,this.ExploreIfDepthLowerThan);
        }

    }
   
}