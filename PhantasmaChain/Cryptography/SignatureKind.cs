using System;
using System.Collections.Generic;
using System.Text;

namespace Phantasma.Cryptography
{
    public enum SignatureKind
    {
        None,
        Ed25519,
        Ring,
        ECDSA
    }
}
