using System.Linq;
using System.Collections.Generic;

namespace Phantasma.VM.Types
{
    public struct Address
    {
        public byte[] PublicKey { get; private set; }

        public Address(byte[] publicKey)
        {
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
