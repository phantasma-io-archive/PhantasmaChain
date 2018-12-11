using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Linq;
using System.Text;

using Phantasma.Cryptography;
using Phantasma.Core.Utils;
using Phantasma.Numerics;
using Phantasma.Cryptography.Ring;

namespace Phantasma.Tests
{
    [TestClass]
    public class CryptoTests
    {
        [TestMethod]
        public void HashTests()
        {
            var bytes = new byte[32];
            var rnd = new Random();
            rnd.NextBytes(bytes);

            var hash = new Hash(bytes);

            Assert.IsTrue(hash.ToByteArray().Length == 32);
            Assert.IsTrue(hash.ToByteArray().SequenceEqual(bytes));

            bytes = new byte[10];
            LargeInteger number;

            do
            {
                rnd.NextBytes(bytes);
                number = new LargeInteger(bytes);
            } while (number <= 0);

            hash = number;
            Assert.IsTrue(hash.ToByteArray().Length == 32);

            LargeInteger other = hash;
            Assert.IsTrue(number == other);

            Assert.IsTrue(Hash.TryParse("FFFFAAAAFFFFBBBBFFFFAAAAFFFFBBBBFFFFAAAAFFFFBBBBFFFFAAAAFFFFBBBB", out hash));
            Assert.IsTrue(Hash.TryParse("0xFFFFAAAAFFFFBBBBFFFFAAAAFFFFBBBBFFFFAAAAFFFFBBBBFFFFAAAAFFFFBBBB", out hash));
            Assert.IsFalse(Hash.TryParse("FFFFAAAXFFFFBBBBFFFFAAAAFFFFBBBBFFFFAAAAFFFFBBBBFFFFAAAAFFFFBBBB", out hash));
            Assert.IsFalse(Hash.TryParse("0xFFFFAAAAFFFFBBBBFFFFAAAAFFFFBBBBFFFFAAAAFFFFBBBBFFFFAAAAFFFFBBB", out hash));
        }

        [TestMethod]
        public void EdDSA()
        {
            var keys = KeyPair.Generate();
            Assert.IsTrue(keys.PrivateKey.Length == KeyPair.PrivateKeyLength);
            Assert.IsTrue(keys.Address.PublicKey.Length == Address.PublicKeyLength);

            var msg = "Hello phantasma";

            var msgBytes = Encoding.ASCII.GetBytes(msg);
            var signature = keys.Sign(msgBytes);

            var verified = signature.Verify(msgBytes, keys.Address);
            Assert.IsTrue(verified);

            // make sure that Verify fails for other addresses
            var otherKeys = KeyPair.Generate();
            Assert.IsFalse(otherKeys.Address == keys.Address);
            verified = signature.Verify(msgBytes, otherKeys.Address);
            Assert.IsFalse(verified);
        }

        [TestMethod]
        public void RingSignatures()
        {
            var rand = new Random();

            int participants = 5;
            var messages = new[] { "hello", "phantasma chain", "welcome to the future" }.Select(Encoding.UTF8.GetBytes).ToArray();
            var keys = Enumerable.Range(0, participants).Select(i => RingSignature.GenerateKeyPair(KeyPair.Generate())).ToArray();
            foreach (var key in keys)
            {
                Assert.IsTrue(LargeInteger.ModPow(RingSignature.GroupParameters.Generator, key.PrivateKey, RingSignature.GroupParameters.Prime) == key.PublicKey);
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

                    var s = (LargeInteger[])orig.C.Clone();
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
            Assert.IsTrue(keys.PrivateKey.Length == KeyPair.PrivateKeyLength);
            Assert.IsTrue(keys.Address.PublicKey.Length == Address.PublicKeyLength);

            var otherKeys = SeedPhraseGenerator.FromSeedPhrase(passphrase, seedPhrase);
            Assert.IsTrue(otherKeys != null);
            Assert.IsTrue(keys.Address.Text == otherKeys.Address.Text);
        }

    }
}
