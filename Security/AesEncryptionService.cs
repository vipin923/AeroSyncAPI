using System.Security.Cryptography;

namespace AeroSyncAPI.Security
{
    public class AesEncryptionService
    {
        private readonly byte[] _key;

        public AesEncryptionService(IConfiguration config)
        {
            // Pull the master key from appsettings.json
            string base64Key = config.GetValue<string>("VaultSecurity:AesMasterKey");
            _key = Convert.FromBase64String(base64Key);

            if (_key.Length != 32)
                throw new ArgumentException("AES-256 requires exactly a 32-byte key.");
        }

        public string EncryptName(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;

            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = _key;
                aesAlg.GenerateIV(); // Generate a random 16-byte IV

                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    // 1. Write the IV to the beginning of the stream (we need it to decrypt later)
                    msEncrypt.Write(aesAlg.IV, 0, aesAlg.IV.Length);

                    // 2. Write the encrypted ciphertext
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                    {
                        swEncrypt.Write(plainText);
                    }

                    // 3. Return the combined IV + Ciphertext as a Base64 string for SQL
                    return Convert.ToBase64String(msEncrypt.ToArray());
                }
            }
        }
       

        public string DecryptName(string cipherTextBase64)
        {
            if (string.IsNullOrEmpty(cipherTextBase64)) return cipherTextBase64;

            byte[] fullCipher = Convert.FromBase64String(cipherTextBase64);

            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = _key;

                // 1. Extract the 16-byte IV from the front of the array
                byte[] iv = new byte[16];
                Array.Copy(fullCipher, 0, iv, 0, iv.Length);
                aesAlg.IV = iv;

                // 2. Extract the actual encrypted data
                int cipherTextLength = fullCipher.Length - 16;
                byte[] cipherText = new byte[cipherTextLength];
                Array.Copy(fullCipher, 16, cipherText, 0, cipherTextLength);

                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                // 3. Decrypt
                using (MemoryStream msDecrypt = new MemoryStream(cipherText))
                using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                {
                    return srDecrypt.ReadToEnd();
                }
            }
        }
    }
}

