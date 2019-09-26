using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Domain;

namespace Phantasma.Blockchain.Contracts.Native
{
    public sealed class BlockContract: SmartContract
    {
        public override string Name => Nexus.BlockContractName;

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

            if (Runtime.Chain.BlockHeight > 0)
            {
                var lastBlock = Runtime.Chain.LastBlock;
                lastValidator = Runtime.Chain.GetValidatorForBlock(lastBlock);
                validationSlotTime = lastBlock.Timestamp;
            }
            else
            {
                lastValidator = Runtime.Nexus.GetValidatorByIndex(0).address;
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
            var validatorCount = Runtime.Nexus.GetPrimaryValidatorCount();
            var chainIndex = Runtime.Nexus.GetIndexOfChain(Runtime.Chain.Name);
            Runtime.Expect(chainIndex >= 0, "invalid chain index");

            validatorIndex += chainIndex;
            validatorIndex = validatorIndex % validatorCount;

            var currentIndex = validatorIndex;

            do
            {
                var validator = Runtime.Nexus.GetValidatorByIndex(validatorIndex);
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

            var count = Runtime.Nexus.HasGenesis ? Runtime.Nexus.GetPrimaryValidatorCount() : 0;
            if (count > 0)
            {
                Runtime.Expect(Runtime.Nexus.IsKnownValidator(from), "validator failed");
                var expectedValidator = GetCurrentValidator();
                Runtime.Expect(from == expectedValidator, "current validator mismatch");
            }
            else
            {
                Runtime.Expect(Runtime.Chain.IsRoot, "must be root chain");
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

            var validators = Runtime.Nexus.GetValidators();
            Runtime.Expect(validators.Length > 0, "no active validators found");

            var totalAvailable = Runtime.GetBalance(DomainSettings.FuelTokenSymbol, this.Address);
            var totalValidators = Runtime.Nexus.GetPrimaryValidatorCount();
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

                if (Runtime.Nexus.TransferTokens(Runtime, DomainSettings.FuelTokenSymbol, this.Address, validator.address, amountPerValidator))
                {
                    Runtime.Notify(EventKind.TokenReceive, validator.address, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = amountPerValidator, symbol = DomainSettings.FuelTokenSymbol });
                    delivered++;
                }
            }

            Runtime.Expect(delivered > 0, "failed to claim fees");
        }
    }
}
