using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Storage.Context;

namespace Phantasma.Blockchain.Contracts.Native
{
    public enum ValidatorStatus
    {
        Invalid,
        Active,
        Waiting, // aka StandBy
    }

    public struct ValidatorEntry
    {
        public Address address;
        public Timestamp election;
        public ValidatorStatus status;
    }

    public sealed class ValidatorContract : SmartContract
    {
        public const string ValidatorCountTag = "validator.count";
        public const string ValidatorRotationTimeTag = "validator.rotation.time";
        public const string ValidatorPollTag = "elections";

        public override string Name => "validator";

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

        public ValidatorStatus GetValidatorStatus(Address address)
        {
            var totalValidators = (int)Runtime.GetGovernanceValue(ValidatorCountTag);

            for (int i = 0; i < totalValidators; i++)
            {
                var validator = _validators.Get<BigInteger, ValidatorEntry>(i);
                if (validator.address == address)
                {
                    return validator.status;
                }
            }

            return ValidatorStatus.Invalid;
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

        public ValidatorEntry GetValidatorByIndex(BigInteger index)
        {
            Runtime.Expect(index >= 0, "invalid validator index");

            var totalValidators = Runtime.GetGovernanceValue(ValidatorCountTag);
            Runtime.Expect(index < totalValidators, "invalid validator index");

            if (_validators.ContainsKey<BigInteger>(index))
            {
                var validator = _validators.Get<BigInteger, ValidatorEntry>(index);
                return validator;
            }

            return new ValidatorEntry()
            {
                address = Address.Null,
                status = ValidatorStatus.Invalid,
                election = new Timestamp(0)
            };
        }

        public BigInteger GetActiveValidatorCount()
        {
            var totalValidators = Runtime.GetGovernanceValue(ValidatorCountTag);
            return (totalValidators * 10) / 25;
        }

        public bool IsActiveValidator(Address address)
        {
            return GetValidatorStatus(address) == ValidatorStatus.Active;
        }

        public bool IsWaitingValidator(Address address)
        {
            return GetValidatorStatus(address) == ValidatorStatus.Waiting;
        }

        public bool IsKnownValidator(Address address)
        {
            return GetValidatorStatus(address) != ValidatorStatus.Invalid;
        }

        // NOTE - witness not required, as anyone should be able to call this, permission is granted based on consensus
        public void SetValidator(Address from, BigInteger index)
        {
            Runtime.Expect(from.IsUser, "must be user address");

            ValidatorStatus status;

            if (Runtime.Nexus.Ready)
            {
                Runtime.Expect(index >= 0, "invalid index");

                var totalValidators = (int)Runtime.GetGovernanceValue(ValidatorCountTag);
                Runtime.Expect(index < totalValidators, "invalid index");

                var pollName = ConsensusContract.SystemPoll + ValidatorPollTag;
                var obtainedRank = (BigInteger)Runtime.CallContext("consensus", "GetRank", pollName, from);
                Runtime.Expect(obtainedRank >= 0, "no consensus for electing this address");
                Runtime.Expect(obtainedRank == index, "this address was elected at a different index");

                status = index < GetActiveValidatorCount() ? ValidatorStatus.Active : ValidatorStatus.Waiting;
            }
            else
            {
                Runtime.Expect(index == 0, "invalid index");
                status = ValidatorStatus.Active;
            }

            var requiredStake = EnergyContract.MasterAccountThreshold;
            var stakedAmount = (BigInteger)Runtime.CallContext("energy", "GetStake", from);

            Runtime.Expect(stakedAmount >= requiredStake, "not enough stake");

            var entry = new ValidatorEntry()
            {
                address = from,
                election = Runtime.Time,
                status = status,
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
            var stakedAmount = (BigInteger)Runtime.CallContext("energy", "GetStake", target);

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

            var transferResult = (bool)Runtime.CallContext("energy", "Migrate", from, to);
            Runtime.Expect(transferResult, "stake transfer failed");

            var entry = _validatorMap.Get<Address, ValidatorEntry>(from);
            _validatorMap.Remove<Address>(from);

            entry.address = to;
            _validatorMap.Set<Address, ValidatorEntry>(to, entry);
        }*/
    }
}
