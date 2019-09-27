using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Domain;

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
    }
}
