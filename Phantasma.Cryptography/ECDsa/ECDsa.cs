using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using System;
using System.Linq;

namespace Phantasma.Cryptography.ECC
{
    public enum ECDsaCurve
    {
        Secp256r1,
        Secp256k1,
    }

    public static class ECDsa
    {
        public static byte[] GetPublicKey(byte[] privateKey, bool compressed, ECDsaCurve curve)
        {
            X9ECParameters ecCurve;
            switch (curve)
            {
                case ECDsaCurve.Secp256k1:
                    ecCurve = SecNamedCurves.GetByName("secp256k1");
                    break;
                default:
                    ecCurve = SecNamedCurves.GetByName("secp256r1");
                    break;
            }

            var dom = new ECDomainParameters(ecCurve.Curve, ecCurve.G, ecCurve.N, ecCurve.H);

            var d = new BigInteger(1, privateKey);
            var q = dom.G.Multiply(d);

            var publicParams = new ECPublicKeyParameters(q, dom);
            return publicParams.Q.GetEncoded(compressed);
        }

        public static byte[] Sign(byte[] message, byte[] prikey, ECDsaCurve curve)
        {
            var signer = SignerUtilities.GetSigner("SHA256withECDSA");
            X9ECParameters ecCurve;
            switch (curve)
            {
                case ECDsaCurve.Secp256k1:
                    ecCurve = SecNamedCurves.GetByName("secp256k1");
                    break;
                default:
                    ecCurve = SecNamedCurves.GetByName("secp256r1");
                    break;
            }
            var dom = new ECDomainParameters(ecCurve.Curve, ecCurve.G, ecCurve.N, ecCurve.H);
            var privateKeyParameters = new ECPrivateKeyParameters(new BigInteger(1, prikey), dom);

            signer.Init(true, privateKeyParameters);
            signer.BlockUpdate(message, 0, message.Length);
            var signature = signer.GenerateSignature();

            return FromDER(signature);
        }

        public static bool Verify(byte[] message, byte[] signature, byte[] pubkey, ECDsaCurve curve)
        {
            var signer = SignerUtilities.GetSigner("SHA256withECDSA");
            X9ECParameters ecCurve;
            switch (curve)
            {
                case ECDsaCurve.Secp256k1:
                    ecCurve = SecNamedCurves.GetByName("secp256k1");
                    break;
                default:
                    ecCurve = SecNamedCurves.GetByName("secp256r1");
                    break;
            }
            var dom = new ECDomainParameters(ecCurve.Curve, ecCurve.G, ecCurve.N, ecCurve.H);

            ECPublicKeyParameters publicKeyParameters;
            if (pubkey.Length == 33)
                publicKeyParameters = new ECPublicKeyParameters(dom.Curve.DecodePoint(pubkey), dom);
            else
                publicKeyParameters = new ECPublicKeyParameters(dom.Curve.CreatePoint(new BigInteger(1, pubkey.Take(pubkey.Length / 2).ToArray()), new BigInteger(1, pubkey.Skip(pubkey.Length / 2).ToArray())), dom);

            signer.Init(false, publicKeyParameters);
            signer.BlockUpdate(message, 0, message.Length);

            return signer.VerifySignature(ToDER(signature));
        }

        public static byte[] FromDER(byte[] signature)
        {
            var decoder = new Asn1InputStream(signature);
            var seq = decoder.ReadObject() as DerSequence;
            if (seq == null || seq.Count != 2)
                throw new FormatException("Invalid DER Signature");
            var R = ((DerInteger)seq[0]).Value.ToByteArrayUnsigned();
            var S = ((DerInteger)seq[1]).Value.ToByteArrayUnsigned();

            byte[] concatenated = new byte[R.Length + S.Length];
            Buffer.BlockCopy(R, 0, concatenated, 0, R.Length);
            Buffer.BlockCopy(S, 0, concatenated, R.Length, S.Length);

            return concatenated;
        }
        public static byte[] ToDER(byte[] signature)
        {
            // We convert from concatenated "raw" R + S format to DER format that Bouncy Castle uses.
            return new DerSequence(
                // first 32 bytes is "R" number
                new DerInteger(new BigInteger(1, signature.Take(32).ToArray())),
                // last 32 bytes is "S" number
                new DerInteger(new BigInteger(1, signature.Skip(32).ToArray())))
                .GetDerEncoded();
        }
    }
}