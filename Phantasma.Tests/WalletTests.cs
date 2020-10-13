using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.Blockchain;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.VM.Utils;
using System.Linq;

namespace Phantasma.Tests
{
    [TestClass]
    public class WalletTests
    {
        [TestMethod]
        public void TransferScriptMethodExtraction()
        {
            var source = PhantasmaKeys.Generate();
            var dest = PhantasmaKeys.Generate();
            var amount = UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals);
            var script = ScriptUtils.BeginScript().AllowGas(source.Address, Address.Null, 1, 999).TransferTokens(DomainSettings.StakingTokenSymbol, source.Address, dest.Address, amount).SpendGas(source.Address).EndScript();

            var table = DisasmUtils.GetDefaultDisasmTable();
            var methods = DisasmUtils.ExtractMethodCalls(script, table);

            Assert.IsTrue(methods != null && methods.Count() == 3);
        }
    }

}
