using System.Linq;
using System.Collections.Generic;
using Phantasma.Utils;
using Phantasma.Mathematics;

namespace Phantasma.VM.Types
{
    public struct Address: IInteropObject
    {
        public static readonly Address Null = new Address(new byte[32]);

        public byte[] PublicKey { get; private set; }

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
                    _text =  Base58.Encode(bytes);
                }

                return _text;
            }
        }

        public Address(byte[] publicKey)
        {
            Throw.IfNull(publicKey, "publicKey");
            Throw.If(publicKey.Length != PublicKeyLength, $"publicKey length must be {PublicKeyLength}");
            this._text = null;
            this.PublicKey = publicKey;
        }

        public static bool operator ==(Address A, Address B) { return A.PublicKey.SequenceEqual(B.PublicKey); }

        public static bool operator !=(Address A, Address B) { return !A.PublicKey.SequenceEqual(B.PublicKey); }

        public override string ToString()
        {
            return this.Text;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Address))
            {
                return false;
            }

            var address = (Address)obj;
            return EqualityComparer<byte[]>.Default.Equals(PublicKey, address.PublicKey);
        }

        public override int GetHashCode()
        {
            return PublicKey.GetHashCode();
        }

        public static Address FromText(string text)
        {
            var bytes = Base58.Decode(text);
            var opcode = bytes[0];

            Throw.If(opcode != 74, "Invalid address");

            return new Address(bytes.Skip(1).ToArray());
        }

        public int GetSize()
        {
            return PublicKeyLength;
        }
    }
}
