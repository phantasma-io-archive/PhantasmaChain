using Phantasma.VM.Types;
using System;
using System.Collections.Generic;
using System.Text;

namespace Phantasma.Cryptography
{
    public class Ed25519Signature : Signature
    {
        public override SignatureKind Kind => SignatureKind.Ed25519;

        public Ed25519Signature(byte[] bytes): base(bytes)
        {

        }

        public override bool Verify(byte[] message, Address address)
        {
            return Ed25519.Verify(this.Bytes, message, address.PublicKey);
        }
    }
}
