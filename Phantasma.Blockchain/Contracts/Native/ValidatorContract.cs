using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Storage.Context;

namespace Phantasma.Blockchain.Contracts.Native
{
    public enum ValidatorType
    {
        Invalid,
        Primary,
        Secondary, // aka StandBy
    }

    public struct ValidatorEntry
    {
        public Address address;
        public Timestamp election;
        public ValidatorType type;
    }

    public sealed class ValidatorContract : SmartContract
    {
        public const string ValidatorCountTag = "validator.count";
        public const string ValidatorRotationTimeTag = "validator.rotation.time";
        public const string ValidatorPollTag = "elections";

        public override string Name => Nexus.ValidatorContractName;

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
            if (Runtime.Nexus.Ready)
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
            if (Runtime.Nexus.Ready)
            {
                var totalValidators = Runtime.GetGovernanceValue(ValidatorCountTag);
                return (totalValidators * 10) / 25;
            }

            return 1;
        }

        public BigInteger GetMaxSecondaryValidators()
        {
            if (Runtime.Nexus.Ready)
            {
                var totalValidators = Runtime.GetGovernanceValue(ValidatorCountTag);
                return totalValidators - GetMaxPrimaryValidators();
            }

            return 0;
        }

        // NOTE - witness not required, as anyone should be able to call this, permission is granted based on consensus
        public void SetValidator(Address from, BigInteger index, ValidatorType type)
        {
            Runtime.Expect(from.IsUser, "must be user address");
            Runtime.Expect(type != ValidatorType.Invalid, "invalid validator type");
            
            var activeValidators = GetValidatorCount(ValidatorType.Primary);

            Runtime.Expect(index >= 0, "invalid index");

            var totalValidators = GetMaxTotalValidators();
            Runtime.Expect(index < totalValidators, "invalid index");

            if (activeValidators > 1)
            {
                var pollName = ConsensusContract.SystemPoll + ValidatorPollTag;
                var obtainedRank = Runtime.CallContext("consensus", "GetRank", pollName, from).AsNumber();
                Runtime.Expect(obtainedRank >= 0, "no consensus for electing this address");
                Runtime.Expect(obtainedRank == index, "this address was elected at a different index");

                var expectedType = index < GetMaxPrimaryValidators() ? ValidatorType.Primary : ValidatorType.Secondary;
                Runtime.Expect(type == expectedType, "unexpected validator type");
            }
            else
            {
                Runtime.Expect(type == ValidatorType.Primary, "type must be primary");
            }

            var requiredStake = StakeContract.MasterAccountThreshold;
            var stakedAmount = Runtime.CallContext(Nexus.StakeContractName, "GetStake", from).AsNumber();

            Runtime.Expect(stakedAmount >= requiredStake, "not enough stake");

            if (index > 0)
            {
                var isPreviousSet = _validators.ContainsKey<BigInteger>(index - 1);
                Runtime.Expect(isPreviousSet, "previous validator slot is not set");
            }

            var entry = new ValidatorEntry()
            {
                address = from,
                election = Runtime.Time,
                type = type,
            };
            _validators.Set<BigInteger, ValidatorEntry>(index, entry);

            Runtime.Notify(EventKind.ValidatorAdd, Runtime.Chain.Address, from);
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
        }*/

            /*
        public void Migrate(Address from, Address to)
        {
            Runtime.Expect(IsWitness(from), "witness failed");

            Runtime.Expect(to.IsUser, "destination must be user address");

            var index = GetIndexOfValidator(from);
            Runtime.Expect(index >= 0, "not a validator");

            var transferResult = (bool)Runtime.CallContext(Nexus.StakeContractName, "Migrate", from, to);
            Runtime.Expect(transferResult, "stake transfer failed");

            var entry = _validatorMap.Get<Address, ValidatorEntry>(from);
            _validatorMap.Remove<Address>(from);

            entry.address = to;
            _validatorMap.Set<Address, ValidatorEntry>(to, entry);
        }*/
    }
}
