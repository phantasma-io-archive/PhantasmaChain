using Microsoft.VisualStudio.TestTools.UnitTesting;

using System.Linq;
using System.Text;

using Phantasma.API;
using Phantasma.Blockchain;
using Phantasma.Cryptography;

namespace Phantasma.Tests
{
    [TestClass]
    public class ApiTests
    {
        private static readonly string testWIF = "Kx9Kr8MwQ9nAJbHEYNAjw5n99B2GpU6HQFf75BGsC3hqB1ZoZm5W";
        private static readonly string testAddress = "P9dKgENhREKbRNsecvVeyPLvrMVJJqDHSWBwFZPyEJjSy";

        private NexusAPI CreateAPI()
        {
            var owner = KeyPair.FromWIF(testWIF);
            var sim = new ChainSimulator(owner, 1234);
            var api = new NexusAPI(sim.Nexus);
            return api;
        }

        [TestMethod]
        public void TestGetAccount()
        {
            var api = CreateAPI();

            var account = (AccountResult) api.GetAccount(testAddress);
            Assert.IsTrue(account.address == testAddress);
            Assert.IsTrue(account.name == "genesis");
            Assert.IsTrue(account.balances.Length > 0);
        }
    }
}
