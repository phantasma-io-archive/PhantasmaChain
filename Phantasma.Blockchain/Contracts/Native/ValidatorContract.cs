using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Storage.Context;
using System.Linq;

namespace Phantasma.Blockchain.Contracts.Native
{
    public enum ValidatorStatus
    {
        Active,
        Waiting, // aka StandBy
        Demoted,
    }

    public struct ValidatorEntry
    {
        public Address address;
        public Timestamp election;
        public ValidatorStatus status;
    }

    public sealed class ValidatorContract : SmartContract
    {
        public const string ActiveValidatorCountTag = "validator.active.count";
        public const string StandByValidatorCountTag = "validator.standby.count";
        public const string ValidatorPollTag = "elections";

        public override string Name => "validator";

        private StorageList _validatorList; //<Address> 
        private StorageMap _validatorMap; // <Address, ValidatorInfo>

        public ValidatorContract() : base()
        {
        }

        public Address[] GetActiveValidatorAddresses()
        {
            var addresses = _validatorList.All<Address>();
            return addresses.Select(x => _validatorMap.Get<Address, ValidatorEntry>(x)).Where(x => x.status == ValidatorStatus.Active).Select(x => x.address).ToArray();
        }

        public BigInteger GetActiveValidators()
        {
            return _validatorList.Count();
        }

        public ValidatorEntry GetValidator(Address address)
        {
            Runtime.Expect(IsKnownValidator(address), "not a validator");
            return _validatorMap.Get<Address, ValidatorEntry>(address);
        }

        public bool IsActiveValidator(Address address)
        {
            if (_validatorMap.ContainsKey(address))
            {
                var validator = _validatorMap.Get<Address, ValidatorEntry>(address);
                return validator.status == ValidatorStatus.Active;
            }

            return false;
        }

        public bool IsWaitingValidator(Address address)
        {
            if (_validatorMap.ContainsKey(address))
            {
                var validator = _validatorMap.Get<Address, ValidatorEntry>(address);
                return validator.status == ValidatorStatus.Waiting;
            }

            return false;
        }

        public bool IsRejectedValidator(Address address)
        {
            if (_validatorMap.ContainsKey(address))
            {
                var validator = _validatorMap.Get<Address, ValidatorEntry>(address);
                return validator.status == ValidatorStatus.Demoted;
            }

            return false;
        }

        public bool IsKnownValidator(Address address)
        {
            if (_validatorMap.ContainsKey(address))
            {
                var validator = _validatorMap.Get<Address, ValidatorEntry>(address);
                return validator.status != ValidatorStatus.Demoted;
            }

            return false;
        }

        public void AddValidator(Address from)
        {
            Runtime.Expect(from.IsUser, "must be user address");
            Runtime.Expect(IsWitness(from), "witness failed");

            var count = _validatorList.Count();

            if (count > 0)
            {
                var max = Runtime.GetGovernanceValue(ActiveValidatorCountTag);
                Runtime.Expect(count < max, "no open validators spots");

                var pollName = ConsensusContract.SystemPoll + ValidatorPollTag;
                var hasConsensus = (bool)Runtime.CallContext("consensus", "HasRank", pollName, from, max);
                Runtime.Expect(hasConsensus, "no consensus for electing this address");
            }

            var requiredStake = EnergyContract.MasterAccountThreshold;
            var stakedAmount = (BigInteger)Runtime.CallContext("energy", "GetStake", from);

            Runtime.Expect(stakedAmount >= requiredStake, "not enough stake");

            _validatorList.Add(from);

            var entry = new ValidatorEntry()
            {
                address = from,
                election = Runtime.Time,
            };
            _validatorMap.Set(from, entry);

            Runtime.Notify(EventKind.ValidatorAdd, Runtime.Chain.Address, from);
        }

        public void RemoveValidator(Address target)
        {
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
        }

        public BigInteger GetIndexOfValidator(Address address)
        {
            if (address.IsNull)
            {
                return -1;
            }

            var index = _validatorList.IndexOf(address);
            return index;
        }

        public Address GetValidatorByIndex(BigInteger index)
        {
            Runtime.Expect(index >= 0, "invalid validator index");

            var count = _validatorList.Count();
            Runtime.Expect(index < count, "invalid validator index");

            var address = _validatorList.Get<Address>(index);
            return address;
        }

        public void Migrate(Address from, Address to)
        {
            Runtime.Expect(IsWitness(from), "witness failed");

            Runtime.Expect(to.IsUser, "destination must be user address");

            var index = GetIndexOfValidator(from);
            Runtime.Expect(index >= 0, "not a validator");

            var transferResult = (bool)Runtime.CallContext("energy", "Migrate", from, to);
            Runtime.Expect(transferResult, "stake transfer failed");

            _validatorList.Replace<Address>(index, to);

            var entry = _validatorMap.Get<Address, ValidatorEntry>(from);
            _validatorMap.Remove<Address>(from);

            entry.address = to;
            _validatorMap.Set<Address, ValidatorEntry>(to, entry);
        }
    }
}
