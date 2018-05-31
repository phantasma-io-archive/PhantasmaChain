using System.Linq;

namespace Phantasma.Contracts.Types
{
    public struct Address
    {
        public byte[] publicKey { get; }

        public override bool Equals(object obj)
        {
            return ((Address)obj).publicKey.SequenceEqual(this.publicKey);
        }

        public override int GetHashCode()
        {
            return publicKey.GetHashCode();
        }

        public static bool operator ==(Address obj1, Address obj2)
        {
            return obj1.Equals(obj2);
        }

        // this is second one '!='
        public static bool operator !=(Address obj1, Address obj2)
        {
            return !obj1.Equals(obj2);
        }
    }
}
