using Phantasma.Cryptography.Hashing;
using Phantasma.Mathematics;
using Phantasma.Utils;
using System;

namespace Phantasma.Cryptography
{
    public static class Ed25519
    {
        public const int PublicKeySizeInBytes = 32;
        public const int SignatureSizeInBytes = 64;
        public const int ExpandedPrivateKeySizeInBytes = 32 * 2;
        public const int PrivateKeySeedSizeInBytes = 32;
        public const int SharedKeySizeInBytes = 32;

        public static bool Verify(ArraySegment<byte> signature, ArraySegment<byte> message, ArraySegment<byte> publicKey)
        {
            Throw.If(signature.Count != SignatureSizeInBytes, $"Signature size must be {SignatureSizeInBytes}");
            Throw.If(publicKey.Count != PublicKeySizeInBytes, $"Public key size must be {PublicKeySizeInBytes}");
            return Ed25519Operations.VerifySignature(signature.Array, signature.Offset, message.Array, message.Offset, message.Count, publicKey.Array, publicKey.Offset);
        }

        public static bool Verify(byte[] signature, byte[] message, byte[] publicKey)
        {
            Throw.If(signature == null, "signature");
            Throw.If(message == null, "message");
            Throw.If(publicKey == null, "publicKey");
            Throw.If(signature.Length != SignatureSizeInBytes, $"Signature size must be {SignatureSizeInBytes}");
            Throw.If(publicKey.Length != PublicKeySizeInBytes, $"Public key size must be {PublicKeySizeInBytes}");

            return Ed25519Operations.VerifySignature(signature, 0, message, 0, message.Length, publicKey, 0);
        }

        public static void Sign(ArraySegment<byte> signature, ArraySegment<byte> message, ArraySegment<byte> expandedPrivateKey)
        {
            Throw.If(signature.Array == null, "signature.Array");
            Throw.If(signature.Count != SignatureSizeInBytes, "signature.Count");
            Throw.If(expandedPrivateKey.Array == null, "expandedPrivateKey.Array");
            Throw.If(expandedPrivateKey.Count != ExpandedPrivateKeySizeInBytes, "expandedPrivateKey.Count");
            Throw.If(message.Array == null, "message.Array");

            Ed25519Operations.GenerateSignature(signature.Array, signature.Offset, message.Array, message.Offset, message.Count, expandedPrivateKey.Array, expandedPrivateKey.Offset);
        }

        public static byte[] Sign(byte[] message, byte[] expandedPrivateKey)
        {
            var signature = new byte[SignatureSizeInBytes];
            Sign(new ArraySegment<byte>(signature), new ArraySegment<byte>(message), new ArraySegment<byte>(expandedPrivateKey));
            return signature;
        }

        public static byte[] PublicKeyFromSeed(byte[] privateKeySeed)
        {
            byte[] privateKey;
            byte[] publicKey;
            KeyPairFromSeed(out publicKey, out privateKey, privateKeySeed);
            CryptoUtils.Wipe(privateKey);
            return publicKey;
        }

        public static byte[] ExpandedPrivateKeyFromSeed(byte[] privateKeySeed)
        {
            byte[] privateKey;
            byte[] publicKey;
            KeyPairFromSeed(out publicKey, out privateKey, privateKeySeed);
            CryptoUtils.Wipe(publicKey);
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

        public static void KeyPairFromSeed(ArraySegment<byte> publicKey, ArraySegment<byte> expandedPrivateKey, ArraySegment<byte> privateKeySeed)
        {
            if (publicKey.Array == null)
                throw new ArgumentNullException("publicKey.Array");
            if (expandedPrivateKey.Array == null)
                throw new ArgumentNullException("expandedPrivateKey.Array");
            if (privateKeySeed.Array == null)
                throw new ArgumentNullException("privateKeySeed.Array");
            if (publicKey.Count != PublicKeySizeInBytes)
                throw new ArgumentException("publicKey.Count");
            if (expandedPrivateKey.Count != ExpandedPrivateKeySizeInBytes)
                throw new ArgumentException("expandedPrivateKey.Count");
            if (privateKeySeed.Count != PrivateKeySeedSizeInBytes)
                throw new ArgumentException("privateKeySeed.Count");
            Ed25519Operations.crypto_sign_keypair(
                publicKey.Array, publicKey.Offset,
                expandedPrivateKey.Array, expandedPrivateKey.Offset,
                privateKeySeed.Array, privateKeySeed.Offset);
        }

        public static byte[] KeyExchange(byte[] publicKey, byte[] privateKey)
        {
            var sharedKey = new byte[SharedKeySizeInBytes];
            KeyExchange(new ArraySegment<byte>(sharedKey), new ArraySegment<byte>(publicKey), new ArraySegment<byte>(privateKey));
            return sharedKey;
        }

        //Needs more testing
        public static void KeyExchange(ArraySegment<byte> sharedKey, ArraySegment<byte> publicKey, ArraySegment<byte> privateKey)
        {
            Throw.If(sharedKey.Array == null, "sharedKey.Array");
            Throw.If(publicKey.Array == null, "publicKey.Array");
            Throw.If(privateKey.Array == null, "privateKey");
            Throw.If(sharedKey.Count != 32, "sharedKey.Count != 32");
            Throw.If(publicKey.Count != 32, "publicKey.Count != 32");
            Throw.If(privateKey.Count != 64, "privateKey.Count != 64");

            FieldElement montgomeryX, edwardsY, edwardsZ, sharedMontgomeryX;
            FieldOperations.fe_frombytes(out edwardsY, publicKey.Array, publicKey.Offset);
            FieldOperations.fe_1(out edwardsZ);
            EdwardsToMontgomeryX(out montgomeryX, ref edwardsY, ref edwardsZ);
            byte[] h = SHA512.Hash(privateKey.Array, privateKey.Offset, 32);//ToDo: Remove alloc
            ScalarOperations.sc_clamp(h, 0);
            MontgomeryOperations.scalarmult(out sharedMontgomeryX, h, 0, ref montgomeryX);
            CryptoUtils.Wipe(h);
            FieldOperations.fe_tobytes(sharedKey.Array, sharedKey.Offset, ref sharedMontgomeryX);
            KeyExchangeOutputHashNaCl(sharedKey.Array, sharedKey.Offset);
        }

        internal static void EdwardsToMontgomeryX(out FieldElement montgomeryX, ref FieldElement edwardsY, ref FieldElement edwardsZ)
        {
            FieldElement tempX, tempZ;
            FieldOperations.fe_add(out tempX, ref edwardsZ, ref edwardsY);
            FieldOperations.fe_sub(out tempZ, ref edwardsZ, ref edwardsY);
            FieldOperations.fe_invert(out tempZ, ref tempZ);
            FieldOperations.fe_mul(out montgomeryX, ref tempX, ref tempZ);
        }

        private static readonly byte[] _zero16 = new byte[16];
        
        // hashes like the NaCl paper says instead i.e. HSalsa(x,0)
        internal static void KeyExchangeOutputHashNaCl(byte[] sharedKey, int offset)
        {
            Salsa20.HSalsa20(sharedKey, offset, sharedKey, offset, _zero16, 0);
        }
    }
}