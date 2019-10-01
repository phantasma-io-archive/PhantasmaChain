using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.Storage;
using Phantasma.Storage.Context;

namespace Phantasma.Contracts.Native
{
    public sealed class BlockContract: NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Block;

        private Address previousValidator;

        public BlockContract() : base()
        {
        }

        public Address GetCurrentValidator()
        {
            Address lastValidator;
            Timestamp validationSlotTime;

            var slotDuration = (int)Runtime.GetGovernanceValue(ValidatorContract.ValidatorRotationTimeTag);
            var chainCreationTime = Runtime.Nexus.GenesisTime;

            if (Runtime.Chain.Height > 0)
            {
                var lastBlock = Runtime.GetLastBlock();
                lastValidator = Runtime.GetValidatorForBlock(lastBlock.Hash);
                validationSlotTime = lastBlock.Timestamp;
            }
            else
            {
                lastValidator = Runtime.GetValidatorByIndex(0).address;
                validationSlotTime = chainCreationTime;
            }

            var adjustedSeconds = (uint)((validationSlotTime.Value / slotDuration) * slotDuration);
            validationSlotTime = new Timestamp(adjustedSeconds);


            var diff = Runtime.Time - validationSlotTime;
            if (diff < slotDuration)
            {
                return lastValidator;
            }

            int validatorIndex = (int)(diff / slotDuration);
            var validatorCount = Runtime.GetPrimaryValidatorCount();
            var chainIndex = Runtime.GetIndexOfChain(Runtime.Chain.Name);
            Runtime.Expect(chainIndex >= 0, "invalid chain index");

            validatorIndex += chainIndex;
            validatorIndex = validatorIndex % validatorCount;

            var currentIndex = validatorIndex;

            do
            {
                var validator = Runtime.GetValidatorByIndex(validatorIndex);
                if (validator.type == ValidatorType.Primary && !validator.address.IsNull)
                {
                    return validator.address;
                }

                validatorIndex++;
                if (validatorIndex >= validatorCount)
                {
                    validatorIndex = 0;
                }
            } while (currentIndex != validatorIndex);

            // should never reached here, failsafe
            return Runtime.Nexus.GenesisAddress;
        }

        public void OpenBlock(Address from)
        {
            Runtime.Expect(Runtime.IsWitness(from), "witness failed");

            var count = Runtime.Nexus.HasGenesis ? Runtime.GetPrimaryValidatorCount() : 0;
            if (count > 0)
            {
                Runtime.Expect(Runtime.IsKnownValidator(from), "validator failed");
                var expectedValidator = GetCurrentValidator();
                Runtime.Expect(from == expectedValidator, "current validator mismatch");
            }
            else
            {
                Runtime.Expect(Runtime.IsRootChain(), "must be root chain");
            }

            if (previousValidator != from)
            {
                Runtime.Notify(EventKind.ValidatorSwitch, from, previousValidator);
                previousValidator = from;
            }

            Runtime.Notify(EventKind.BlockCreate, from, Runtime.Chain.Address);
        }

        public void CloseBlock(Address from)
        {
            var expectedValidator = GetCurrentValidator();
            Runtime.Expect(from == expectedValidator, "current validator mismatch");
            Runtime.Expect(from.IsUser, "must be user address");
            Runtime.Expect(Runtime.IsWitness(from), "witness failed");

            var validators = Runtime.GetValidators();
            Runtime.Expect(validators.Length > 0, "no active validators found");

            var totalAvailable = Runtime.GetBalance(DomainSettings.FuelTokenSymbol, this.Address);
            var totalValidators = Runtime.GetPrimaryValidatorCount();
            var amountPerValidator = totalAvailable / totalValidators;
            Runtime.Expect(amountPerValidator > 0, "not enough fees available");

            Runtime.Notify(EventKind.BlockClose, from, Runtime.Chain.Address);

            int delivered = 0;
            for (int i = 0; i < totalValidators; i++)
            {
                var validator = validators[i];
                if (validator.type != ValidatorType.Primary)
                {
                    continue;
                }

                Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, this.Address, validator.address, amountPerValidator);
                delivered++;
            }

            Runtime.Expect(delivered > 0, "failed to claim fees");
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

            if (tokenInfo.IsFungible())
            {
                Runtime.SwapTokens(sourceChain.Name, sourceAddress, Runtime.Chain.Name, targetAddress, symbol, value, null, null);
            }
            else
            {
                var nft = Serialization.Unserialize<PackedNFTData>(data);                 
                Runtime.SwapTokens(sourceChain.Name, sourceAddress, Runtime.Chain.Name, targetAddress, symbol, value, nft.ROM, nft.RAM);
            }
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
