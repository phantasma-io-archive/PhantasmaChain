using System;
using Phantasma.Blockchain.Contracts;
using Phantasma.Cryptography;
using Phantasma.Utils;
using Phantasma.VM.Contracts;

namespace Phantasma.Blockchain
{
    public partial class Chain
    {
        private Transaction GenerateNativeTokenIssueTx(KeyPair owner)
        {
            var script = ScriptUtils.TokenIssueScript("Phantasma", "SOUL", 100000000, 100000000/*, Contracts.TokenAttribute.Burnable | Contracts.TokenAttribute.Tradable*/);
            var tx = new Transaction(owner.PublicKey, script, 0, 0);
            tx.Sign(owner);
            return tx;
        }

        private Transaction GenerateDistributionDeployTx(KeyPair owner)
        {
            var script = ScriptUtils.ContractDeployScript(DistributionContract.DefaultScript, DistributionContract.DefaultABI);
            var tx = new Transaction(owner.PublicKey, script, 0, 0);
            tx.Sign(owner);
            return tx;
        }

        private Transaction GenerateGovernanceDeployTx(KeyPair owner)
        {
            var script = ScriptUtils.ContractDeployScript(GovernanceContract.DefaultScript, GovernanceContract.DefaultABI);
            var tx = new Transaction(owner.PublicKey, script, 0, 0);
            tx.Sign(owner);
            return tx;
        }

        private Transaction GenerateStakeDeployTx(KeyPair owner)
        {
            var script = ScriptUtils.ContractDeployScript(StakeContract.DefaultScript, StakeContract.DefaultABI);
            var tx = new Transaction(owner.PublicKey, script, 0, 0);
            tx.Sign(owner);
            return tx;
        }

        private Block CreateGenesisBlock(KeyPair owner)
        {
            var issueTx = GenerateNativeTokenIssueTx(owner);
            var distTx = GenerateDistributionDeployTx(owner);
            var govTx = GenerateDistributionDeployTx(owner);
            var stakeTx = GenerateStakeDeployTx(owner);
            var block = new Block(Timestamp.Now, owner.PublicKey, null /*fix me*/, new Transaction[] { issueTx, distTx, govTx, stakeTx });

            return block;
        }

    }
}
