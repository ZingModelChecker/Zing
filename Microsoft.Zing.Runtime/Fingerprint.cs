/***********************************************************************************
 * Fingerprint.cs -- Implementation of Stern & Dill's probabilistic hash compaction *
 **********************************************************************************/

using System;
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
            if (s != null)
            {
                ret = s.s0 == s0
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
    public class computeHASH
    {
        //static variables
        private static int gratelen = 8192000;

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

    #endregion Stern & Dill probabilistic hash compression
}