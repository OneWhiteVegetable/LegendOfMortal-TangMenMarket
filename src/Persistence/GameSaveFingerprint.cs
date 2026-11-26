using System;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using Mortal.Core;

namespace XingShangMod.Persistence
{
    internal static class GameSaveFingerprint
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public static string Compute(GameSave gameSave)
        {
            if (gameSave == null)
                throw new ArgumentNullException("gameSave");

            byte[] bytes;
            using (var stream = new MemoryStream())
            {
                new BinaryFormatter().Serialize(stream, gameSave);
                bytes = stream.ToArray();
            }
            return ComputeHash(bytes);
        }

        public static string ComputeText(string value)
        {
            return ComputeHash(StrictUtf8.GetBytes(value ?? string.Empty));
        }

        private static string ComputeHash(byte[] bytes)
        {
            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] hash = algorithm.ComputeHash(bytes);
                var result = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    result.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                return result.ToString();
            }
        }
    }
}
