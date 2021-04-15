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
using System.Collections.Generic;

namespace Phantasma.Cryptography
{
    public enum AddressKind
    {
        Invalid = 0,
        User = 1,
        System = 2,
        Interop = 3,
    }

    public struct Address: ISerializable
    {
        public static readonly Address Null = new Address(NullPublicKey);

        private static byte[] NullPublicKey => new byte[LengthInBytes];

        private byte[] _bytes;

        public const int LengthInBytes = 34;
        public const int MaxPlatformNameLength = 10;

        public AddressKind Kind => IsNull ? AddressKind.System : (_bytes[0] >= 3) ? AddressKind.Interop : (AddressKind)_bytes[0];

        public bool IsSystem => Kind == AddressKind.System;

        public bool IsInterop => Kind == AddressKind.Interop;

        public bool IsUser => Kind == AddressKind.User;

        public bool IsNull
        {
            get
            {
                if (_bytes == null)
                {
                    return true;
                }

                for (int i = 1; i < _bytes.Length; i++)
                {
                    if (_bytes[i] != 0)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        private string _text;

        private static Dictionary<byte[], string> _keyToTextCache = new Dictionary<byte[], string>(new ByteArrayComparer());

        public string Text
        {
            get
            {
                if (string.IsNullOrEmpty(_text))
                {
                    lock (_keyToTextCache) {
                        if (_keyToTextCache.ContainsKey(_bytes))
                        {
                            _text = _keyToTextCache[_bytes];
                        }
                    }

                    if (string.IsNullOrEmpty(_text))
                    {
                        char prefix;

                        switch (Kind)
                        {
                            case AddressKind.User: prefix = 'P'; break;
                            case AddressKind.Interop: prefix = 'X'; break;
                            default: prefix = 'S'; break;

                        }
                        _text = prefix + Base58.Encode(_bytes);
                        lock (_keyToTextCache) {
                            _keyToTextCache[_bytes] = _text;
                        }
                    }
                }

                return _text;
            }
        }

        private Address(byte[] publicKey)
        {
            Throw.IfNull(publicKey, "publicKey");
            if (publicKey.Length != LengthInBytes)
            {
                throw new Exception($"publicKey length must be {LengthInBytes}");
            }
            _bytes = new byte[LengthInBytes];
            Array.Copy(publicKey, this._bytes, LengthInBytes);
            this._text = null;
        }

        public static Address FromBytes(byte[] bytes)
        {
            return new Address(bytes);
        }

        public static Address FromKey(IKeyPair key)
        {
            var bytes = new byte[LengthInBytes];
            bytes[0] = (byte)AddressKind.User;

            if (key.PublicKey.Length == 32)
            {
                ByteArrayUtils.CopyBytes(key.PublicKey, 0, bytes, 2, 32);
            }
            else
            if (key.PublicKey.Length == 33)
            {
                ByteArrayUtils.CopyBytes(key.PublicKey, 0, bytes, 1, 33);
            }
            else
            {
                throw new Exception("Invalid public key length");
            }

            return new Address(bytes);
        }

        public static Address FromHash(string str)
        {
            var bytes = Encoding.UTF8.GetBytes(str);
            return FromHash(bytes);
        }

        public static Address FromHash(byte[] input)
        {
            var hash = CryptoExtensions.SHA256(input);
            var bytes = ByteArrayUtils.ConcatBytes(new byte[] { (byte)AddressKind.System, 0 }, hash);
            return new Address(bytes);
        }

        public static bool operator ==(Address A, Address B)
        {
            if (A._bytes == null)
            {
                return B._bytes == null;
            }

            if (B._bytes == null || A._bytes.Length != B._bytes.Length)
            {
                return false;
            }

            for (int i = 0; i < A._bytes.Length; i++)
            {
                if (A._bytes[i] != B._bytes[i])
                {
                    return false;
                }
            }
            return true;
        }

        public static bool operator !=(Address A, Address B)
        {
            if (A._bytes == null)
            {
                return B._bytes != null;
            }

            if (B._bytes == null || A._bytes.Length != B._bytes.Length)
            {
                return true;
            }

            for (int i = 0; i < A._bytes.Length; i++)
            {
                if (A._bytes[i] != B._bytes[i])
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

            var thisBytes = this._bytes;
            var otherBytes = otherAddress._bytes;

            if ((thisBytes == null) || (otherBytes == null))
            {
                if (thisBytes == null && otherBytes != null && otherAddress.IsNull)
                {
                    return true;
                }

                if (otherBytes == null && thisBytes != null && this.IsNull)
                {
                    return true;
                }

                return (thisBytes == null) == (otherBytes == null);
            }

            if (thisBytes.Length != otherBytes.Length) // failsafe, should never happen
            {
                return false;
            }

            for (int i=0; i<thisBytes.Length; i++)
            {
                if (thisBytes[i] != otherBytes[i])
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
                return (int)Murmur32.Hash(_bytes);
            }
        }

        public static Address FromWIF(string WIF)
        {
            var keyPair = PhantasmaKeys.FromWIF(WIF);
            return keyPair.Address;
        }

        private static Dictionary<string, Address> _textToAddressCache = new Dictionary<string, Address>();

        public static Address FromText(string text)
        {
            Address addr;

            lock (_textToAddressCache)
            {
                if (_textToAddressCache.ContainsKey(text))
                {
                    return _textToAddressCache[text];
                }

                var originalText = text;

                var prefix = text[0];

                text = text.Substring(1);
                var bytes = Base58.Decode(text);

                Throw.If(bytes.Length != LengthInBytes, "Invalid address data");

                addr = new Address(bytes);

                switch (prefix)
                {
                    case 'P':
                        Throw.If(addr.Kind != AddressKind.User, "address should be user");
                        break;

                    case 'S':
                        Throw.If(addr.Kind != AddressKind.System, "address should be system");
                        break;

                    case 'X':
                        Throw.If(addr.Kind < AddressKind.Interop, "address should be interop");
                        break;

                    default:
                        throw new Exception("invalid address prefix: " + prefix);
                }

                _textToAddressCache[originalText] = addr;
            }

            return addr;
        }

        public int GetSize()
        {
            return LengthInBytes;
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
            writer.WriteByteArray(this._bytes);
        }

        public void UnserializeData(BinaryReader reader)
        {
            this._bytes = reader.ReadByteArray();
            this._text = null;
        }

        public void DecodeInterop(out byte platformID, out byte[] publicKey)
        {
            platformID = this.PlatformID;
            publicKey = new byte[33];
            ByteArrayUtils.CopyBytes(_bytes, 1, publicKey, 0, publicKey.Length);
        }

        public byte PlatformID => (byte)(1 + _bytes[0] - AddressKind.Interop);

        public static Address FromInterop(byte platformID, byte[] publicKey)
        {
            Throw.If(publicKey == null || publicKey.Length != 33, "public key is invalid");
            Throw.If(platformID < 1, "invalid platform id");

            var bytes = new byte[LengthInBytes];
            bytes[0] = (byte)(AddressKind.Interop+platformID-1);
            ByteArrayUtils.CopyBytes(publicKey, 0, bytes, 1, publicKey.Length);
            return new Address(bytes);
        }

        public byte[] ToByteArray()
        {
            var bytes = new byte[LengthInBytes];
            if (_bytes != null)
            {
                if (_bytes.Length != LengthInBytes)
                {
                    throw new Exception("invalid address byte length");
                }
                ByteArrayUtils.CopyBytes(_bytes, 0, bytes, 0, _bytes.Length);
            }

            return bytes;
        }
    }
}
