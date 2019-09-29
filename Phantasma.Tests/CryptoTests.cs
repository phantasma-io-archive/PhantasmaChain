using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Linq;
using System.Text;

using Phantasma.Cryptography;
using Phantasma.Core.Utils;
using Phantasma.Numerics;
using Phantasma.Cryptography.Ring;
using Phantasma.Cryptography.ECC;
using Phantasma.Neo.Core;

namespace Phantasma.Tests
{
    [TestClass]
    public class CryptoTests
    {
        [TestMethod]
        public void HashClass()
        {
            var bytes = new byte[32];
            var rnd = new Random();
            rnd.NextBytes(bytes);

            var hash = new Hash(bytes);

            Assert.IsTrue(hash.ToByteArray().Length == 32);
            Assert.IsTrue(hash.ToByteArray().SequenceEqual(bytes));

            bytes = new byte[32];
            BigInteger number;

            do
            {
                rnd.NextBytes(bytes);
                number = BigInteger.FromUnsignedArray(bytes, isPositive: true);
            } while (number <= 0);

            hash = number;
            Assert.IsTrue(hash.ToByteArray().Length == 32);

            BigInteger other = hash;
            Assert.IsTrue(number == other);

            Assert.IsTrue(Hash.TryParse("FFFFAAAAFFFFBBBBFFFFAAAAFFFFBBBBFFFFAAAAFFFFBBBBFFFFAAAAFFFFBBBB", out hash));
            Assert.IsTrue(Hash.TryParse("0xFFFFAAAAFFFFBBBBFFFFAAAAFFFFBBBBFFFFAAAAFFFFBBBBFFFFAAAAFFFFBBBB", out hash));
            Assert.IsFalse(Hash.TryParse("FFFFAAAXFFFFBBBBFFFFAAAAFFFFBBBBFFFFAAAAFFFFBBBBFFFFAAAAFFFFBBBB", out hash));
            Assert.IsFalse(Hash.TryParse("0xFFFFAAAAFFFFBBBBFFFFAAAAFFFFBBBBFFFFAAAAFFFFBBBBFFFFAAAAFFFFBBB", out hash));
        }

        [TestMethod]
        public void EdDSA()
        {
            var keys = PhantasmaKeys.Generate();
            Assert.IsTrue(keys.PrivateKey.Length == PhantasmaKeys.PrivateKeyLength);

            var msg = "Hello phantasma";

            var msgBytes = Encoding.ASCII.GetBytes(msg);
            var signature = keys.Sign(msgBytes);

            var targetAddress = Address.FromKey(keys);

            var verified = signature.Verify(msgBytes, targetAddress);
            Assert.IsTrue(verified);

            // make sure that Verify fails for other addresses
            var otherKeys = PhantasmaKeys.Generate();
            Assert.IsFalse(otherKeys.Address == targetAddress);
            verified = signature.Verify(msgBytes, otherKeys.Address);
            Assert.IsFalse(verified);
        }

        [TestMethod]
        public void ECDsa()
        {
            var privateKey = Base16.Decode("6f6784731c4e526c97fa6a97b6f22e96f307588c5868bc2c545248bc31207eb1");
            Assert.IsTrue(privateKey.Length == 32);

            var curve = ECCurve.Secp256r1;

            var publicKey = curve.G * privateKey;

            var msg = "Hello phantasma";

            var msgBytes = Encoding.ASCII.GetBytes(msg);

            var signer = new ECDsa(privateKey, ECCurve.Secp256r1);
            var signature = signer.GenerateSignature(msgBytes);
            Assert.IsNotNull(signature);

            var verified = Cryptography.ECC.ECDsa.VerifySignature(msgBytes, signature, curve, publicKey);
            Assert.IsTrue(verified);

            // make sure that Verify fails for other addresses

            var otherPrivateKey = Base16.Decode("97b6f22e96f307588c5868bc2c545248bc31207eb16f6784731c4e526c97fa6a");
            Assert.IsTrue(otherPrivateKey.Length == 32);

            var otherPublicKey = ECCurve.Secp256r1.G * otherPrivateKey;

            verified = Cryptography.ECC.ECDsa.VerifySignature(msgBytes, signature, curve, otherPublicKey);
            Assert.IsFalse(verified);
        }

        [TestMethod]
        public void RingSignatures()
        {
            var rand = new Random();

            int participants = 5;
            var messages = new[] { "hello", "phantasma chain", "welcome to the future" }.Select(Encoding.UTF8.GetBytes).ToArray();
            var keys = Enumerable.Range(0, participants).Select(i => RingSignature.GenerateKeyPair(PhantasmaKeys.Generate())).ToArray();
            foreach (var key in keys)
            {
                Assert.IsTrue(BigInteger.ModPow(RingSignature.GroupParameters.Generator, key.PrivateKey, RingSignature.GroupParameters.Prime) == key.PublicKey);
            }

            var publicKeys = keys.Select(k => k.PublicKey).ToArray();

            var signatures = new RingSignature[participants, messages.Length];
            for (int i = 0; i < participants; ++i)
            {
                for (int j = 0; j < messages.Length; ++j)
                {
                    signatures[i, j] = RingSignature.GenerateSignature(messages[j], publicKeys, keys[i].PrivateKey, i);
                    Assert.IsTrue(signatures[i, j].VerifySignature(messages[j], publicKeys));

                    for (int k = 0; k < messages.Length; ++k)
                    {
                        Assert.IsFalse(signatures[i, j].VerifySignature(messages[k], publicKeys) != (k == j));
                    }

                    var orig = signatures[i, j];
                    var tampered = new RingSignature(orig.Y0, orig.S.FlipBit(rand.Next(orig.S.GetBitLength())), orig.C);
                    Assert.IsFalse(tampered.VerifySignature(messages[j], publicKeys));

                    tampered = new RingSignature(orig.Y0.FlipBit(rand.Next(orig.Y0.GetBitLength())), orig.S, orig.C);
                    Assert.IsFalse(tampered.VerifySignature(messages[j], publicKeys));

                    var s = (BigInteger[])orig.C.Clone();
                    var t = rand.Next(s.Length);
                    s[t] = s[t].FlipBit(rand.Next(s[t].GetBitLength()));
                    tampered = new RingSignature(orig.Y0, orig.S, s);
                    Assert.IsFalse(tampered.VerifySignature(messages[j], publicKeys));
                }
            }

            for (int i = 0; i < participants; ++i)
            {
                for (int j = 0; j < messages.Length; ++j)
                {
                    for (int k = 0; k < participants; ++k)
                        for (int l = 0; l < messages.Length; ++l)
                        {
                            Assert.IsTrue(signatures[i, j].IsLinked(signatures[k, l]) == (i == k));
                        }
                }
            }
        }

        [TestMethod]
        public void SeedPhrases()
        {
            string passphrase = "hello world";
            string seedPhrase;
            var keys = SeedPhraseGenerator.Generate(passphrase, out seedPhrase);

            Assert.IsTrue(keys != null);
            Assert.IsTrue(keys.PrivateKey.Length == PhantasmaKeys.PrivateKeyLength);
            Assert.IsTrue(keys.Address.ToByteArray().Length == Address.LengthInBytes);

            var otherKeys = SeedPhraseGenerator.FromSeedPhrase(passphrase, seedPhrase);
            Assert.IsTrue(otherKeys != null);
            Assert.IsTrue(keys.Address.Text == otherKeys.Address.Text);
        }

        [TestMethod]
        public void SharedSecret()
        {
            var keyA = PhantasmaKeys.Generate();
            var keyB = PhantasmaKeys.Generate();
            var secret = "Hello Phantasma!";

            var pubA = EncryptionUtils.Curve.G * keyA.PrivateKey;
            var pubB = EncryptionUtils.Curve.G * keyB.PrivateKey;

            var sA = (pubB * keyA.PrivateKey).EncodePoint(false);
            var sB = (pubA * keyB.PrivateKey).EncodePoint(false);
            Assert.IsTrue(sA.SequenceEqual(sB));

            var encryptedMessage = EncryptionUtils.Encrypt(secret, keyA, pubB);
            var decryptedMessage = EncryptionUtils.Decrypt<string>(encryptedMessage, keyB, pubA);

            Assert.IsTrue(decryptedMessage == secret);
        }

        [TestMethod]
        public void SignatureSwap()
        {
            var rawTx = Base16.Decode("80000001AA2F638AE527480F6976CBFC268E06048040F77328F78A8F269F9DAB660715C70000029B7CFFDAA674BEAE0F930EBE6085AF9093E5FE56B34A5C220CCDCF6EFC336FC500E1F50500000000D9020CC50B04E75027E19A5D5A9E377A042A0BB59B7CFFDAA674BEAE0F930EBE6085AF9093E5FE56B34A5C220CCDCF6EFC336FC500C2EB0B000000005B1258432BE2AB39C5CD1CAAFBD2B7AAA4B0F034014140A24433C702A47174B9DC1CC6DA90611AA8895B09A5BAD82406CCEF77D594A7343F79084D42BBF8D7C818C4540B38A2E168A7B932D2C0999059A0B3A3B43F6D31232102FC1D6F42B05D00E6AEDA82DF286EB6E2578042F6CAEBE72144342466113BD81EAC");
            var tx = Neo.Core.Transaction.Unserialize(rawTx);

            var wif = "KwVG94yjfVg1YKFyRxAGtug93wdRbmLnqqrFV6Yd2CiA9KZDAp4H";
            var neoKeys = Phantasma.Neo.Core.NeoKeys.FromWIF(wif);

            Assert.IsTrue(tx.witnesses.Any());
            var wit = tx.witnesses.First();
            var witAddress = wit.ExtractAddress();

            var transposedAddress = Address.FromKey(neoKeys);

            Assert.IsTrue(transposedAddress.IsUser);
            Assert.IsTrue(transposedAddress == witAddress);

            var msg = "Hello Phantasma!";
            var payload = Encoding.UTF8.GetBytes(msg);
            var neoSig = ECDsaSignature.Generate(neoKeys, payload, ECDsaCurve.Secp256r1);

            var validateNeoSig = neoSig.Verify(payload, transposedAddress);
            Assert.IsTrue(validateNeoSig);
        }

        //https://github.com/neo-project/proposals/blob/master/nep-2.mediawiki
        [TestMethod]
        public void DecryptNEP2()
        {
            var passphrase = "Satoshi";
            var encrypted = "6PYN6mjwYfjPUuYT3Exajvx25UddFVLpCw4bMsmtLdnKwZ9t1Mi3CfKe8S";

            var keys = NeoKeys.FromNEP2(encrypted, passphrase);
            var wif = keys.WIF;

            var expectedWIF = "KwYgW8gcxj1JWJXhPSu4Fqwzfhp5Yfi42mdYmMa4XqK7NJxXUSK7";
            Assert.IsTrue(expectedWIF == wif);
        }
    }

}