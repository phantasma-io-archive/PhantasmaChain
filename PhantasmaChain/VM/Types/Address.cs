using System.Linq;
using System.Collections.Generic;
using Phantasma.Utils;

namespace Phantasma.VM.Types
{
    public struct Address
    {
        public static readonly Address Null = new Address(new byte[32]);

        public byte[] PublicKey { get; private set; }

        private string _text;
        public string Text
        {
            get
            {
                if (string.IsNullOrEmpty(_text))
                {
                    _text = this.PublicKey.PublicKeyToAddress();
                }

                return _text;
            }
        }

        public Address(byte[] publicKey)
        {
            this._text = null;
            this.PublicKey = publicKey;
        }

        public static bool operator ==(Address A, Address B) { return A.PublicKey.SequenceEqual(B.PublicKey); }

        public static bool operator !=(Address A, Address B) { return !A.PublicKey.SequenceEqual(B.PublicKey); }

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
    }
}
