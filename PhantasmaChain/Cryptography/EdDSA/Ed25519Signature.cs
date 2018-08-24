using Phantasma.VM.Types;
using System.Collections.Generic;

namespace Phantasma.Cryptography.EdDSA
{
    public class Ed25519Signature : Signature
    {
        public override SignatureKind Kind => SignatureKind.Ed25519;

        public Ed25519Signature(byte[] bytes): base(bytes)
        {

        }

        public override bool Verify(byte[] message, IEnumerable<Address> addresses)
        {
            foreach (var address in addresses)
            {
                if (Ed25519.Verify(this.Bytes, message, address.PublicKey))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
