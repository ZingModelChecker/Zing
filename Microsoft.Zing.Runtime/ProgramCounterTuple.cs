using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.Zing
{
    [CLSCompliant(false)]
    public struct ProgramCounter
    {
        private static Hashtable methodNameTable = new Hashtable();
        private static Hashtable methodBlockTable = new Hashtable();
        private static Hashtable methodNumberTable = new Hashtable();
        private static ushort methodCounter = 0;

        private ushort methodNumber;
        [CLSCompliant(false)]
        public ushort MethodNumber { get { return methodNumber; } }

        private ushort nextBlock;
        public ushort NextBlock { get { return nextBlock; } }

        public override bool Equals(object obj)
        {
            ProgramCounter pc = (ProgramCounter)obj;
            return (methodNumber == pc.methodNumber && nextBlock == pc.nextBlock);
        }

        public static bool operator ==(ProgramCounter pc1, ProgramCounter pc2)
        {
            return pc1.Equals(pc2);
        }

        public static bool operator !=(ProgramCounter pc1, ProgramCounter pc2)
        {
            return !pc1.Equals(pc2);
        }

        public ProgramCounter(ZingMethod method)
        {
            string name = method.MethodName;
            object o = methodNameTable[name];
            Hashtable blockTable = null;
            if (o == null)
            {
                methodNameTable[name] = ++methodCounter;
                blockTable = new Hashtable();
                methodBlockTable[name] = blockTable;
                methodNumberTable[methodCounter] = name;
                methodNumber = methodCounter;
            }
            else
            {
                blockTable = (Hashtable)methodBlockTable[name];
                methodNumber = (ushort)o;
            }

            nextBlock = method.NextBlock;
            blockTable[nextBlock] = method.ProgramCounter;
        }

        public override int GetHashCode()
        {
            uint i1 = (uint)methodNumber;
            i1 = i1 << 16;
            uint i2 = (uint)nextBlock;
            return (int)(i1 | i2);
        }

        public bool Equals(ProgramCounter pc)
        {
            return (methodNumber == pc.methodNumber && nextBlock == pc.nextBlock);
        }

        public int CompareTo(ProgramCounter pc)
        {
            int result;
            result = methodNumber.CompareTo(pc.methodNumber);
            if (result != 0)
                return result;
            else
                return nextBlock.CompareTo(pc.nextBlock);
        }

        public override string ToString()
        {
            if (methodNumber == 0)
                return "END";
            else
            {
                string methodName = (string)methodNumberTable[methodNumber];
                Hashtable blockTable = (Hashtable)methodBlockTable[methodName];
                string nextBlockName = (string)blockTable[nextBlock];
                return methodName + ":" + nextBlockName;
            }
        }

        public static string GetMethodName(ushort number)
        {
            if (number == 0)
                return null;
            else
                return (string)methodNumberTable[number];
        }

        public static string GetNextBlockName(string methodName, ushort blockNumber)
        {
            Hashtable blockTable = (Hashtable)methodBlockTable[methodName];
            return (string)blockTable[blockNumber];
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly")]
    [CLSCompliant(false)]
    public class ProgramCounterTuple : IComparable
    {
        private ProgramCounter[] pcs;

        public ProgramCounter[] ProgramCounters()
        {
            return pcs;
        }

        public ProgramCounterTuple(ProgramCounter[] counters)
        {
            pcs = counters;
        }

        public override int GetHashCode()
        {
            int acc = 0;
            for (int i = 0; i < pcs.Length; i++)
                acc ^= pcs[i].GetHashCode();
            return acc;
        }

        public int CompareTo(object obj)
        {
            ProgramCounter[] pcs1 = ((ProgramCounterTuple)obj).pcs;
            int result;

            result = pcs.Length.CompareTo(pcs1.Length);
            if (result != 0)
                return result;
            for (int i = 0; i < pcs.Length; i++)
            {
                result = pcs[i].CompareTo(pcs1[i]);
                if (result != 0)
                    return result;
            }
            return 0;
        }

        public override bool Equals(object obj)
        {
            ProgramCounter[] pcs1 = ((ProgramCounterTuple)obj).pcs;
            if (pcs.Length != pcs1.Length)
                return false;
            for (int i = 0; i < pcs.Length; i++)
                if (!Equals(pcs[i], pcs1[i])) return false;
            return true;
        }

        public static bool operator !=(ProgramCounterTuple obj1, object obj2)
        {
            return !obj1.Equals(obj2);
        }

        public static bool operator ==(ProgramCounterTuple obj1, object obj2)
        {
            return obj1.Equals(obj2);
        }

        public static int operator <(ProgramCounterTuple obj1, object obj2)
        {
            return obj1.CompareTo(obj2);
        }

        public static int operator >(ProgramCounterTuple obj1, object obj2)
        {
            return -1 * obj1.CompareTo(obj2);
        }

        public ProgramCounter this[int processNumber]
        {
            get
            {
                return this.pcs[processNumber];
            }
        }

        public int Length
        {
            get
            {
                return pcs.Length;
            }
        }
    }
}
