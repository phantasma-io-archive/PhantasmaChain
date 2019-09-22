using System.Linq;
using Phantasma.Core;
using System;
using Phantasma.Numerics;
using Phantasma.Cryptography.Hashing;
using Phantasma.Core.Utils;
using Phantasma.Storage;
using System.IO;
using Phantasma.Storage.Utils;
using System.Text;

namespace Phantasma.Cryptography
{
    public struct Address: ISerializable
    {
        public static readonly Address Null = new Address(NullPublicKey);

        private static byte[] NullPublicKey => new byte[PublicKeyLength];

        private byte[] _publicKey;
        public byte[] PublicKey
        {
            get
            {
                if (_publicKey == null)
                {
                    return NullPublicKey;
                }

                return _publicKey;
            }

            private set
            {
                _publicKey = value;
            }
        }

        public const int PublicKeyLength = 32;
        public const int MaxPlatformNameLength = 10;

        public bool IsSystem => _publicKey != null && (_publicKey.Length > 0 && _publicKey[0] == SystemOpcode || IsNull);

        // NOTE currently we only support interop chain names with 3 chars, but this could be expanded to support up to 10 chars
        public bool IsInterop => _publicKey != null && _publicKey.Length > 0 && _publicKey[0] == InteropOpcode;

        public bool IsUser => !IsSystem && !IsInterop;

        public bool IsNull
        {
            get
            {
                if (_publicKey == null)
                {
                    return true;
                }

                for (int i = 0; i < _publicKey.Length; i++)
                {
                    if (_publicKey[i] != 0)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        private const byte UserOpcode = 75;
        private const byte SystemOpcode = 85;
        private const byte InteropOpcode = 102;

        private string _text;
        public string Text
        {
            get
            {
                if (string.IsNullOrEmpty(_text))
                {
                    byte opcode;

                    if (IsSystem)
                    {
                        opcode = SystemOpcode;
                    }
                    else
                    if (IsInterop)
                    {
                        opcode = InteropOpcode;
                    }
                    else
                    {
                        opcode = UserOpcode;
                    }

                    var bytes = ByteArrayUtils.ConcatBytes(new byte[] { opcode }, PublicKey);
                    _text = Base58.Encode(bytes);
                }

                return _text;
            }
        }

        public Address(byte[] publicKey)
        {
            Throw.IfNull(publicKey, "publicKey");
            Throw.If(publicKey.Length != PublicKeyLength, $"publicKey length must be {PublicKeyLength}");
            _publicKey = new byte[PublicKeyLength];
            Array.Copy(publicKey, this._publicKey, PublicKeyLength);
            this._text = null;
        }

        public static Address FromHash(string str)
        {
            var bytes = Encoding.UTF8.GetBytes(str);
            return FromHash(bytes);
        }

        public static Address FromHash(byte[] bytes)
        {
            var hash = CryptoExtensions.SHA256(bytes);
            hash[0] = SystemOpcode;
            return new Address(hash);
        }

        public static bool operator ==(Address A, Address B)
        {
            if (A._publicKey == null)
            {
                return B._publicKey == null;
            }

            if (B._publicKey == null || A._publicKey.Length != B._publicKey.Length)
            {
                return false;
            }

            for (int i = 0; i < A._publicKey.Length; i++)
            {
                if (A._publicKey[i] != B._publicKey[i])
                {
                    return false;
                }
            }
            return true;
        }

        public static bool operator !=(Address A, Address B)
        {
            if (A._publicKey == null)
            {
                return B._publicKey != null;
            }

            if (B._publicKey == null || A._publicKey.Length != B._publicKey.Length)
            {
                return true;
            }

            for (int i = 0; i < A._publicKey.Length; i++)
            {
                if (A._publicKey[i] != B._publicKey[i])
                {
                    return true;
                }
            }
            return false;
        }

        public override string ToString()
        {
            if (this.IsNull)
            {
                return "[Null address]";
            }

            return this.Text;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Address))
            {
                return false;
            }

            var otherAddress = (Address)obj;

            var thisKey = this.PublicKey;
            var otherKey = otherAddress.PublicKey;

            if (thisKey.Length != otherKey.Length) // failsafe, should never happen
            {
                return false;
            }

            for (int i=0; i<thisKey.Length; i++)
            {
                if (thisKey[i] != otherKey[i])
                {
                    return false;
                }
            }

            return true;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (int)Murmur32.Hash(PublicKey);
            }
        }

        public static Address FromWIF(string WIF)
        {
            var keyPair = KeyPair.FromWIF(WIF);
            return keyPair.Address;
        }

        public static Address FromText(string text)
        {
            Throw.If(text.Length != 45, "Invalid address length");

            var bytes = Base58.Decode(text);
            var opcode = bytes[0];

            Throw.If(opcode != UserOpcode && opcode != SystemOpcode && opcode != InteropOpcode, "Invalid address opcode");

            return new Address(bytes.Skip(1).ToArray());
        }

        public static Address FromScript(byte[] script)
        {
            var hash = script.SHA256();
            return new Address(hash);
        }

        public int GetSize()
        {
            return PublicKeyLength;
        }

        public static bool IsValidAddress(string text)
        {
            try
            {
                var addr = Address.FromText(text);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteByteArray(this._publicKey);
        }

        public void UnserializeData(BinaryReader reader)
        {
            this._publicKey = reader.ReadByteArray();
            this._text = null;
        }

        public void DecodeInterop(out string platformName, out byte[] data, int expectedDataLength)
        {
            Throw.If(expectedDataLength < 0, "invalid data length");
            Throw.If(expectedDataLength > 27, "data is too large");
            Throw.If(!IsInterop, "must be an interop address");
 
            var sb = new StringBuilder();
            int i = 1;
            while (true)
            {
                if (i >= PublicKeyLength)
                {
                    throw new Exception("invalid interop address");
                }

                if (_publicKey[i] == InteropOpcode)
                {
                    break;
                }

                var ch = (char)_publicKey[i];
                sb.Append(ch);
                i++;
            }

            if (sb.Length == 0)
            {
                throw new Exception("invalid interop address");
            }

            i++;
            platformName = sb.ToString();

            if (expectedDataLength > 0)
            {
                data = new byte[expectedDataLength];
                for (int n = 0; n < expectedDataLength; n++)
                {
                    data[n] = _publicKey[i + n];
                }
            }
            else
            {
                data = null;
            }
        }

        public static Address EncodeInterop(string platformName, byte[] data)
        {
            Throw.If(string.IsNullOrEmpty(platformName), "platform name cant be null");
            Throw.If(platformName.Length > MaxPlatformNameLength, "platform name is too big");

            var bytes = new byte[PublicKeyLength];
            bytes[0] = InteropOpcode;
            int i = 1;
            foreach (var ch in platformName)
            {
                bytes[i] = (byte)ch;
                i++;
            }
            bytes[i] = InteropOpcode;
            i++;

            foreach (var ch in data)
            {
                bytes[i] = (byte)ch;
                i++;
            }

            return new Address(bytes);
        }
    }
}
