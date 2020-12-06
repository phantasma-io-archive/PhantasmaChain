using System.Numerics;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Storage.Context;

namespace Phantasma.Blockchain.Contracts
{
    public sealed class BlockContract: NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Block;

        public BlockContract() : base()
        {
        }

        #region SETTLEMENTS
        internal StorageMap _settledTransactions; //<Hash, Hash>
        internal StorageMap _swapMap; // <Address, List<Hash>>

        public bool IsSettled(Hash hash)
        {
            return _settledTransactions.ContainsKey(hash);
        }

        private void RegisterHashAsKnown(Hash sourceHash, Hash targetHash)
        {
            _settledTransactions.Set(sourceHash, targetHash);
        }

        private void DoSettlement(IChain sourceChain, Address sourceAddress, Address targetAddress, string symbol, BigInteger value, byte[] data)
        {
            Runtime.Expect(value > 0, "value must be greater than zero");
            Runtime.Expect(targetAddress.IsUser, "target must not user address");

            Runtime.Expect(this.Runtime.TokenExists(symbol), "invalid token");
            var tokenInfo = this.Runtime.GetToken(symbol);

            /*if (tokenInfo.IsCapped())
            {
                var supplies = new SupplySheet(symbol, this.Runtime.Chain, Runtime.Nexus);
                
                if (IsAddressOfParentChain(sourceChain.Address))
                {
                    Runtime.Expect(supplies.MoveFromParent(this.Storage, value), "target supply check failed");
                }
                else // child chain
                {
                    Runtime.Expect(supplies.MoveFromChild(this.Storage, sourceChain.Name, value), "target supply check failed");
                }
            }
            */

            Runtime.SwapTokens(sourceChain.Name, sourceAddress, Runtime.Chain.Name, targetAddress, symbol, value);
        }

        public void SettleTransaction(Address sourceChainAddress, Hash hash)
        {
            Runtime.Expect(Runtime.IsAddressOfParentChain(sourceChainAddress) || Runtime.IsAddressOfChildChain(sourceChainAddress), "source must be parent or child chain");

            Runtime.Expect(!IsSettled(hash), "hash already settled");

            var sourceChain = this.Runtime.GetChainByAddress(sourceChainAddress);

            var tx = Runtime.ReadTransactionFromOracle(DomainSettings.PlatformName, sourceChain.Name, hash);

            int settlements = 0;

            foreach (var transfer in tx.Transfers)
            {
                if (transfer.destinationChain == this.Runtime.Chain.Name)
                {
                    DoSettlement(sourceChain, transfer.sourceAddress, transfer.destinationAddress, transfer.Symbol, transfer.Value, transfer.Data);
                    settlements++;
                }
            }

            Runtime.Expect(settlements > 0, "no settlements in the transaction");
            RegisterHashAsKnown(hash, Runtime.Transaction.Hash);
        }
        #endregion
    }
}
