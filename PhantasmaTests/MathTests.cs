using NUnit.Framework;
using System.Text;
using Phantasma.Mathematics;
using Phantasma.Utils;
using Phantasma.VM.Types;
using System;
using System.Linq;

namespace PhantasmaTests
{
    [TestFixture]
    public class MathTests
    {
        public void Base16Tests()
        {
            var bytes = new byte[32];
            var rnd = new Random();
            rnd.NextBytes(bytes);

            var hex = Base16.Encode(bytes);

            Assert.IsTrue(hex.Length == bytes.Length * 2);

            var output = Base16.Decode(hex);
            Assert.IsTrue(output.Length == bytes.Length);
            Assert.IsTrue(output.SequenceEqual(bytes));
        }

        public void Base58Tests()
        {
            var bytes = new byte[32];
            var rnd = new Random();
            rnd.NextBytes(bytes);

            var hex = Base58.Encode(bytes);

            Assert.IsTrue(hex.Length == bytes.Length * 2);

            var output = Base58.Decode(hex);
            Assert.IsTrue(output.Length == bytes.Length);
            Assert.IsTrue(output.SequenceEqual(bytes));
        }
    }
}
