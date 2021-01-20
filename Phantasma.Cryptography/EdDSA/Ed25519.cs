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
            //var curve = Org.BouncyCastle.Crypto.EC.CustomNamedCurves.GetByName("curve25519");

            var publicKeyParameters = new Ed25519PublicKeyParameters(publicKey, 0);

            signer.Init(false, publicKeyParameters);
            signer.BlockUpdate(message, 0, message.Length);
            return signer.VerifySignature(signature);
        }

        public static byte[] Sign(byte[] message, byte[] expandedPrivateKey)
        {
            var signer = new Ed25519Signer();
            //var curve = Org.BouncyCastle.Crypto.EC.CustomNamedCurves.GetByName("curve25519");

            var privateKeyParameters = new Ed25519PrivateKeyParameters(expandedPrivateKey, 0);

            signer.Init(true, privateKeyParameters);
            signer.BlockUpdate(message, 0, message.Length);

            return signer.GenerateSignature();
        }

        public static byte[] PublicKeyFromSeed(byte[] privateKeySeed)
        {
            byte[] privateKey;
            byte[] publicKey;
            KeyPairFromSeed(out publicKey, out privateKey, privateKeySeed);
            CryptoExtensions.Wipe(privateKey);
            return publicKey;
        }

        public static byte[] ExpandedPrivateKeyFromSeed(byte[] privateKeySeed)
        {
            byte[] privateKey;
            byte[] publicKey;
            KeyPairFromSeed(out publicKey, out privateKey, privateKeySeed);
            CryptoExtensions.Wipe(publicKey);
            return privateKey;
        }

        public static void KeyPairFromSeed(out byte[] publicKey, out byte[] expandedPrivateKey, byte[] privateKeySeed)
        {
            Throw.If(privateKeySeed == null, "privateKeySeed");
            Throw.If(privateKeySeed.Length != PrivateKeySeedSizeInBytes, $"privateKeySeed length should be {PrivateKeySeedSizeInBytes}");

            var pk = new byte[PublicKeySizeInBytes];
            var sk = new byte[ExpandedPrivateKeySizeInBytes];
            Ed25519Operations.crypto_sign_keypair(pk, 0, sk, 0, privateKeySeed, 0);
            publicKey = pk;
            expandedPrivateKey = sk;
        }
    }
}