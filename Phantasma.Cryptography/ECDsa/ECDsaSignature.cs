using Phantasma.Core;
using Phantasma.Core.Utils;
using Phantasma.Storage.Utils;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Phantasma.Cryptography.ECC
{
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

                var pubKey = ExtractPublicKeyFromAddress(address);

                if (CryptoExtensions.VerifySignatureECDsa(message, this.Bytes, pubKey))
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

            if (keypair.PublicKey.Length != 33)
            {
                throw new System.Exception("public key must be 33 bytes");
            }

            var signature = CryptoExtensions.SignECDsa(message, keypair.PrivateKey, keypair.PublicKey);
            return new ECDsaSignature(signature, curve);
        }

        public static byte[] ExtractPublicKeyFromAddress(Address address)
        {
            var pubKey = address.ToByteArray().Skip(1).ToArray();
            if (pubKey[0] != 2 && pubKey[0] != 3)
            {
                throw new System.Exception("invalid ECDsa public key");
            }

            return pubKey;
        }
    }
}