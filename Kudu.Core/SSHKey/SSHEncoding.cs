using System;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Kudu.Core.SSHKey
{
    internal static class SSHEncoding
    {
        private const string SSHPrefix = "ssh-rsa";
        private static readonly byte[] sshHeader = { 0x00, 0x00, 0x00, 0x07, 0x73, 0x73, 0x68, 0x2D, 0x72, 0x73, 0x61 };

        /// <summary>
        /// Get a SSH key in PEM encoded format
        /// </summary>
        /// <param name="rsa">rsa public key</param>
        /// <returns>SSH encoding string</returns>
        public static string GetString(RSAParameters param)
        {
            int length = sshHeader.Length + 4 + param.Exponent.Length + 4 + param.Modulus.Length;
            if ((param.Exponent[0] & 0x80) != 0)
            {
                length++;
            }

            if ((param.Modulus[0] & 0x80) != 0)
            {
                length++;
            }

            int offset = 0;
            byte[] bytes = new byte[length];
            Array.Copy(sshHeader, 0, bytes, 0, sshHeader.Length);
            offset += sshHeader.Length;
            offset += SshEncodeBuffer(param.Exponent, bytes, offset);
            offset += SshEncodeBuffer(param.Modulus, bytes, offset);

            Debug.Assert(offset == bytes.Length, "length mush match");
            return string.Format("{0} {1}", SSHPrefix, Convert.ToBase64String(bytes, 0, offset));
        }

        private static int SshEncodeBuffer(byte[] input, byte[] encoded, int offset)
        {
            int adjustedLen = input.Length;
            int index = 4;
            if ((input[0] & 0x80) != 0x00)
            {
                adjustedLen++;
                encoded[offset + 4] = 0;
                index = 5;
            }

            encoded[offset] = (byte)(adjustedLen >> 24);
            encoded[offset + 1] = (byte)(adjustedLen >> 16);
            encoded[offset + 2] = (byte)(adjustedLen >> 8);
            encoded[offset + 3] = (byte)adjustedLen;

            Array.Copy(input, 0, encoded, offset + index, input.Length);
            return index + input.Length;
        }
    }
}
