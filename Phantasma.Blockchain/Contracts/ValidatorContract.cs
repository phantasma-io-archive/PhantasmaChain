using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.Storage.Context;

namespace Phantasma.Blockchain.Contracts
{
    public sealed class ValidatorContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Validator;

        public const string ValidatorCountTag = "validator.count";
        public const string ValidatorRotationTimeTag = "validator.rotation.time";
        public const string ValidatorPollTag = "elections";

        private StorageMap _validators; // <BigInteger, ValidatorInfo>

        public ValidatorContract() : base()
        {
        }

        public ValidatorEntry[] GetValidators()
        {
            var totalValidators = (int)Runtime.GetGovernanceValue(ValidatorCountTag);
            var result = new ValidatorEntry[totalValidators];

            for (int i = 0; i < totalValidators; i++)
            {
                result[i] = GetValidatorByIndex(i);
            }
            return result;
        }

        public ValidatorType GetValidatorType(Address address)
        {
            var totalValidators = (int)Runtime.GetGovernanceValue(ValidatorCountTag);

            for (int i = 0; i < totalValidators; i++)
            {
                var validator = _validators.Get<BigInteger, ValidatorEntry>(i);
                if (validator.address == address)
                {
                    return validator.type;
                }
            }

            return ValidatorType.Invalid;
        }

        public BigInteger GetIndexOfValidator(Address address)
        {
            if (!address.IsUser)
            {
                return -1;
            }

            var totalValidators = (int)Runtime.GetGovernanceValue(ValidatorCountTag);

            for (int i = 0; i < totalValidators; i++)
            {
                var validator = GetValidatorByIndex(i);
                if (validator.address == address)
                {
                    return i;
                }
            }

            return -1;
        }

        public int GetMaxTotalValidators()
        {
            if (Runtime.HasGenesis)
            {
                return (int)Runtime.GetGovernanceValue(ValidatorCountTag);
            }

            return 1;
        }

        public ValidatorEntry GetValidatorByIndex(BigInteger index)
        {
            Runtime.Expect(index >= 0, "invalid validator index");

            var totalValidators = GetMaxTotalValidators();
            Runtime.Expect(index < totalValidators, "invalid validator index");

            if (_validators.ContainsKey<BigInteger>(index))
            {
                var validator = _validators.Get<BigInteger, ValidatorEntry>(index);
                return validator;
            }

            return new ValidatorEntry()
            {
                address = Address.Null,
                type = ValidatorType.Invalid,
                election = new Timestamp(0)
            };
        }

        public BigInteger GetValidatorCount(ValidatorType type)
        {
            if (type == ValidatorType.Invalid)
            {
                return 0;
            }

            var max = GetMaxPrimaryValidators();
            var count = 0;
            for (int i = 0; i < max; i++)
            {
                var validator = GetValidatorByIndex(i);
                if (validator.type == type)
                {
                    count++;
                }
            }
            return count;
        }

        public BigInteger GetMaxPrimaryValidators()
        {
            if (Runtime.HasGenesis)
            {
                var totalValidators = Runtime.GetGovernanceValue(ValidatorCountTag);
                var result = (totalValidators * 10) / 25;

                if (totalValidators > 0 && result < 1)
                {
                    result = 1;
                }

                return result;
            }

            return 1;
        }

        public BigInteger GetMaxSecondaryValidators()
        {
            if (Runtime.HasGenesis)
            {
                var totalValidators = Runtime.GetGovernanceValue(ValidatorCountTag);
                return totalValidators - GetMaxPrimaryValidators();
            }

            return 0;
        }

        // NOTE - witness not required, as anyone should be able to call this, permission is granted based on consensus
        public void SetValidator(Address target, BigInteger index, ValidatorType type)
        {
            Runtime.Expect(target.IsUser, "must be user address");
            Runtime.Expect(type == ValidatorType.Primary || type == ValidatorType.Secondary, "invalid validator type");

            var primaryValidators = GetValidatorCount(ValidatorType.Primary);
            var secondaryValidators = GetValidatorCount(ValidatorType.Secondary);

            Runtime.Expect(index >= 0, "invalid index");

            var totalValidators = GetMaxTotalValidators();
            Runtime.Expect(index < totalValidators, "invalid index");

            var expectedType = index < GetMaxPrimaryValidators() ? ValidatorType.Primary : ValidatorType.Secondary;
            Runtime.Expect(type == expectedType, "unexpected validator type");

            var requiredStake = Runtime.CallNativeContext(NativeContractKind.Stake, nameof(StakeContract.GetMasterThreshold), target).AsNumber();
            var stakedAmount = Runtime.GetStake(target);

            Runtime.Expect(stakedAmount >= requiredStake, "not enough stake");

            if (index > 0)
            {
                var isPreviousSet = _validators.ContainsKey<BigInteger>(index - 1);
                Runtime.Expect(isPreviousSet, "previous validator slot is not set");

                var previousEntry = _validators.Get<BigInteger, ValidatorEntry>(index - 1);
                Runtime.Expect(previousEntry.type != ValidatorType.Invalid, " previous validator has unexpected status");
            }

            if (primaryValidators > 0)
            {
                var isValidatorProposed = _validators.ContainsKey<BigInteger>(index);

                if (isValidatorProposed)
                {
                    var currentEntry = _validators.Get<BigInteger, ValidatorEntry>(index);
                    if (currentEntry.type != ValidatorType.Proposed)
                    {
                        Runtime.Expect(currentEntry.type == ValidatorType.Invalid, "invalid validator state");
                        isValidatorProposed = false;
                    }
                }

                if (isValidatorProposed)
                {
                    Runtime.Expect(Runtime.IsWitness(target), "invalid witness");
                }
                else
                {
                    if (primaryValidators > 1)
                    {
                        var pollName = ConsensusContract.SystemPoll + ValidatorPollTag;
                        var obtainedRank = Runtime.CallNativeContext(NativeContractKind.Consensus, "GetRank", pollName, target).AsNumber();
                        Runtime.Expect(obtainedRank >= 0, "no consensus for electing this address");
                        Runtime.Expect(obtainedRank == index, "this address was elected at a different index");
                    }
                    else
                    {
                        var firstValidator = GetValidatorByIndex(0).address;
                        Runtime.Expect(Runtime.IsWitness(firstValidator), "invalid witness");
                    }

                    type = ValidatorType.Proposed;
                }
            }
            else
            {
                Runtime.Expect(Runtime.IsWitness(Runtime.GenesisAddress), "invalid witness");
            }

            var entry = new ValidatorEntry()
            {
                address = target,
                election = Runtime.Time,
                type = type,
            };
            _validators.Set<BigInteger, ValidatorEntry>(index, entry);

            if (type == ValidatorType.Primary)
            {
                var newValidators = GetValidatorCount(ValidatorType.Primary);
                Runtime.Expect(newValidators > primaryValidators, "number of primary validators did not change");
            }
            else
            if (type == ValidatorType.Secondary)
            {
                var newValidators = GetValidatorCount(ValidatorType.Secondary);
                Runtime.Expect(newValidators > secondaryValidators, "number of secondary validators did not change");
            }

            if (type != ValidatorType.Proposed)
            {
                Runtime.AddMember(DomainSettings.ValidatorsOrganizationName, this.Address, target);
            }

            Runtime.Notify(type == ValidatorType.Proposed ? EventKind.ValidatorPropose : EventKind.ValidatorElect, Runtime.Chain.Address, target);
        }

        /*public void DemoteValidator(Address target)
        {
            Runtime.Expect(false, "not fully implemented");

            Runtime.Expect(target.IsUser, "must be user address");
            Runtime.Expect(IsKnownValidator(target), "not a validator");

            var count = _validatorList.Count();
            Runtime.Expect(count > 1, "cant remove last validator");

            var entry = _validatorMap.Get<Address, ValidatorEntry>(target);

            bool brokenRules = false;

            var diff = Timestamp.Now - Runtime.Nexus.GetValidatorLastActivity(target);
            var maxPeriod = 3600 * 2; // 2 hours
            if (diff > maxPeriod)
            {
                brokenRules = true;
            }

            var requiredStake = EnergyContract.MasterAccountThreshold;
            var stakedAmount = (BigInteger)Runtime.CallContext(Nexus.StakeContractName, "GetStake", target);

            if (stakedAmount < requiredStake)
            {
                brokenRules = true;
            }

            Runtime.Expect(brokenRules, "no rules broken");

            _validatorMap.Remove(target);
            _validatorList.Remove(target);

            Runtime.Notify(EventKind.ValidatorRemove, Runtime.Chain.Address, target);
            Runtime.RemoveMember(DomainSettings.ValidatorsOrganizationName, this.Address, from, to);
        }*/

        public void Migrate(Address from, Address to)
        {
            Runtime.Expect(Runtime.PreviousContext.Name == "account", "invalid context");

            Runtime.Expect(Runtime.IsWitness(from), "witness failed");

            Runtime.Expect(to.IsUser, "destination must be user address");

            var index = GetIndexOfValidator(from);
            Runtime.Expect(index >= 0, "validator index not found");

            var entry = _validators.Get<BigInteger, ValidatorEntry>(index);
            Runtime.Expect(entry.type == ValidatorType.Primary || entry.type == ValidatorType.Secondary, "not active validator");

            entry.address = to;
            _validators.Set<BigInteger, ValidatorEntry>(index, entry);

            Runtime.MigrateMember(DomainSettings.ValidatorsOrganizationName, this.Address, from, to);

            Runtime.Notify(EventKind.ValidatorRemove, Runtime.Chain.Address, from);
            Runtime.Notify(EventKind.ValidatorElect, Runtime.Chain.Address, to);
            Runtime.Notify(EventKind.AddressMigration, to, from);
        }
    }
}
