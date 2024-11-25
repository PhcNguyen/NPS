﻿using System.Security.Cryptography;

namespace NServer.Infrastructure.Helper
{
    internal class GeneratorHelper
    {
        public static byte[] K128()
        {
            using var rng = RandomNumberGenerator.Create();
            byte[] key = new byte[128 / 8];
            rng.GetBytes(key);
            return key;
        }

        public static byte[] K192()
        {
            using var rng = RandomNumberGenerator.Create();
            byte[] key = new byte[192 / 8];
            rng.GetBytes(key);
            return key;
        }

        public static byte[] K256()
        {
            using var rng = RandomNumberGenerator.Create();
            byte[] key = new byte[256 / 8];
            rng.GetBytes(key);
            return key;
        }
    }
}