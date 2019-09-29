using Phantasma.Core;
using Phantasma.Core.Utils;
using Phantasma.Storage.Utils;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Phantasma.Cryptography.ECC
{
    public enum ECDsaCurve
    {
        Secp256r1,
        Secp256k1,
    }

    public class ECDsaSignature : Signature
    {
        public byte[] Bytes { get; private set; }
        public ECDsaCurve Curve { get; private set; }

        public override SignatureKind Kind =>  SignatureKind.ECDSA;

        internal ECDsaSignature()
        {
            this.Bytes = null;
        }

        public ECDsaSignature(byte[] bytes, ECDsaCurve curve)
        {
            this.Bytes = bytes;
            this.Curve = curve;
        }

        public override bool Verify(byte[] message, IEnumerable<Address> addresses)
        {
            if (Curve != ECDsaCurve.Secp256r1)
            {
                throw new System.Exception($"Support for verifying ECDsa with curve {Curve} not implemented!");
            }

            foreach (var address in addresses)
            {
                if (!address.IsUser)
                {
                    continue;
                }

                var pubKeyBytes = ByteArrayUtils.ConcatBytes(new byte[] { 2 }, address.PublicKey);
                //var pubKey = ECC.ECPoint.DecodePoint(pubKeyBytes, Curve);
                if (CryptoExtensions.VerifySignatureECDsa(message, this.Bytes, pubKeyBytes))
                {
                    return true;
                }
            }

            return false;
        }

        public override void SerializeData(BinaryWriter writer)
        {
            writer.Write((byte)Curve);
            writer.WriteByteArray(this.Bytes);
        }

        public override void UnserializeData(BinaryReader reader)
        {
            this.Curve = (ECDsaCurve)reader.ReadByte();
            this.Bytes = reader.ReadByteArray();
        }

        public static ECDsaSignature Generate(IKeyPair keypair, byte[] message, ECDsaCurve curve)
        {
            Throw.If(curve != ECDsaCurve.Secp256r1, "curve support not implemented for " + curve);

            var pubKeyBytes = ByteArrayUtils.ConcatBytes(new byte[] { 2 }, keypair.PublicKey);
            var point = ECPoint.DecodePoint(pubKeyBytes, ECCurve.Secp256r1);
            var pubKey = point.EncodePoint(false).Skip(1).ToArray();
            var signature = CryptoExtensions.SignECDsa(message, keypair.PrivateKey, pubKey);
            return new ECDsaSignature(signature, curve);
        }
    }
}