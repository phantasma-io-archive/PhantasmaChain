using NUnit.Framework;
using System.Text;
using Phantasma.Cryptography;
using Phantasma.Utils;
using Phantasma.VM.Types;

namespace PhantasmaTests
{
    [TestFixture]
    public class CryptoTests
    {
        [Test]
        public void KeyPairSign()
        {
            var keys = KeyPair.Generate();
            Assert.IsTrue(keys.PrivateKey.Length == KeyPair.PrivateKeyLength);
            Assert.IsTrue(keys.Address.PublicKey.Length == Address.PublicKeyLength);

            var msg = "Hello world";

            var msgBytes = Encoding.ASCII.GetBytes(msg);
            var signature = keys.Sign(msgBytes);

        }
    }
}
