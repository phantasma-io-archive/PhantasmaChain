using System.Linq;
using System.Collections.Generic;
using Phantasma.Core;
using System;
using Phantasma.Numerics;
using Phantasma.Cryptography.Hashing;

namespace Phantasma.Cryptography
{
    public struct Address
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

        private string _text;
        public string Text
        {
            get
            {
                if (string.IsNullOrEmpty(_text))
                {
                    byte opcode = 74;
                    var bytes = new byte[] { opcode }.Concat(PublicKey).ToArray();
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
                return "[Burner address]";
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
            return Murmur32.Hash(PublicKey);
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

            Throw.If(opcode != 74, "Invalid address opcode");

            return new Address(bytes.Skip(1).ToArray());
        }

        public static Address FromScript(byte[] script)
        {
            var hash = script.Sha256();
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
    }
}
