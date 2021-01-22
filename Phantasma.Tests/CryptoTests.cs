using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Linq;
using System.Text;

using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Cryptography.ECC;
using Phantasma.Ethereum.Hex.HexConvertors.Extensions;
using Phantasma.Neo.Core;
using NeoNetwork = Neo.Network;
using System.Numerics;

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
                number = new BigInteger(bytes);
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
        public void ECDsaSecP256r1()
        {
            var curveEnum = ECDsaCurve.Secp256r1;

            var key = "05329371ecfd126ad7d1f946dc18d5b03a5dd2470a6da8aab83bec5b81d47735";
            var wif = "KwPpBSByydVKqStGHAnZzQofCqhDmD2bfRgc9BmZqM3ZmsdWJw4d";
            Assert.IsTrue(key.Length == 64);

            var privateKey = key.HexToByteArray();

            var publicKey = ECDsa.GetPublicKey(privateKey, true, curveEnum);
            Assert.IsTrue(Base16.Encode(publicKey) == "023B7412A73F8F344DF626DFE85ACDCD7CBC0163C44FDD6F43C05BD6105EA27DC7");
            var uncompressedPublicKey = ECDsa.GetPublicKey(privateKey, false, curveEnum).Skip(1).ToArray();
            Assert.IsTrue(Base16.Encode(uncompressedPublicKey) == "3B7412A73F8F344DF626DFE85ACDCD7CBC0163C44FDD6F43C05BD6105EA27DC796E8B58603844196DD3FDD6D60C83E31D09FBC3B360020A82D65067994DBE6EC");

            var msgBytes = Encoding.ASCII.GetBytes("Phantasma");

            // CryptoExtensions.SignECDsa()/.VerifySignatureECDsa() tests.
            var signature = ECDsa.Sign(msgBytes, privateKey, curveEnum);
            Assert.IsNotNull(signature);

            Console.WriteLine("CryptoExtensions.SignECDsa() signature: " + Base16.Encode(signature));

            Assert.IsTrue(ECDsa.Verify(msgBytes, signature, publicKey, curveEnum));
            Assert.IsTrue(ECDsa.Verify(msgBytes, signature, uncompressedPublicKey, curveEnum));

            // Correct predefined signature.
            Assert.IsTrue(ECDsa.Verify(msgBytes, Base16.Decode("0E3A48373F966F48B8DD179C7505EE985772B4187E36C55DA24D56FBDB13CFA50F61BF203F8BBEC0CD2326DBE3514F81C7EF84CABF31E6E467EDE760B3E93ED8"), publicKey, curveEnum));
            // Incorrect predefined signature.
            Assert.IsFalse(ECDsa.Verify(msgBytes, Base16.Decode("0E3A48373F966F48B8DD179C7505EE985772B4187E36C55DA24D56FBDB13CFA50F61BF203F8BBEC0CD2326DBE3514F81C7EF84CABF31E6E467EDE760B3E93ED9"), publicKey, curveEnum));

            // ECDsaSignature.Generate()/ECDsaSignature.Verify() tests.

            var neoKeys = Neo.Core.NeoKeys.FromWIF(wif);

            // Verifying previous signature, received from CryptoExtensions.SignECDsa().
            var ecdsaSignature = new ECDsaSignature(signature, curveEnum);
            Console.WriteLine("ECDsaSignature() signature: " + Base16.Encode(ecdsaSignature.ToByteArray()));
            Assert.IsTrue(ecdsaSignature.Verify(msgBytes, Phantasma.Cryptography.Address.FromKey(neoKeys)));

            // Generating new signature with ECDsaSignature.Generate() and verifying it.
            var ecdsaSignature2 = ECDsaSignature.Generate(neoKeys, msgBytes, curveEnum);
            Console.WriteLine("ECDsaSignature() signature2: " + Base16.Encode(ecdsaSignature2.ToByteArray()));
            Assert.IsTrue(ecdsaSignature.Verify(msgBytes, Phantasma.Cryptography.Address.FromKey(neoKeys)));
        }
        [TestMethod]
        public void ECDsaSecP256k1()
        {
            var curveEnum = ECDsaCurve.Secp256k1;

            var address = "0x66571c32d77c4852be4c282eb952ba94efbeac20";
            var key = "6f6784731c4e526c97fa6a97b6f22e96f307588c5868bc2c545248bc31207eb1";
            Assert.IsTrue(key.Length == 64);

            var privateKey = key.HexToByteArray();

            var publicKey = ECDsa.GetPublicKey(privateKey, true, curveEnum);
            Assert.IsTrue(Base16.Encode(publicKey) == "0314953E3853945DFEA47C562CA363D7A9856DD3394AB5C1A03A1A71F3AE155E57");
            var uncompressedPublicKey = ECDsa.GetPublicKey(privateKey, false, curveEnum).Skip(1).ToArray();
            Assert.IsTrue(Base16.Encode(uncompressedPublicKey) == "14953E3853945DFEA47C562CA363D7A9856DD3394AB5C1A03A1A71F3AE155E57D8BE683B82770F7070B8BB5A6F26A066373E5A772C204221BC45C94138801957");

            var kak = new Phantasma.Ethereum.Util.Sha3Keccack().CalculateHash(uncompressedPublicKey);
            var Address = "0x" + Base16.Encode(kak.Skip(12).ToArray()).ToLower();
            Console.WriteLine("Address: " + Address);
            Assert.IsTrue(Address == address);

            var msgBytes = Encoding.ASCII.GetBytes("Phantasma");

            // CryptoExtensions.SignECDsa()/.VerifySignatureECDsa() tests.
            var signature = ECDsa.Sign(msgBytes, privateKey, curveEnum);
            Assert.IsNotNull(signature);

            Console.WriteLine("CryptoExtensions.SignECDsa() signature: " + Base16.Encode(signature));

            Assert.IsTrue(ECDsa.Verify(msgBytes, signature, publicKey, curveEnum));
            Assert.IsTrue(ECDsa.Verify(msgBytes, signature, uncompressedPublicKey, curveEnum));

            // Correct predefined signature.
            Assert.IsTrue(ECDsa.Verify(msgBytes, Base16.Decode("DC959D270B3268D1DF5D46CFE509C7162068DFCE68EBEAAFF26E85DA4C2CFF7588F2D4C0915FF8420F88A3EC8C633E8B1F8788CCE8B044208029233742884862"), publicKey, curveEnum));
            // Incorrect predefined signature.
            Assert.IsFalse(ECDsa.Verify(msgBytes, Base16.Decode("DC959D270B3268D1DF5D46CFE509C7162068DFCE68EBEAAFF26E85DA4C2CFF7588F2D4C0915FF8420F88A3EC8C633E8B1F8788CCE8B044208029233742884863"), publicKey, curveEnum));

            // ECDsaSignature.Generate()/ECDsaSignature.Verify() tests.

            var ethKeys = Ethereum.EthereumKey.FromPrivateKey(key);

            // Verifying previous signature, received from CryptoExtensions.SignECDsa().
            var ecdsaSignature = new ECDsaSignature(signature, curveEnum);
            Console.WriteLine("ECDsaSignature() signature: " + Base16.Encode(ecdsaSignature.ToByteArray()));
            Assert.IsTrue(ecdsaSignature.Verify(msgBytes, Phantasma.Cryptography.Address.FromKey(ethKeys)));

            // Generating new signature with ECDsaSignature.Generate() and verifying it.
            var ecdsaSignature2 = ECDsaSignature.Generate(ethKeys, msgBytes, curveEnum);
            Console.WriteLine("ECDsaSignature() signature2: " + Base16.Encode(ecdsaSignature2.ToByteArray()));
            Assert.IsTrue(ecdsaSignature.Verify(msgBytes, Phantasma.Cryptography.Address.FromKey(ethKeys)));
        }

        [TestMethod]
        public void SignatureSwap()
        {
            var rawTx = Base16.Decode("80000001AA2F638AE527480F6976CBFC268E06048040F77328F78A8F269F9DAB660715C70000029B7CFFDAA674BEAE0F930EBE6085AF9093E5FE56B34A5C220CCDCF6EFC336FC500E1F50500000000D9020CC50B04E75027E19A5D5A9E377A042A0BB59B7CFFDAA674BEAE0F930EBE6085AF9093E5FE56B34A5C220CCDCF6EFC336FC500C2EB0B000000005B1258432BE2AB39C5CD1CAAFBD2B7AAA4B0F034014140A24433C702A47174B9DC1CC6DA90611AA8895B09A5BAD82406CCEF77D594A7343F79084D42BBF8D7C818C4540B38A2E168A7B932D2C0999059A0B3A3B43F6D31232102FC1D6F42B05D00E6AEDA82DF286EB6E2578042F6CAEBE72144342466113BD81EAC");
            var tx = NeoNetwork.P2P.Payloads.Transaction.DeserializeFrom(rawTx);

            var wif = "KwVG94yjfVg1YKFyRxAGtug93wdRbmLnqqrFV6Yd2CiA9KZDAp4H";
            var neoKeys = Phantasma.Neo.Core.NeoKeys.FromWIF(wif);

            Assert.IsTrue(tx.Witnesses.Any());
            var wit = tx.Witnesses.First();
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
    }

}
