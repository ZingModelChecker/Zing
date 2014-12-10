﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;
using System.Diagnostics.Contracts;

namespace Microsoft.Zing
{
    [Serializable]
    public class StateData
    {
        

        /// <summary>
        /// Magic bit used during NDFS liveness algorithm to distinguish between red and green states
        /// </summary>
        private bool magicBit = false;
        public bool MagicBit
        {
            get { return magicBit; }
            set { magicBit = value; }
        }

        public StateData(bool magic)
        {
            MagicBit = magic;
        }

        public StateData()
        {

        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;
            else
            {
                if (obj is StateData)
                {
                    return magicBit = (obj as StateData).magicBit;
                }
                else
                    return false;
            }
        }
        public bool Equals(StateData st)
        {
            return st.magicBit == magicBit;
        }
    }

    /// <summary>
    /// The global state used during state full search to store all the visited states.
    /// </summary>
    public class ZingerStateTable
    {
        /// <summary>
        /// State table map from fingerprint to the state-data.
        /// </summary>
        ConcurrentDictionary<Fingerprint, StateData> hashTable = new ConcurrentDictionary<Fingerprint, StateData>();

        public void AddOrUpdate(Fingerprint fp, StateData sd)
        {
            if (hashTable.ContainsKey(fp))
            {
                StateData oldValue;
                hashTable.TryGetValue(fp, out oldValue);
                hashTable.TryUpdate(fp, sd, oldValue);
            }
            else
            {
                if(hashTable.TryAdd(fp, sd))
                {
                    ZingerStats.IncrementStatesCount();
                }
            }
        }

        public bool Contains(Fingerprint fp)
        {
            return hashTable.ContainsKey(fp);
        }

        public StateData GetStateData(Fingerprint fp)
        {
            StateData statedata;
            var check = hashTable.TryGetValue(fp, out statedata);
            Contract.Assert(check);
            return statedata;
        }
    }
}