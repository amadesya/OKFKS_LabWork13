using System.Security.Cryptography;

namespace FileHashTransfer.Common
{
    public class HashCalculator
    {
        public static (string hash, byte[] salt) ComputeHashWithSalt(byte[] data)
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                byte[] salt = new byte[32];
                rng.GetBytes(salt);

                using (var sha256 = SHA256.Create())
                {
                    byte[] combined = new byte[data.Length + salt.Length];
                    Buffer.BlockCopy(data, 0, combined, 0, data.Length);
                    Buffer.BlockCopy(salt, 0, combined, data.Length, salt.Length);

                    byte[] hashBytes = sha256.ComputeHash(combined);
                    string hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

                    return (hash, salt);
                }
            }
        }

        public static bool VerifyHashWithSalt(byte[] data, byte[] salt, string expectedHash)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] combined = new byte[data.Length + salt.Length];
                Buffer.BlockCopy(data, 0, combined, 0, data.Length);
                Buffer.BlockCopy(salt, 0, combined, data.Length, salt.Length);

                byte[] hashBytes = sha256.ComputeHash(combined);
                string actualHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

                return actualHash == expectedHash;
            }
        }

        public static string ComputeHash(byte[] data)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(data);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }
        }
    }
}