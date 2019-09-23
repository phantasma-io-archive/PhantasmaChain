using Phantasma.Storage.Utils;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Phantasma.Cryptography.ECC
{
    public class ECDsaSignature : Signature
    {
        public static readonly ECCurve Curve = ECCurve.Secp256r1;

        public byte[] Bytes { get; private set; }

        public override SignatureKind Kind =>  SignatureKind.ECDSA;

        internal ECDsaSignature()
        {
            this.Bytes = null;
        }

        public ECDsaSignature(byte[] bytes)
        {
            this.Bytes = bytes;
        }

        public override bool Verify(byte[] message, IEnumerable<Address> addresses)
        {
            foreach (var address in addresses)
            {
                if (!address.IsUser)
                {
                    continue;
                }

                var pubKeyBytes = address.PublicKey.Skip(1).ToArray();
                var pubKey = ECC.ECPoint.DecodePoint(pubKeyBytes, Curve);
                if (ECDsa.VerifySignature(message, this.Bytes, Curve, pubKey))
                {
                    return true;
                }
            }

            return false;
        }

        public override void SerializeData(BinaryWriter writer)
        {
            writer.WriteByteArray(this.Bytes);
        }

        public override void UnserializeData(BinaryReader reader)
        {
            this.Bytes = reader.ReadByteArray();
        }
    }
}