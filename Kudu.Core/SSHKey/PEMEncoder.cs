using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Kudu.Core.SSHKey
{
    /// <summary>
    /// PEM Encoding format for RSA private keys
    /// This is based on Abstract Syntax Notation One (ASN.1)
    /// </summary>
    internal static class PEMEncoding
    {
        private const string PEMHeader = "-----BEGIN RSA PRIVATE KEY-----";
        private const string PEMFooter = "-----END RSA PRIVATE KEY-----";
        private const int CharsPerLine = 64;
        private const byte IntegerTag = 0x02;
        private const byte SequenceTag = 0x10;
        private const byte ConstructedTag = 0x20;

        private static byte[] zeroAsn1 = new byte[] { 0x00 };

        /// <summary>
        /// Get PEM encoding string
        /// </summary>
        /// <param name="rsa">rsa private key</param>
        /// <returns>PEM encoding string</returns>
        public static string GetString(RSAParameters parameters)
        {
            Debug.Assert(parameters.D != null && parameters.D.Length > 0, "PEMEncoding only supports RSA private key!");

            List<byte[]> seq = new List<byte[]>(9);
            seq.Add(zeroAsn1); // version
            seq.Add(ToAsn1(parameters.Modulus));
            seq.Add(ToAsn1(parameters.Exponent));
            seq.Add(ToAsn1(parameters.D));
            seq.Add(ToAsn1(parameters.P));
            seq.Add(ToAsn1(parameters.Q));
            seq.Add(ToAsn1(parameters.DP));
            seq.Add(ToAsn1(parameters.DQ));
            seq.Add(ToAsn1(parameters.InverseQ));

            // Intermediate buffer could be avoided if we could calculate expected length
            MemoryStream temp = new MemoryStream();
            foreach (byte[] asn1 in seq)
            {
                temp.WriteAsn1(asn1);
            }

            temp.Flush();
            byte[] bytes = temp.ToArray();
            string encoding;
            using (var stream = new MemoryStream())
            {
                stream.WriteTag(SequenceTag | ConstructedTag);
                stream.WriteLength(bytes.Length);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush();

                encoding = Convert.ToBase64String(stream.ToArray());
            }
            StringBuilder strb = new StringBuilder();
            strb.AppendLine(PEMHeader);
            for (int i = 0; i < encoding.Length; i += CharsPerLine)
            {
                strb.AppendLine(encoding.Substring(i, Math.Min(CharsPerLine, encoding.Length - i)));
            }

            strb.AppendLine(PEMFooter);
            return strb.ToString();
        }

        private static void WriteAsn1(this MemoryStream mem, byte[] bytes)
        {
            mem.WriteTag(IntegerTag);
            mem.WriteLength(bytes.Length);
            mem.Write(bytes, 0, bytes.Length);
        }

        // Convert RSAParameters (big-endian) to ASN.1 
        private static byte[] ToAsn1(byte[] bytes)
        {
            // find the highest bit that is not zero
            byte highest = 0x00;
            for (int i = 0; i < bytes.Length; ++i)
            {
                if (bytes[i] != 0x00)
                {
                    highest = bytes[i];
                    break;
                }
            }

            // ensure unsigned per ASN.1 by adding zero if sign bit is set 
            if (0x80 == (highest & 0x80))
            {
                byte[] temp = new byte[bytes.Length + 1];
                Array.Copy(bytes, 0, temp, 1, bytes.Length);
                bytes = temp;
            }

            return bytes;
        }

        private static void WriteLength(this MemoryStream mem, int length)
        {
            if (length > 127)
            {
                int size = 1;
                uint val = (uint)length;

                while ((val >>= 8) != 0)
                {
                    size++;
                }

                mem.WriteByte((byte)(size | 0x80));

                for (int i = (size - 1) * 8; i >= 0; i -= 8)
                {
                    mem.WriteByte((byte)(length >> i));
                }
            }
            else
            {
                mem.WriteByte((byte)length);
            }
        }

        private static void WriteTag(this MemoryStream mem, int tag)
        {
            mem.WriteByte((byte)tag);
        }
    }
}
