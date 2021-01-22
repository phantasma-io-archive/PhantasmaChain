using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.Cryptography.ECC;
using Phantasma.Numerics;
using System;
using System.Text;

// Testing ECDsa signature
// Testing methods:
// bool Verify(byte[] message, IEnumerable<Address> addresses)
// ECDsaSignature Generate(IKeyPair keypair, byte[] message, ECDsaCurve curve, Func<byte[], byte[], byte[], byte[]> customSignFunction = null)
// ExtractPublicKeyFromAddress(Address address)

namespace Phantasma.Tests
{
    [TestClass]
    public class CryptoECDsaSignatureTests
    {
        [TestMethod]
        public void GenerateVerifyNeoTest()
        {
            var curve = ECDsaCurve.Secp256r1;

            var msg = "Hello Phantasma!";
            var msgBytes = Encoding.ASCII.GetBytes(msg);

            var msgIncorrect = "Hello Fhantasma!";
            var msgIncorrectBytes = Encoding.ASCII.GetBytes(msgIncorrect);

            var keys = Neo.Core.NeoKeys.Generate();
            Assert.IsTrue(keys.PrivateKey.Length == Cryptography.PhantasmaKeys.PrivateKeyLength);

            var keysIncorrect = Neo.Core.NeoKeys.Generate();
            Assert.IsTrue(keysIncorrect.PrivateKey.Length == Cryptography.PhantasmaKeys.PrivateKeyLength);

            var ecdsaSignature = ECDsaSignature.Generate(keys, msgBytes, curve);

            Console.WriteLine("ecdsaSignature.Bytes: " + Base16.Encode(ecdsaSignature.Bytes));

            var bytes = new byte[34];
            bytes[0] = (byte)Cryptography.AddressKind.User;
            Core.Utils.ByteArrayUtils.CopyBytes(keys.PublicKey, 0, bytes, 1, 33);
            var address = Cryptography.Address.FromBytes(bytes);

            var bytes2 = new byte[34];
            bytes[0] = (byte)Cryptography.AddressKind.User;
            Core.Utils.ByteArrayUtils.CopyBytes(keysIncorrect.PublicKey, 0, bytes2, 1, 33);
            var addressIncorrect = Cryptography.Address.FromBytes(bytes2);

            // Check correct message and address
            Assert.IsTrue(ecdsaSignature.Verify(msgBytes, new Cryptography.Address[] { address }));
            // Check incorrect message
            Assert.IsFalse(ecdsaSignature.Verify(msgIncorrectBytes, new Cryptography.Address[] { address }));
            // Check incorrect address
            Assert.IsFalse(ecdsaSignature.Verify(msgBytes, new Cryptography.Address[] { addressIncorrect }));
        }
        [TestMethod]
        public void GenerateVerifyEthTest()
        {
            var curve = ECDsaCurve.Secp256k1;

            var msg = "Hello Phantasma!";
            var msgBytes = Encoding.ASCII.GetBytes(msg);

            var msgIncorrect = "Hello Fhantasma!";
            var msgIncorrectBytes = Encoding.ASCII.GetBytes(msgIncorrect);

            var keys = Ethereum.EthereumKey.Generate();
            Assert.IsTrue(keys.PrivateKey.Length == Cryptography.PhantasmaKeys.PrivateKeyLength);

            var keysIncorrect = Ethereum.EthereumKey.Generate();
            Assert.IsTrue(keysIncorrect.PrivateKey.Length == Cryptography.PhantasmaKeys.PrivateKeyLength);

            var ecdsaSignature = ECDsaSignature.Generate(keys, msgBytes, curve);

            Console.WriteLine("ecdsaSignature.Bytes: " + Base16.Encode(ecdsaSignature.Bytes));

            var bytes = new byte[34];
            bytes[0] = (byte)Cryptography.AddressKind.User;
            Core.Utils.ByteArrayUtils.CopyBytes(keys.PublicKey, 0, bytes, 1, 33);
            var address = Cryptography.Address.FromBytes(bytes);

            var bytes2 = new byte[34];
            bytes[0] = (byte)Cryptography.AddressKind.User;
            Core.Utils.ByteArrayUtils.CopyBytes(keysIncorrect.PublicKey, 0, bytes2, 1, 33);
            var addressIncorrect = Cryptography.Address.FromBytes(bytes2);

            // Check correct message and address
            Assert.IsTrue(ecdsaSignature.Verify(msgBytes, new Cryptography.Address[] { address }));
            // Check incorrect message
            Assert.IsFalse(ecdsaSignature.Verify(msgIncorrectBytes, new Cryptography.Address[] { address }));
            // Check incorrect address
            Assert.IsFalse(ecdsaSignature.Verify(msgBytes, new Cryptography.Address[] { addressIncorrect }));
        }
        [TestMethod]
        public void VerifyNeoPredefinedTest()
        {
            var curve = ECDsaCurve.Secp256r1;

            var wif = "KwPpBSByydVKqStGHAnZzQofCqhDmD2bfRgc9BmZqM3ZmsdWJw4d";
            var wifIncorrect = "KzCR1GkfdVqnkXD6nvJnTVP8doXGYtBiSC5wCwf4o33QS2DwLpeJ";
            
            var signatureHex = "BE92E117E4C7829B90BF70764089B83ABBC41B3A017937E1F714FC88D475B47568D8961600964EEB6C6AEA5763A8416B946248C1A5A39D4DE29F0EE3B8A5D320";
            var signatureIncorrectHex = "BE92E117E4C7829B90BF70764089B83ABBC41B3A017937E1F714FC88D475B47568D8961600964EEB6C6AEA5763A8416B946248C1A5A39D4DE29F0EE3B8A5D321";

            var msg = "Hello Phantasma!";
            var msgBytes = Encoding.ASCII.GetBytes(msg);

            var msgIncorrect = "Hello Fhantasma!";
            var msgIncorrectBytes = Encoding.ASCII.GetBytes(msgIncorrect);

            var ecdsaSignature = new ECDsaSignature(Base16.Decode(signatureHex), curve);
            var ecdsaSignatureIncorrect = new ECDsaSignature(Base16.Decode(signatureIncorrectHex), curve);

            var keys = Neo.Core.NeoKeys.FromWIF(wif);
            Assert.IsTrue(keys.PrivateKey.Length == Cryptography.PhantasmaKeys.PrivateKeyLength);

            var keysIncorrect = Neo.Core.NeoKeys.FromWIF(wifIncorrect);
            Assert.IsTrue(keysIncorrect.PrivateKey.Length == Cryptography.PhantasmaKeys.PrivateKeyLength);

            var bytes = new byte[34];
            bytes[0] = (byte)Cryptography.AddressKind.User;
            Core.Utils.ByteArrayUtils.CopyBytes(keys.PublicKey, 0, bytes, 1, 33);
            var address = Cryptography.Address.FromBytes(bytes);

            var bytes2 = new byte[34];
            bytes[0] = (byte)Cryptography.AddressKind.User;
            Core.Utils.ByteArrayUtils.CopyBytes(keysIncorrect.PublicKey, 0, bytes2, 1, 33);
            var addressIncorrect = Cryptography.Address.FromBytes(bytes2);

            // Check correct message and address
            Assert.IsTrue(ecdsaSignature.Verify(msgBytes, new Cryptography.Address[] { address }));
            
            // Check incorrect message
            Assert.IsFalse(ecdsaSignature.Verify(msgIncorrectBytes, new Cryptography.Address[] { address }));

            // Check incorrect address
            Assert.IsFalse(ecdsaSignature.Verify(msgBytes, new Cryptography.Address[] { addressIncorrect }));

            // Check incorrect signature
            Assert.IsFalse(ecdsaSignatureIncorrect.Verify(msgBytes, new Cryptography.Address[] { address }));
        }
        [TestMethod]
        public void VerifyEthPredefinedTest()
        {
            var curve = ECDsaCurve.Secp256k1;

            var wif = "KwPpBSByydVKqStGHAnZzQofCqhDmD2bfRgc9BmZqM3ZmsdWJw4d";
            var wifIncorrect = "KzCR1GkfdVqnkXD6nvJnTVP8doXGYtBiSC5wCwf4o33QS2DwLpeJ";

            var signatureHex = "AD19BE0BD2EF66DA9D7EEC7E89A7CD6613D16F205BD4F807E6794C740BA278C5C52D02B38B1576417B7F9FCC21079E83D2ED429FA6C528097BFB0E48D19BAD9B";
            var signatureIncorrectHex = "AD19BE0BD2EF66DA9D7EEC7E89A7CD6613D16F205BD4F807E6794C740BA278C5C52D02B38B1576417B7F9FCC21079E83D2ED429FA6C528097BFB0E48D19BAD9C";

            var msg = "Hello Phantasma!";
            var msgBytes = Encoding.ASCII.GetBytes(msg);

            var msgIncorrect = "Hello Fhantasma!";
            var msgIncorrectBytes = Encoding.ASCII.GetBytes(msgIncorrect);

            var ecdsaSignature = new ECDsaSignature(Base16.Decode(signatureHex), curve);
            var ecdsaSignatureIncorrect = new ECDsaSignature(Base16.Decode(signatureIncorrectHex), curve);

            var keys = Ethereum.EthereumKey.FromWIF(wif);
            Assert.IsTrue(keys.PrivateKey.Length == Cryptography.PhantasmaKeys.PrivateKeyLength);

            var keysIncorrect = Ethereum.EthereumKey.FromWIF(wifIncorrect);
            Assert.IsTrue(keysIncorrect.PrivateKey.Length == Cryptography.PhantasmaKeys.PrivateKeyLength);

            var bytes = new byte[34];
            bytes[0] = (byte)Cryptography.AddressKind.User;
            Core.Utils.ByteArrayUtils.CopyBytes(keys.PublicKey, 0, bytes, 1, 33);
            var address = Cryptography.Address.FromBytes(bytes);

            var bytes2 = new byte[34];
            bytes[0] = (byte)Cryptography.AddressKind.User;
            Core.Utils.ByteArrayUtils.CopyBytes(keysIncorrect.PublicKey, 0, bytes2, 1, 33);
            var addressIncorrect = Cryptography.Address.FromBytes(bytes2);

            // Check correct message and address
            Assert.IsTrue(ecdsaSignature.Verify(msgBytes, new Cryptography.Address[] { address }));

            // Check incorrect message
            Assert.IsFalse(ecdsaSignature.Verify(msgIncorrectBytes, new Cryptography.Address[] { address }));

            // Check incorrect address
            Assert.IsFalse(ecdsaSignature.Verify(msgBytes, new Cryptography.Address[] { addressIncorrect }));

            // Check incorrect signature
            Assert.IsFalse(ecdsaSignatureIncorrect.Verify(msgBytes, new Cryptography.Address[] { address }));
        }
    }
}
