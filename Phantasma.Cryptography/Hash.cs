using System;
using System.IO;
using System.Numerics;
using System.Linq;
using Phantasma.Numerics;
using Phantasma.Core;
using Phantasma.Core.Utils;
using Phantasma.Storage;
using Phantasma.Storage.Utils;
using System.Text;

namespace Phantasma.Cryptography
{
    public struct Hash : ISerializable, IComparable<Hash>, IEquatable<Hash>
    {
        public const int Length = 32;

        public static readonly Hash Null = Hash.FromBytes(new byte[Length]);

        private byte[] _data;
    
        public int Size => _data.Length;

        public bool IsNull
        {
            get
            {
                if (_data == null)
                {
                    return true;
                }

                for (int i=0; i<_data.Length; i++)
                {
                    if (_data[i] != 0)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(obj, null))
                return false;

            if (!(obj is Hash))
                return false;

            var otherHash = (Hash)obj;

            var thisData = this._data;
            var otherData = otherHash._data;

            if (thisData.Length != otherData.Length)
            {
                return false;
            }

            for (int i = 0; i < thisData.Length; i++)
            {
                if (otherData[i] != thisData[i])
                {
                    return false;
                }
            }

            return true;
        }

        public override int GetHashCode() => (int)_data.ToUInt32(0);

        public byte[] ToByteArray()
        {
            var result = new byte[_data.Length];
            ByteArrayUtils.CopyBytes(_data, 0, result, 0, _data.Length);
            return result;
        }

        public override string ToString()
        {
            return Base16.Encode(ByteArrayUtils.ReverseBytes(_data));
        }

        public static readonly Hash Zero = new Hash();

        public Hash(byte[] value)
        {
            Throw.If(value == null, "value cannot be null");
            Throw.If(value.Length != Length, $"value must have length {Length}/{value.Length}");

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
            Throw.If(s.Length < 64, "string too short");

            var ch = char.ToUpper(s[1]);
            if (ch == 'X')
            {
                Throw.If(s[0] != '0', "invalid hexdecimal prefix");
                return Parse(s.Substring(2));
            }

            var ExpectedLength = Length * 2;
            Throw.If(s.Length != ExpectedLength, $"length of string must be {Length}");

            return new Hash(ByteArrayUtils.ReverseBytes(s.Decode()));
        }

        public static bool TryParse(string s, out Hash result)
        {
            if (string.IsNullOrEmpty(s))
            {
                result = Hash.Null;
                return false;
            }

            if (s.StartsWith("0x"))
            {
                return TryParse(s.Substring(2), out result);
            }
            if (s.Length != 64)
            {
                result = Hash.Null;
                return false;
            }

            try
            {
                byte[] data = Base16.Decode(s);

                result = new Hash(ByteArrayUtils.ReverseBytes(data));
                return true;
            }
            catch
            {
                result = Hash.Null;
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

        // If necessary pads the number to 32 bytes with zeros 
        public static implicit operator Hash(BigInteger val)
        {
            var src = val.ToByteArray();
            Throw.If(src.Length > Length, "number is too large");

            return FromBytes(src);
        }

        public static Hash FromBytes(byte[] input)
        {
            if (input.Length != Length) // NOTE this is actually problematic, better to separate into 2 methods
            {
                input = CryptoExtensions.Sha256(input);
            }

            var bytes = new byte[Length];
            Array.Copy(input, bytes, input.Length);
            return new Hash(bytes);
        }

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteByteArray(this._data);
        }

        public void UnserializeData(BinaryReader reader)
        {
            this._data = reader.ReadByteArray();
        }

        public static implicit operator BigInteger(Hash val)
        {
            var result = new byte[Hash.Length + 1];
            ByteArrayUtils.CopyBytes(val.ToByteArray(), 0, result, 0, Hash.Length);
            Console.WriteLine("biggi: " + new BigInteger(result));
            return new BigInteger(result);
        }

        public static Hash MerkleCombine(Hash A, Hash B)
        {
            var bytes = new byte[Hash.Length * 2];
            ByteArrayUtils.CopyBytes(A._data, 0, bytes, 0, Hash.Length);
            ByteArrayUtils.CopyBytes(B._data, 0, bytes, Hash.Length, Hash.Length);
            return Hash.FromBytes(bytes);
        }

        public static Hash FromString(string str)
        {
            var bytes = CryptoExtensions.Sha256(str);
            return new Hash(bytes);
        }

        public static Hash FromUnpaddedHex(string hash)
        {
            if (hash.StartsWith("0x"))
            {
                hash = hash.Substring(2);
            }

            var sb = new StringBuilder();
            sb.Append(hash);
            while (sb.Length < 64)
            {
                sb.Append('0');
                sb.Append('0');
            }

            var temp = sb.ToString();
            return Hash.Parse(temp);
        }
    }
}
