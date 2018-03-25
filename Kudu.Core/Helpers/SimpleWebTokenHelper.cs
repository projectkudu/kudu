using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Kudu.Core.Helpers
{
    public static class SimpleWebTokenHelper
    {
        /// <summary>
        /// A SWT or a Simple Web Token is a token that's made of key=value pairs seperated
        /// by &. We only specify expiration in ticks from now (exp={ticks})
        /// The SWT is then returned as an encrypted string
        /// </summary>
        /// <param name="validUntil">Datetime for when the token should expire</param>
        /// <returns>a SWT signed by this app</returns>
        public static string CreateToken(DateTime validUntil) => Encrypt($"exp={validUntil.Ticks}");

        [SuppressMessage("Microsoft.Usage", "CA2202:Object 'cipherStream' and 'cryptoStream' can be disposed mo re than once",
            Justification = "MemoeryStream, CryptoStream, and BinaryWriter handle multiple disposal correctly. The alternative is pretty ugly code for clearing each variable, checking for null, and manual dispose.")]
        private static string Encrypt(string value)
        {
            using (var aes = new AesManaged { Key = GetWebSiteAuthEncryptionKey() })
            {
                // IV is always generated for the key every time
                aes.GenerateIV();
                var input = Encoding.UTF8.GetBytes(value);
                var iv = Convert.ToBase64String(aes.IV);

                using (var encrypter = aes.CreateEncryptor(aes.Key, aes.IV))
                using (var cipherStream = new MemoryStream())
                {
                    using (var cryptoStream = new CryptoStream(cipherStream, encrypter, CryptoStreamMode.Write))
                    using (var binaryWriter = new BinaryWriter(cryptoStream))
                    {
                        binaryWriter.Write(input);
                        cryptoStream.FlushFinalBlock();
                    }

                    // return {iv}.{swt}.{sha236(key)}
                    return string.Format("{0}.{1}.{2}", iv, Convert.ToBase64String(cipherStream.ToArray()), GetSHA256Base64String(aes.Key));
                }
            }
        }

        private static string GetSHA256Base64String(byte[] key)
        {
            using (var sha256 = new SHA256Managed())
            {
                return Convert.ToBase64String(sha256.ComputeHash(key));
            }
        }

        private static byte[] GetWebSiteAuthEncryptionKey()
        {
            var key = System.Environment.GetEnvironmentVariable(Constants.SiteAuthEncryptionKey);
            if (string.IsNullOrEmpty(key))
            {
                throw new InvalidOperationException($"No {Constants.SiteAuthEncryptionKey} defined in the environment");
            }

            // The key is in a hex string format
            var bytes = new List<byte>(key.Length / 2);
            for (var i = 0; i < key.Length; i += 2)
            {
                bytes.Add(Convert.ToByte(key.Substring(i, 2), 16));
            }

            return bytes.ToArray();
        }
    }
}
