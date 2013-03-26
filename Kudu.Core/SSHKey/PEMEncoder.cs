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


        public static RSAParameters ExtractPublicKey(string privateKey)
        {
            string pem;
            if (!TryParsePEM(privateKey, out pem))
            {
                throw new FormatException("Unsupported private key format.");
            }

            byte[] encoding = Convert.FromBase64String(pem);

            using (var mem = new MemoryStream(encoding))
            {

                int tag = mem.ReadTag();
                if (tag != (SequenceTag | ConstructedTag))
                {
                    throw new ArgumentException("Unexpected tag in PEM!");
                }

                int length = mem.ReadLength(encoding.Length - (int)mem.Position);
                if (length != (encoding.Length - (int)mem.Position))
                {
                    throw new ArgumentException("Unexpected PEM length!");
                }

                byte[] version = mem.ReadAsn1(encoding.Length - (int)mem.Position);
                if (version.Length != zeroAsn1.Length || version[0] != zeroAsn1[0])
                {
                    throw new ArgumentException("Unsupported PEM version 0!");
                }

                return new RSAParameters
                {
                    Modulus = FromAsn1(mem.ReadAsn1(encoding.Length - (int)mem.Position)),
                    Exponent = FromAsn1(mem.ReadAsn1(encoding.Length - (int)mem.Position))
                };
            }
        }

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

        // Convert ASN.1 to RSAParameters (big-endian) 
        private static byte[] FromAsn1(byte[] bytes)
        {
            Debug.Assert(bytes != null && bytes.Length > 0);

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

            // pretty much reverse process of ToAsn1
            if (0x80 == (highest & 0x80))
            {
                byte[] temp = new byte[bytes.Length - 1];
                Array.Copy(bytes, 1, temp, 0, temp.Length);
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

        private static int ReadTag(this MemoryStream mem)
        {
            return mem.ReadByte();
        }

        private static bool TryParsePEM(string text, out string pem)
        {
            pem = null;
            using (var reader = new StringReader(text.Trim()))
            {
                string line = reader.ReadLine();
                if (line == null || !line.Trim().Equals(PEMHeader, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                var builder = new StringBuilder();
                // Read header
                while ((line = reader.ReadLine()) != null && !String.IsNullOrEmpty(line = line.Trim()) && !line.Equals(PEMFooter, StringComparison.OrdinalIgnoreCase))
                {
                    string[] header = line.Split(new string[] { ": " }, StringSplitOptions.None);
                    if (header.Length == 1)
                    {
                        builder.Append(header[0].Trim());
                        break;
                    }
                    else if (header.Length == 2)
                    {
                        if (header[0].Trim() == "Proc-Type" && header[1].Trim() == "4,ENCRYPTED")
                        {
                            throw new FormatException("Passphrase is not supported!");
                        }
                    }
                    else
                    {
                        throw new FormatException("Invalid PEM format!");
                    }
                }

                while ((line = reader.ReadLine()) != null && !string.IsNullOrEmpty(line = line.Trim()) && !line.Equals(PEMFooter, StringComparison.OrdinalIgnoreCase))
                {
                    builder.Append(line);
                }

                if (line == null || !line.Equals(PEMFooter, StringComparison.OrdinalIgnoreCase))
                {
                    throw new FormatException("Missing PEM footer!");
                }

                pem = builder.ToString();
                return pem.Length > 0;
            }
        }

        private static byte[] ReadAsn1(this MemoryStream mem, int limit)
        {
            int tag = mem.ReadTag();
            if (tag != IntegerTag)
            {
                throw new ArgumentException("Invalid Integer tag!");
            }

            int length = mem.ReadLength(limit);
            byte[] bytes = new byte[length];
            mem.Read(bytes, 0, bytes.Length);
            return bytes;
        }

        private static int ReadLength(this MemoryStream mem, int limit)
        {
            int length = mem.ReadByte();
            if (length < 0)
            {
                throw new ArgumentException("EOF found when length expected");
            }

            if (length == 0x80)
            {
                throw new ArgumentException("Indefinite-length encoding");
            }

            if (length > 127)
            {
                int size = length & 0x7f;

                // Note: The invalid long form "0xff" (see X.690 8.1.3.5c) will be caught here
                if (size > 4)
                {
                    throw new ArgumentException("DER length more than 4 bytes: " + size);
                }

                length = 0;
                for (int i = 0; i < size; i++)
                {
                    int next = mem.ReadByte();

                    if (next < 0)
                    {
                        throw new ArgumentException("EOF found reading length");
                    }

                    length = (length << 8) + next;
                }

                if (length < 0)
                {
                    throw new ArgumentException("Corrupted stream - negative length found");
                }

                if (length >= limit)   // after all we must have read at least 1 byte
                {
                    throw new ArgumentException("Corrupted stream - out of bounds length found");
                }
            }

            return length;
        }
    }
}
