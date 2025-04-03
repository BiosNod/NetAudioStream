using System;
using System.Security.Cryptography;
using System.Text;

namespace StreamingApplication
{
    public static class EncryptionHelper
    {
        private static readonly byte[] Key;
        private static readonly byte[] IV;

        static EncryptionHelper()
        {
            using var deriveBytes = new Rfc2898DeriveBytes(
                "MySuperSecretPassword",
                Encoding.UTF8.GetBytes("16ByteSalt123456"),
                100000,
                HashAlgorithmName.SHA512
            );

            Key = deriveBytes.GetBytes(32); // 256-bit
            IV = deriveBytes.GetBytes(16);  // 128-bit
        }

        public static byte[] Encrypt(byte[] data)
        {
            try
            {
                using var aes = Aes.Create();
                aes.Key = Key;
                aes.IV = IV;
                aes.Padding = PaddingMode.PKCS7;

                using var encryptor = aes.CreateEncryptor();
                return encryptor.TransformFinalBlock(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                Logger.Log($"Encryption failed: {ex.Message}", Logger.LogLevel.Error);
                throw;
            }
        }

        public static byte[] Decrypt(byte[] data)
        {
            try
            {
                using var aes = Aes.Create();
                aes.Key = Key;
                aes.IV = IV;
                aes.Padding = PaddingMode.PKCS7;

                using var decryptor = aes.CreateDecryptor();
                return decryptor.TransformFinalBlock(data, 0, data.Length);
            }
            catch (CryptographicException ex)
            {
                Logger.Log($"Decryption padding error: {ex.Message}", Logger.LogLevel.Error);
                Logger.Log($"Data length: {data.Length} bytes", Logger.LogLevel.Debug);
                throw;
            }
        }
    }
}