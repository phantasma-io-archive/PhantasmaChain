using System;
using System.Globalization;
using System.Linq;
using Phantasma.Utils;

namespace Phantasma.Mathematics
{
    public class UInt256 : IComparable<UInt256>, IEquatable<UInt256>
    {
        public const int bytes = 32;

        private byte[] data_bytes;

        public int Size => data_bytes.Length;

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(obj, null))
                return false;
            if (!(obj is UInt256))
                return false;
            return this.Equals((UInt256)obj);
        }

        public override int GetHashCode()
        {
            return (int)data_bytes.ToUInt32(0);
        }

        public byte[] ToArray()
        {
            return data_bytes;
        }

        public override string ToString()
        {
            return "0x" + Base16.Encode(data_bytes.Reverse().ToArray());
        }

        public static readonly UInt256 Zero = new UInt256();

        public UInt256() : this(null)
        {
        }

        public UInt256(byte[] value)
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

        public int CompareTo(UInt256 other)
        {
            byte[] x = ToArray();
            byte[] y = other.ToArray();
            for (int i = x.Length - 1; i >= 0; i--)
            {
                if (x[i] > y[i])
                    return 1;
                if (x[i] < y[i])
                    return -1;
            }
            return 0;
        }

        bool IEquatable<UInt256>.Equals(UInt256 other)
        {
            return Equals(other);
        }

        public static UInt256 Parse(string s)
        {
            if (s == null)
                throw new ArgumentNullException();
            if (s.StartsWith("0x"))
                s = s.Substring(2);
            if (s.Length != 64)
                throw new FormatException();
            return new UInt256(s.Decode().Reverse().ToArray());
        }

        public static bool TryParse(string s, out UInt256 result)
        {
            if (s == null)
            {
                result = null;
                return false;
            }
            if (s.StartsWith("0x"))
                s = s.Substring(2);
            if (s.Length != 64)
            {
                result = null;
                return false;
            }
            byte[] data = new byte[32];
            for (int i = 0; i < 32; i++)
                if (!byte.TryParse(s.Substring(i * 2, 2), NumberStyles.AllowHexSpecifier, null, out data[i]))
                {
                    result = null;
                    return false;
                }
            result = new UInt256(data.Reverse().ToArray());
            return true;
        }

        public static bool operator ==(UInt256 left, UInt256 right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
                return false;
            return left.Equals(right);
        }

        public static bool operator !=(UInt256 left, UInt256 right)
        {
            return !(left == right);
        }


        public static bool operator >(UInt256 left, UInt256 right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(UInt256 left, UInt256 right)
        {
            return left.CompareTo(right) >= 0;
        }

        public static bool operator <(UInt256 left, UInt256 right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(UInt256 left, UInt256 right)
        {
            return left.CompareTo(right) <= 0;
        }
    }
}
