using Phantasma.Cryptography;
using Phantasma.Mathematics;
using Phantasma.VM.Types;
using System;
using System.IO;
using System.Text;

namespace Phantasma.Utils
{
    public static class IOUtils
    {
        public static void WriteVarInt(this BinaryWriter writer, long value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException();
            if (value < 0xFD)
            {
                writer.Write((byte)value);
            }
            else if (value <= 0xFFFF)
            {
                writer.Write((byte)0xFD);
                writer.Write((ushort)value);
            }
            else if (value <= 0xFFFFFFFF)
            {
                writer.Write((byte)0xFE);
                writer.Write((uint)value);
            }
            else
            {
                writer.Write((byte)0xFF);
                writer.Write(value);
            }
        }

        public static void WriteAddress(this BinaryWriter writer, Address address)
        {
            writer.Write(address.PublicKey);
        }

        public static void WriteHash(this BinaryWriter writer, Hash hash)
        {
            if (hash == null)
            {
                hash = Hash.Null;
            }
            var bytes = hash.ToByteArray();
            writer.Write(bytes);
        }

        public static void WriteBigInteger(this BinaryWriter writer, BigInteger n)
        {
            var bytes = n.ToByteArray();
            writer.Write((byte)bytes.Length);
            writer.Write(bytes);
        }

        public static void WriteByteArray(this BinaryWriter writer, byte[] bytes)
        {
            if (bytes == null)
            {
                writer.Write((byte)0);
                return;
            }
            writer.Write((byte)bytes.Length);
            writer.Write(bytes);
        }

        public static void WriteShortString(this BinaryWriter writer, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                writer.Write((byte)0);
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(text);
            writer.Write((byte)bytes.Length);
            writer.Write(bytes);
        }

        public static ulong ReadVarInt(this BinaryReader reader, ulong max = ulong.MaxValue)
        {
            byte fb = reader.ReadByte();
            ulong value;
            if (fb == 0xFD)
                value = reader.ReadUInt16();
            else if (fb == 0xFE)
                value = reader.ReadUInt32();
            else if (fb == 0xFF)
                value = reader.ReadUInt64();
            else
                value = fb;
            if (value > max) throw new FormatException();
            return value;
        }

        public static Address ReadAddress(this BinaryReader reader)
        {
            var bytes = reader.ReadBytes(Address.PublicKeyLength);
            return new Address(bytes);
        }

        public static Hash ReadHash(this BinaryReader reader)
        {
            var data = reader.ReadBytes(Hash.SIZE);
            var result = new Hash(data);
            return result == Hash.Null ? null : result;
        }

        public static BigInteger ReadBigInteger(this BinaryReader reader)
        {
            var length = reader.ReadByte();
            var bytes = reader.ReadBytes(length);
            return new BigInteger(bytes);
        }

        public static byte[] ReadByteArray(this BinaryReader reader)
        {
            var length = reader.ReadByte();
            if (length == 0)
                return null;
            var bytes = reader.ReadBytes(length);
            return bytes;
        }

        public static string ReadShortString(this BinaryReader reader)
        {
            var length = reader.ReadByte();
            if (length == 0)
                return null;
            var bytes = reader.ReadBytes(length);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
