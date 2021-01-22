using Phantasma.Core;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace Phantasma.Cryptography.EdDSA
{
    public static class Ed25519
    {
        public const int PublicKeySizeInBytes = 32;
        public const int SignatureSizeInBytes = 64;
        public const int ExpandedPrivateKeySizeInBytes = 32 * 2;
        public const int PrivateKeySeedSizeInBytes = 32;
        public const int SharedKeySizeInBytes = 32;

        public static bool Verify(byte[] signature, byte[] message, byte[] publicKey)
        {
            Throw.If(signature == null, "signature");
            Throw.If(message == null, "message");
            Throw.If(publicKey == null, "publicKey");
            Throw.If(signature.Length != SignatureSizeInBytes, $"Signature size must be {SignatureSizeInBytes}");
            Throw.If(publicKey.Length != PublicKeySizeInBytes, $"Public key size must be {PublicKeySizeInBytes}");

            var signer = new Ed25519Signer();

            var publicKeyParameters = new Ed25519PublicKeyParameters(publicKey, 0);

            signer.Init(false, publicKeyParameters);
            signer.BlockUpdate(message, 0, message.Length);
            return signer.VerifySignature(signature);
        }

        public static byte[] Sign(byte[] message, byte[] expandedPrivateKey)
        {
            var signer = new Ed25519Signer();

            var privateKeyParameters = new Ed25519PrivateKeyParameters(expandedPrivateKey, 0);

            signer.Init(true, privateKeyParameters);
            signer.BlockUpdate(message, 0, message.Length);

            return signer.GenerateSignature();
        }

        public static byte[] PublicKeyFromSeed(byte[] privateKeySeed)
        {
            var privateKeyParameters = new Ed25519PrivateKeyParameters(privateKeySeed, 0);
            Ed25519PublicKeyParameters publicKeyParameters = privateKeyParameters.GeneratePublicKey();
            
            return publicKeyParameters.GetEncoded();
        }
    }
}