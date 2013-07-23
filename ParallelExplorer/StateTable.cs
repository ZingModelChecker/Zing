using System;
using System.Collections;
using System.ComponentModel;
using System.IO;
using System.Collections.Generic;

namespace Microsoft.Zing
{
    /// <summary>
    /// Summary description for StateTable.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class DeadStateTable
    {
        public Int64 count;
        protected int tsize;
        //a table lookup to determine a prime number just less
        //than a power of two.  The prime number is determined by
        //computing pow(2,n)-primes[n] where the pow function computes the
        //nth power of two.  See http://www.utm.edu/research/primes/lists/ for
        //more information.
        protected static int[] primes = new int[33] { 0, 0, 1, 1, 3, 1, 1, 1, 5, 3, 3, 9, 3, 1, 3, 19, 15, 1, 5, 1, 3, 9, 3, 15, 3, 39, 5, 39, 57, 3, 35, 1, 5 };
        private bool server;
        public bool Server { get { return server; } set { server = value; } }

        protected DeadState[] FingerPrinttable;

        /// <summary>
        /// Creates a new StateTable object with a capacity that is slightly less than 2**capacity.
        /// </summary>
        /// <param name="capacity">Two raised to the capacity is the upper bound on the hash table size.</param>
        public DeadStateTable(int capacity)
        {
            count = 0;
            //make the table size prime
            tsize = ((1 << capacity) - primes[capacity]);
            FingerPrinttable = new DeadState[tsize];
        }

        /// <summary>
        /// Deletes all data in the StateTable and returns the table lines to the system for garbage collection.
        /// </summary>
        public virtual void Clear()
        {
            FingerPrinttable = new DeadState[tsize];
            /*for(int i = 0; i < FingerPrinttable.Length; i++)
            {
                FingerPrinttable[i] = null;
            }*/
        }

        /// <summary>
        /// Insert a Fingerprint into the StateTable.

        /// </summary>
        /// <param name="fingerprint">Fingerprint object to enter into the table.</param>
        /// <param name="value"> Value that is mapped to by the fingerprint fp</param>
        public virtual bool Insert(Fingerprint fingerprint, int eDV)
        {
            if (fingerprint != null)
            {
                lock (this)
                {
                    count++;
                    DeadState t = new DeadState(fingerprint, eDV);
                    int loc = fingerprint.GetHashCode() % tsize;
                    t.Next = FingerPrinttable[loc];
                    FingerPrinttable[loc] = t;
                }
            }
            return true;

        }

        public virtual bool Update(Fingerprint fp, int eDV, bool magicBit)
        {


            if (fp != null)
            {
                int loc = fp.GetHashCode() % tsize;
                for (DeadState x = FingerPrinttable[loc]; x != null; x = x.Next)
                {
                    if (x.Key.Equals(fp))
                    {
                        x.ExploreIfDepthLowerThan = eDV;
                        x.MagicBit = magicBit;
                        return true;
                    }
                }
            }
            return false;

        }

        /// <summary>
        /// Query the StateTable for the existance of Fingerprint s.
        /// </summary>
        /// <param name="fp">The Fingerprint to lookup in the StateTable.</param>
        /// <returns>True if present, false otherwise.</returns>
        public virtual bool Contains(Fingerprint fp)
        {
            bool ret = false;

            if (fp != null)
            {
                int loc = fp.GetHashCode() % tsize;
                for (DeadState x = FingerPrinttable[loc]; x != null; x = x.Next)
                {
                    if (x.Key.Equals(fp))
                    {
                        ret = true;
                        break;
                    }
                }
            }
            return ret;
        }
        public bool Remove(Fingerprint fp)
        {
            if (fp != null)
            {
                lock (this)
                {
                    count--;
                    int loc = fp.GetHashCode() % tsize;
                    if (FingerPrinttable[loc] != null)
                    {
                        if (FingerPrinttable[loc].Key.Equals(fp))
                        {
                            FingerPrinttable[loc] = FingerPrinttable[loc].Next;
                            return true;
                        }
                        else
                        {
                            DeadState x = FingerPrinttable[loc];
                            for (DeadState c = FingerPrinttable[loc].Next; c != null; c = c.Next, x = x.Next)
                            {
                                if (c.Key.Equals(fp))
                                {
                                    x.Next = c.Next;
                                    return true;

                                }
                            }
                        }
                    }
                }
            }
            return false;
        }

        public void Init()
        {

        }

        public DeadState LookupValue(Fingerprint fp)
        {
            DeadState ret = null;

            if (fp != null)
            {
                int loc = fp.GetHashCode() % tsize;
                for (DeadState x = FingerPrinttable[loc]; x != null; x = x.Next)
                {
                    if (x.Key.Equals(fp))
                    {

                        ret = x;
                        break;
                    }
                }
            }
            return ret;
        }
    }
}