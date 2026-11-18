using System;

namespace XingShangMod.Progression
{
    public static class RunRandom
    {
        public static int NextIndex(int runSeed, int count, params string[] keyParts)
        {
            if (count <= 0)
                throw new ArgumentOutOfRangeException("count");
            return (int)(StableHash(runSeed, keyParts) % (uint)count);
        }

        public static int NextInt(
            int runSeed,
            int minInclusive,
            int maxExclusive,
            params string[] keyParts)
        {
            if (maxExclusive <= minInclusive)
                throw new ArgumentOutOfRangeException("maxExclusive");

            long range = (long)maxExclusive - minInclusive;
            long value = minInclusive + StableHash(runSeed, keyParts) % range;
            return (int)value;
        }

        public static uint StableHash(int runSeed, params string[] keyParts)
        {
            unchecked
            {
                uint hash = 2166136261u;
                HashUInt(ref hash, (uint)runSeed);
                if (keyParts == null)
                {
                    HashUInt(ref hash, uint.MaxValue);
                }
                else
                {
                    HashUInt(ref hash, (uint)keyParts.Length);
                    for (int i = 0; i < keyParts.Length; i++)
                    {
                        string part = keyParts[i];
                        if (part == null)
                        {
                            HashUInt(ref hash, uint.MaxValue);
                            continue;
                        }

                        HashUInt(ref hash, (uint)part.Length);
                        for (int j = 0; j < part.Length; j++)
                        {
                            char value = part[j];
                            HashByte(ref hash, (byte)value);
                            HashByte(ref hash, (byte)(value >> 8));
                        }
                    }
                }

                hash ^= hash >> 16;
                hash *= 0x7feb352du;
                hash ^= hash >> 15;
                hash *= 0x846ca68bu;
                hash ^= hash >> 16;
                return hash;
            }
        }

        private static void HashUInt(ref uint hash, uint value)
        {
            HashByte(ref hash, (byte)value);
            HashByte(ref hash, (byte)(value >> 8));
            HashByte(ref hash, (byte)(value >> 16));
            HashByte(ref hash, (byte)(value >> 24));
        }

        private static void HashByte(ref uint hash, byte value)
        {
            unchecked
            {
                hash ^= value;
                hash *= 16777619u;
            }
        }
    }
}
