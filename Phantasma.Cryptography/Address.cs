using System.Linq;
using System.Collections.Generic;
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
        public static readonly Address Null = new Address(new byte[PublicKeyLength]);

        private byte[] _publicKey;
        public byte[] PublicKey
        {
            get
            {
                if (_publicKey == null)
                {
                    return Null.PublicKey;
                }

                return _publicKey;
            }

            private set
            {
                _publicKey = value;
            }
        }

        public const int PublicKeyLength = 32;

        // NOTE currently we only support interop chain names with 3 chars, but this could be expanded to support up to 10 chars
        public bool IsInterop => _publicKey != null && _publicKey[0] == (byte)'*' && _publicKey[4] == (byte)'*';

        private string _text;
        public string Text
        {
            get
            {
                if (string.IsNullOrEmpty(_text))
                {
                    var opcode = (byte)(IsInterop ? 102 : 74);
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

        public static bool operator ==(Address A, Address B) { return A.PublicKey.SequenceEqual(B.PublicKey); }

        public static bool operator !=(Address A, Address B) { return !A.PublicKey.SequenceEqual(B.PublicKey); }

        public override string ToString()
        {
            if (this == Null)
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

            Throw.If(opcode != 74 && opcode != 102, "Invalid address opcode");

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

        public void DecodeInterop(out string chainName, out byte[] data, int expectedDataLength)
        {
            Throw.If(expectedDataLength < 0, "invalid data length");
            Throw.If(!IsInterop, "must be an interop address");
 
            var sb = new StringBuilder();
            int i = 1;
            while (true)
            {
                if (i >= PublicKeyLength)
                {
                    throw new Exception("invalid interop address");
                }

                var ch = (char)_publicKey[i];
                if (ch == '*')
                {
                    break;
                }

                sb.Append(ch);
                i++;
            }

            if (sb.Length == 0)
            {
                throw new Exception("invalid interop address");
            }

            i++;
            chainName = sb.ToString();

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

        public static Address EncodeInterop(string chainSymbol, byte[] data)
        {
            var bytes = new byte[PublicKeyLength];
            bytes[0] = (byte)'*';
            int i = 1;
            foreach (var ch in chainSymbol)
            {
                bytes[i] = (byte)ch;
                i++;
            }
            bytes[i] = (byte)'*';
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
