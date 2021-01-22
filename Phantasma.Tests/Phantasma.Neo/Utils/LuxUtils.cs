using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.Neo.Utils;

// Testing methods:
// bool IsValidAddress(this string address)

namespace Phantasma.Tests
{
    [TestClass]
    public class PhantasmaNeoUtilsTests
    {
        [TestMethod]
        public void IsValidAddressTest()
        {
            // Checking valid address
            Assert.IsTrue("AP6ZkjweW4NGskMca2KH2cchNJbFWW2vZe".IsValidAddress());

            // Checking invalid address
            Assert.IsFalse("AP6ZkjweW4NGskMca2KH2cchNJbFWW2vZee".IsValidAddress());

            // Checking invalid address
            Assert.IsFalse("AP6ZkjweW4NGskMca2KH2cchNJbFWW2vZ".IsValidAddress());

            // Checking invalid address
            Assert.IsFalse("AP6ZkjweW4NGskMca2KH2cchNJbFWW2vZE".IsValidAddress());

            // Checking invalid address
            Assert.IsFalse("AP6ZkjweW4NGskMca2KH2cchNJbFWW2lOI".IsValidAddress());
        }
    }
}