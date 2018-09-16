using System.Collections.Generic;

namespace Phantasma.Cryptography
{
    public abstract class Signature
    {
        public abstract SignatureKind Kind { get; }

        /// <summary>
        /// Checks if this transaction was signed by at least one of the addresses
        /// </summary>
        public abstract bool Verify(byte[] message, IEnumerable<Address> addresses);

        public bool Verify(byte[] message, Address address)
        {
            return Verify(message, new Address[] { address });
        }
    }
}
