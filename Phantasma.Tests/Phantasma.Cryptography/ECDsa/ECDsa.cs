using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.Cryptography.ECC;
using Phantasma.Numerics;
using System;
using System.Text;

// Testing covers following methods:
// byte[] GenerateSignature(byte[] message)
// bool VerifySignature(byte[] message, byte[] sig, ECCurve curve, ECPoint publicKey)

namespace Phantasma.Tests
{
    [TestClass]
    public class CryptoECDsaTests
    {
        [TestMethod]
        public void GenerateVerifyNeoTest()
        {
            var curve = ECCurve.Secp256r1;

            var wif = "KwPpBSByydVKqStGHAnZzQofCqhDmD2bfRgc9BmZqM3ZmsdWJw4d";
            var wifIncorrect = "KzCR1GkfdVqnkXD6nvJnTVP8doXGYtBiSC5wCwf4o33QS2DwLpeJ";

            var msg = "Hello Phantasma!";
            var msgBytes = Encoding.ASCII.GetBytes(msg);

            var msgIncorrect = "Hello Fhantasma!";
            var msgIncorrectBytes = Encoding.ASCII.GetBytes(msgIncorrect);

            var keys = Neo.Core.NeoKeys.FromWIF(wif);
            Assert.IsTrue(keys.PrivateKey.Length == Cryptography.PhantasmaKeys.PrivateKeyLength);

            var keysIncorrect = Neo.Core.NeoKeys.FromWIF(wifIncorrect);
            Assert.IsTrue(keysIncorrect.PrivateKey.Length == Cryptography.PhantasmaKeys.PrivateKeyLength);

            var publicKey = curve.G * keys.PrivateKey;
            var publicKeyIncorrect = curve.G * keysIncorrect.PrivateKey;

            var signer = new ECDsa(keys.PrivateKey, curve);
            var signature = signer.GenerateSignature(msgBytes);
            Assert.IsNotNull(signature);

            Console.WriteLine("signature: " + Base16.Encode(signature));

            Assert.IsTrue(ECDsa.VerifySignature(msgBytes, signature, curve, publicKey));
            Assert.IsFalse(ECDsa.VerifySignature(msgIncorrectBytes, signature, curve, publicKey));
            Assert.IsFalse(ECDsa.VerifySignature(msgBytes, signature, curve, publicKeyIncorrect));
            Assert.IsFalse(ECDsa.VerifySignature(msgIncorrectBytes, signature, curve, publicKeyIncorrect));
        }
        [TestMethod]
        public void GenerateVerifyEthTest()
        {
            var curve = ECCurve.Secp256k1;

            var wif = "KwPpBSByydVKqStGHAnZzQofCqhDmD2bfRgc9BmZqM3ZmsdWJw4d";
            var wifIncorrect = "KzCR1GkfdVqnkXD6nvJnTVP8doXGYtBiSC5wCwf4o33QS2DwLpeJ";

            var msg = "Hello Phantasma!";
            var msgBytes = Encoding.ASCII.GetBytes(msg);

            var msgIncorrect = "Hello Fhantasma!";
            var msgIncorrectBytes = Encoding.ASCII.GetBytes(msgIncorrect);

            var keys = Neo.Core.NeoKeys.FromWIF(wif);
            Assert.IsTrue(keys.PrivateKey.Length == Cryptography.PhantasmaKeys.PrivateKeyLength);

            var keysIncorrect = Neo.Core.NeoKeys.FromWIF(wifIncorrect);
            Assert.IsTrue(keysIncorrect.PrivateKey.Length == Cryptography.PhantasmaKeys.PrivateKeyLength);

            var publicKey = curve.G * keys.PrivateKey;
            var publicKeyIncorrect = curve.G * keysIncorrect.PrivateKey;

            var signer = new ECDsa(keys.PrivateKey, curve);
            var signature = signer.GenerateSignature(msgBytes);
            Assert.IsNotNull(signature);

            Console.WriteLine("signature: " + Base16.Encode(signature));

            Assert.IsTrue(ECDsa.VerifySignature(msgBytes, signature, curve, publicKey));
            Assert.IsFalse(ECDsa.VerifySignature(msgIncorrectBytes, signature, curve, publicKey));
            Assert.IsFalse(ECDsa.VerifySignature(msgBytes, signature, curve, publicKeyIncorrect));
            Assert.IsFalse(ECDsa.VerifySignature(msgIncorrectBytes, signature, curve, publicKeyIncorrect));
        }
        [TestMethod]
        public void VerifySignatureNeoPredefinedTest()
        {
            var curve = ECCurve.Secp256r1;

            var wif = "KwPpBSByydVKqStGHAnZzQofCqhDmD2bfRgc9BmZqM3ZmsdWJw4d";
            var wifIncorrect = "KzCR1GkfdVqnkXD6nvJnTVP8doXGYtBiSC5wCwf4o33QS2DwLpeJ";

            var signatureHex = "30460221005599DAAA49071C2DB775C9F02C4D2C02BB47F99C85F219159366CC1A774D0C2F022100617921232BD2B23A42748915B90A02750A800CFEB3E91CFAFC1B8477195F669B01";
            var signatureIncorrectHex = "30460221005599DAAA49071C2DB775C9F02C4D2C02BB47F99C85F219159366CC1A774D0C2F022100617921232BD2B23A42748915B90A02750A800CFEB3E91CFAFC1B8477195F669B02";

            var msg = "Hello Phantasma!";
            var msgBytes = Encoding.ASCII.GetBytes(msg);

            var msgIncorrect = "Hello Fhantasma!";
            var msgIncorrectBytes = Encoding.ASCII.GetBytes(msgIncorrect);

            var signatureBytes = Base16.Decode(signatureHex);
            var signatureIncorrectBytes = Base16.Decode(signatureIncorrectHex);

            var keys = Neo.Core.NeoKeys.FromWIF(wif);
            Assert.IsTrue(keys.PrivateKey.Length == Cryptography.PhantasmaKeys.PrivateKeyLength);

            var keysIncorrect = Neo.Core.NeoKeys.FromWIF(wifIncorrect);
            Assert.IsTrue(keysIncorrect.PrivateKey.Length == Cryptography.PhantasmaKeys.PrivateKeyLength);

            var ecPointPublicKey = curve.G * keys.PrivateKey;
            var ecPointPublicKeyIncorrect = curve.G * keysIncorrect.PrivateKey;

            // Check correct signature, message and public key
            Assert.IsTrue(ECDsa.VerifySignature(msgBytes, signatureBytes, curve, ecPointPublicKey));

            // Check incorrect signature
            // TODO MUST BE PASSED
            // Assert.IsFalse(ECDsa.VerifySignature(msgBytes, signatureIncorrectBytes, curve, ecPointPublicKey));

            // Check incorrect message
            Assert.IsFalse(ECDsa.VerifySignature(msgIncorrectBytes, signatureBytes, curve, ecPointPublicKey));

            // Check incorrect public key
            Assert.IsFalse(ECDsa.VerifySignature(msgIncorrectBytes, signatureBytes, curve, ecPointPublicKeyIncorrect));

            // Check incorrect signature and message
            Assert.IsFalse(ECDsa.VerifySignature(msgIncorrectBytes, signatureIncorrectBytes, curve, ecPointPublicKey));

            // Check incorrect signature and public key
            Assert.IsFalse(ECDsa.VerifySignature(msgBytes, signatureIncorrectBytes, curve, ecPointPublicKeyIncorrect));

            // Check incorrect message and public key
            Assert.IsFalse(ECDsa.VerifySignature(msgIncorrectBytes, signatureBytes, curve, ecPointPublicKeyIncorrect));

            // Check incorrect everything
            Assert.IsFalse(ECDsa.VerifySignature(msgIncorrectBytes, signatureIncorrectBytes, curve, ecPointPublicKeyIncorrect));
        }
        [TestMethod]
        public void VerifySignatureEthPredefinedTest()
        {
            var curve = ECCurve.Secp256k1;

            var wif = "KwPpBSByydVKqStGHAnZzQofCqhDmD2bfRgc9BmZqM3ZmsdWJw4d";
            var wifIncorrect = "KzCR1GkfdVqnkXD6nvJnTVP8doXGYtBiSC5wCwf4o33QS2DwLpeJ";

            var signatureHex = "30460221000DF635BBC35FFE1DA2C0720FD8C0ED1BDBCC94A145647629A8FF71B2D097CC0E0221006DD388FAD179D534C54EF8BAE40A0BAF48C0FFAD54A43387922379B8D5B3EB0101";
            var signatureIncorrectHex = "30460221000DF635BBC35FFE1DA2C0720FD8C0ED1BDBCC94A145647629A8FF71B2D097CC0E0221006DD388FAD179D534C54EF8BAE40A0BAF48C0FFAD54A43387922379B8D5B3EB0102";

            var msg = "Hello Phantasma!";
            var msgBytes = Encoding.ASCII.GetBytes(msg);

            var msgIncorrect = "Hello Fhantasma!";
            var msgIncorrectBytes = Encoding.ASCII.GetBytes(msgIncorrect);

            var signatureBytes = Base16.Decode(signatureHex);
            var signatureIncorrectBytes = Base16.Decode(signatureIncorrectHex);

            var keys = Ethereum.EthereumKey.FromWIF(wif);
            Assert.IsTrue(keys.PrivateKey.Length == Cryptography.PhantasmaKeys.PrivateKeyLength);

            var keysIncorrect = Ethereum.EthereumKey.FromWIF(wifIncorrect);
            Assert.IsTrue(keysIncorrect.PrivateKey.Length == Cryptography.PhantasmaKeys.PrivateKeyLength);

            var ecPointPublicKey = curve.G * keys.PrivateKey;
            var ecPointPublicKeyIncorrect = curve.G * keysIncorrect.PrivateKey;

            // Check correct signature, message and public key
            Assert.IsTrue(ECDsa.VerifySignature(msgBytes, signatureBytes, curve, ecPointPublicKey));

            // Check incorrect signature
            // TODO MUST BE PASSED
            // Assert.IsFalse(ECDsa.VerifySignature(msgBytes, signatureIncorrectBytes, curve, ecPointPublicKey));

            // Check incorrect message
            Assert.IsFalse(ECDsa.VerifySignature(msgIncorrectBytes, signatureBytes, curve, ecPointPublicKey));

            // Check incorrect public key
            Assert.IsFalse(ECDsa.VerifySignature(msgIncorrectBytes, signatureBytes, curve, ecPointPublicKeyIncorrect));

            // Check incorrect signature and message
            Assert.IsFalse(ECDsa.VerifySignature(msgIncorrectBytes, signatureIncorrectBytes, curve, ecPointPublicKey));

            // Check incorrect signature and public key
            Assert.IsFalse(ECDsa.VerifySignature(msgBytes, signatureIncorrectBytes, curve, ecPointPublicKeyIncorrect));

            // Check incorrect message and public key
            Assert.IsFalse(ECDsa.VerifySignature(msgIncorrectBytes, signatureBytes, curve, ecPointPublicKeyIncorrect));

            // Check incorrect everything
            Assert.IsFalse(ECDsa.VerifySignature(msgIncorrectBytes, signatureIncorrectBytes, curve, ecPointPublicKeyIncorrect));
        }
    }
}