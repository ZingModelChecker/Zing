using System;

namespace Microsoft.Zing
{
    public static class ZingRNG
    {
        private const int NN = 312;
        private const int MM = 156;
        private const UInt64 UM =  0xFFFFFFFF80000000UL;
        private const UInt64 LM = 0x7FFFFFFFUL;
        private const UInt64 MATRIX_A = (UInt64)0xB5026F5AA96619E9UL;

        private static readonly UInt64[] magic;
        private static UInt64[] mt;
        private static int mti;

        static ZingRNG()
        {
            magic = new UInt64[2];
            magic[0] = 0;
            magic[1] = MATRIX_A;
            mt = new UInt64[NN];
            mt[0] = (UInt64)System.DateTime.Now.Ticks;
            for (int i = 1; i < mt.Length; i++)
            {
                mt[i] = (6364136223846793005UL * (mt[i - 1] ^ (mt[i - 1] >> 62)) + (UInt64)i);
            }
            mti = mt.Length;
        }

        public static UInt64 GetRandom()
        {
            UInt64 x;

            lock (typeof(ZingRNG))
            {
                if (mti >= NN)
                {
                    int i;

                    for (i = 0; i < NN - MM; i++)
                    {
                        x = (mt[i] & UM) | (mt[i + 1] & LM);
                        mt[i] = mt[i + MM] ^ (x >> 1) ^ magic[(int)(x & 1UL)];
                    }
                    for (; i < NN - 1; i++)
                    {
                        x = (mt[i] & UM) | (mt[i + 1] & LM);
                        mt[i] = mt[i + (MM - NN)] ^ (x >> 1) ^ magic[(int)(x & 1UL)];
                    }
                    x = (mt[NN - 1] & UM) | (mt[0] & LM);
                    mt[NN - 1] = mt[MM - 1] ^ (x >> 1) ^ magic[(int)(x & 1UL)];
                    mti = 0;
                }

                x = mt[mti++];
            }

            x ^= (x >> 29) & 0x5555555555555555UL;
            x ^= (x << 17) & 0x71D67FFFEDA60000UL;
            x ^= (x << 37) & 0xFFF7EEE000000000UL;
            x ^= (x >> 43);

            return x;
        }

        public static double GetUniformRV()
        {
            return ((double)GetRandom() / (double)UInt64.MaxValue);
        }
    }
}
