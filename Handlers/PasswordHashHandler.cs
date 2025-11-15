using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using System.Security.Cryptography;
using System;

namespace MovieApi.Handlers
{
    public static class PasswordHashHandler
    {
        private static readonly int _iterationCount = 100000;
        private static readonly RandomNumberGenerator _randomNumberGenerator = RandomNumberGenerator.Create();

        // Hash Password
        public static string HashPassword(string password)
        {
            // 1. Generate a salt
            int saltSize = 128 / 8; // 16 bytes
            var salt = new byte[saltSize];
            _randomNumberGenerator.GetBytes(salt);

            // 2. Derive the subkey (hashed password)
            var subkey = KeyDerivation.Pbkdf2(
                password: password,
                salt: salt,
                prf: KeyDerivationPrf.HMACSHA512,
                iterationCount: _iterationCount,
                numBytesRequested: 256 / 8 // 32 bytes
            );

            // 3. Format: [version (1 byte)] [prf (4 bytes)] [iter count (4 bytes)] [salt length (4 bytes)] [salt] [subkey]
            var outputBytes = new byte[13 + salt.Length + subkey.Length];
            outputBytes[0] = 0x01; // Version marker
            WriteNetworkByteOrder(outputBytes, 1, (uint)KeyDerivationPrf.HMACSHA512);
            WriteNetworkByteOrder(outputBytes, 5, (uint)_iterationCount);
            WriteNetworkByteOrder(outputBytes, 9, (uint)saltSize);
            Buffer.BlockCopy(salt, 0, outputBytes, 13, salt.Length);
            Buffer.BlockCopy(subkey, 0, outputBytes, 13 + saltSize, subkey.Length);

            return Convert.ToBase64String(outputBytes);
        }

        // Verify Password
        public static bool VerifyPassword(string password, string hashedPassword)
        {
            try
            {
                var decodedHashedPassword = Convert.FromBase64String(hashedPassword);

                // Version marker
                if (decodedHashedPassword[0] != 0x01)
                    return false;

                var prf = (KeyDerivationPrf)ReadNetworkByteOrder(decodedHashedPassword, 1);
                var iterCount = (int)ReadNetworkByteOrder(decodedHashedPassword, 5);
                var saltLength = (int)ReadNetworkByteOrder(decodedHashedPassword, 9);

                if (saltLength < 128 / 8)
                    return false;

                var salt = new byte[saltLength];
                Buffer.BlockCopy(decodedHashedPassword, 13, salt, 0, salt.Length);

                var subkeyLength = decodedHashedPassword.Length - 13 - salt.Length;
                if (subkeyLength < 128 / 8)
                    return false;

                var expectedSubkey = new byte[subkeyLength];
                Buffer.BlockCopy(decodedHashedPassword, 13 + salt.Length, expectedSubkey, 0, expectedSubkey.Length);

                // Hash the incoming password with the same params
                var actualSubkey = KeyDerivation.Pbkdf2(
                    password: password,
                    salt: salt,
                    prf: prf,
                    iterationCount: iterCount,
                    numBytesRequested: subkeyLength
                );

                // Compare subkeys (constant time)
                return CryptographicOperations.FixedTimeEquals(actualSubkey, expectedSubkey);
            }
            catch
            {
                return false;
            }
        }

        // Utility Helpers
        private static void WriteNetworkByteOrder(byte[] buffer, int offset, uint value)
        {
            buffer[offset + 0] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)(value >> 0);
        }

        private static uint ReadNetworkByteOrder(byte[] buffer, int offset)
        {
            return ((uint)(buffer[offset + 0]) << 24)
                 | ((uint)(buffer[offset + 1]) << 16)
                 | ((uint)(buffer[offset + 2]) << 8)
                 | ((uint)(buffer[offset + 3]));
        }
    }
}
