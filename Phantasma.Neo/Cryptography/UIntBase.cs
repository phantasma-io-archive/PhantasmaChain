using Phantasma.Neo.Utils;
using System;
using System.IO;
using System.Linq;

namespace Phantasma.Neo.Cryptography
{
    public abstract class UIntBase : IEquatable<UIntBase>
    {
        private byte[] data_bytes;

        public int Size => data_bytes.Length;

        protected UIntBase(int bytes, byte[] value)
        {
            if (value == null)
            {
                this.data_bytes = new byte[bytes];
                return;
            }
            if (value.Length != bytes)
                throw new ArgumentException();
            this.data_bytes = value;
        }

        public bool Equals(UIntBase other)
        {
            if (ReferenceEquals(other, null))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            if (data_bytes.Length != other.data_bytes.Length)
                return false;
            return data_bytes.SequenceEqual(other.data_bytes);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(obj, null))
                return false;
            if (!(obj is UIntBase))
                return false;
            return this.Equals((UIntBase)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                const int p = 16777619;
                int hash = (int)2166136261;

                for (int i = 0; i < data_bytes.Length; i++)
                    hash = (hash ^ data_bytes[i]) * p;

                hash += hash << 13;
                hash ^= hash >> 7;
                hash += hash << 3;
                hash ^= hash >> 17;
                hash += hash << 5;
                return hash;
            }
        }

        public byte[] ToArray()
        {
            return data_bytes;
        }

        /// <summary>
        /// 转为16进制字符串
        /// </summary>
        /// <returns>返回16进制字符串</returns>
        public override string ToString()
        {
            return "0x" + data_bytes.Reverse().ToHexString();
        }

        public static bool operator ==(UIntBase left, UIntBase right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
                return false;
            return left.Equals(right);
        }

        public static bool operator !=(UIntBase left, UIntBase right)
        {
            return !(left == right);
        }
    }
}
