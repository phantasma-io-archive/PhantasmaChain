using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Phantasma.Mathematics;
using Phantasma.Utils;

namespace Phantasma.Cryptography
{
    public class Hash : IComparable<Hash>, IEquatable<Hash>
    {
        public const int SIZE = 32;

        private byte[] _data;

        public int Size => _data.Length;

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(obj, null))
                return false;
            if (!(obj is Hash))
                return false;
            return this.Equals((Hash)obj);
        }

        public override int GetHashCode()
        {
            return (int)_data.ToUInt32(0);
        }

        public byte[] ToByteArray()
        {
            return _data;
        }

        public override string ToString()
        {
            return "0x" + Base16.Encode(_data.Reverse().ToArray());
        }

        public static readonly Hash Zero = new Hash();

        public Hash()
        {
            this._data = new byte[SIZE];
        }

        public Hash(byte[] value)
        {
            Throw.If(value == null, "value cannot be null");
            Throw.If(value.Length != SIZE, $"value must have length {SIZE}");

            this._data = value;
        }

        public int CompareTo(Hash other)
        {
            byte[] x = ToByteArray();
            byte[] y = other.ToByteArray();
            for (int i = x.Length - 1; i >= 0; i--)
            {
                if (x[i] > y[i])
                    return 1;
                if (x[i] < y[i])
                    return -1;
            }
            return 0;
        }

        bool IEquatable<Hash>.Equals(Hash other)
        {
            return Equals(other);
        }

        public static Hash Parse(string s)
        {
            Throw.If(string.IsNullOrEmpty(s), "string cannot be empty");

            if (s.StartsWith("0x"))
            {
                return Parse(s.Substring(2));
            }

            Throw.If(s.Length != SIZE, $"length of string must be {SIZE}");

            return new Hash(s.Decode().Reverse().ToArray());
        }

        public static bool TryParse(string s, out Hash result)
        {
            if (string.IsNullOrEmpty(s))
            {
                result = null;
                return false;
            }

            if (s.StartsWith("0x"))
            {
                return TryParse(s.Substring(2), out result);
            }
            if (s.Length != 64)
            {
                result = null;
                return false;
            }

            try
            {
                byte[] data = Base16.Decode(s);

                result = new Hash(data.Reverse().ToArray());
                return true;
            }
            catch
            {
                result = null;
                return false;
            }
        }

        public static bool operator ==(Hash left, Hash right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
                return false;
            return left.Equals(right);
        }

        public static bool operator !=(Hash left, Hash right)
        {
            return !(left == right);
        }


        public static bool operator >(Hash left, Hash right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(Hash left, Hash right)
        {
            return left.CompareTo(right) >= 0;
        }

        public static bool operator <(Hash left, Hash right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(Hash left, Hash right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static implicit operator Hash(BigInteger val)
        {
            return new Hash(val.ToByteArray());
        }

        public static implicit operator BigInteger(Hash val)
        {
            return new BigInteger(val.ToByteArray());
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(_data);
        }

        public static Hash Unserialize(BinaryReader reader)
        {
            var data = reader.ReadBytes(SIZE);
            return new Hash(data);
        }
    }
}
