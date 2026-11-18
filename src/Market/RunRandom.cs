using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace XingShangMod.Market
{
    public sealed class RunRandom
    {
        private readonly int _runSeed;

        public RunRandom(int runSeed)
        {
            _runSeed = runSeed;
        }

        public ulong NextUInt64(string purpose, string stableKey)
        {
            if (purpose == null)
                throw new ArgumentNullException("purpose");
            if (stableKey == null)
                throw new ArgumentNullException("stableKey");

            byte[] purposeBytes = Encoding.UTF8.GetBytes(purpose);
            byte[] keyBytes = Encoding.UTF8.GetBytes(stableKey);
            byte[] input = new byte[12 + purposeBytes.Length + keyBytes.Length];
            input[0] = (byte)_runSeed;
            input[1] = (byte)(_runSeed >> 8);
            input[2] = (byte)(_runSeed >> 16);
            input[3] = (byte)(_runSeed >> 24);
            WriteLength(input, 4, purposeBytes.Length);
            Buffer.BlockCopy(purposeBytes, 0, input, 8, purposeBytes.Length);
            int keyLengthOffset = 8 + purposeBytes.Length;
            WriteLength(input, keyLengthOffset, keyBytes.Length);
            Buffer.BlockCopy(keyBytes, 0, input, keyLengthOffset + 4, keyBytes.Length);

            byte[] hash;
            using (SHA256 sha = SHA256.Create())
                hash = sha.ComputeHash(input);

            ulong value = 0;
            for (int i = 0; i < 8; i++)
                value |= (ulong)hash[i] << (8 * i);
            return value;
        }

        public int NextIndex(string purpose, string stableKey, int count)
        {
            if (count <= 0)
                throw new ArgumentOutOfRangeException("count");
            return (int)(NextUInt64(purpose, stableKey) % (ulong)count);
        }

        public T Choose<T>(string purpose, string stableKey, IReadOnlyList<T> values)
        {
            if (values == null)
                throw new ArgumentNullException("values");
            return values[NextIndex(purpose, stableKey, values.Count)];
        }

        private static void WriteLength(byte[] destination, int offset, int value)
        {
            destination[offset] = (byte)value;
            destination[offset + 1] = (byte)(value >> 8);
            destination[offset + 2] = (byte)(value >> 16);
            destination[offset + 3] = (byte)(value >> 24);
        }
    }
}
