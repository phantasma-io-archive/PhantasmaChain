using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Storage.Context;

namespace Phantasma.Blockchain.Contracts.Native
{
    public enum ValidatorStatus
    {
        Active,
        Waiting, // aka StandBy
        Rejected,
    }

    public struct ValidatorEntry
    {
        public Address address;
        public Timestamp joinDate;
        public Timestamp lastActivity;
        public ValidatorStatus status;
        public int slashes;
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

        public Address[] GetValidators()
        {
            return _validatorList.All<Address>();
        }

        public BigInteger GetValidatorCount()
        {
            return _validatorList.Count();
        }

        public ValidatorEntry GetValidator(Address address)
        {
            Runtime.Expect(IsKnownValidator(address), "not a validator");
            return _validatorMap.Get<Address, ValidatorEntry>(address);
        }

        private bool IsActiveValidator(Address address)
        {
            if (_validatorMap.ContainsKey(address))
            {
                var validator = _validatorMap.Get<Address, ValidatorEntry>(address);
                return validator.status == ValidatorStatus.Active;
            }

            return false;
        }

        private bool IsWaitingValidator(Address address)
        {
            if (_validatorMap.ContainsKey(address))
            {
                var validator = _validatorMap.Get<Address, ValidatorEntry>(address);
                return validator.status == ValidatorStatus.Waiting;
            }

            return false;
        }

        private bool IsRejectedValidator(Address address)
        {
            if (_validatorMap.ContainsKey(address))
            {
                var validator = _validatorMap.Get<Address, ValidatorEntry>(address);
                return validator.status == ValidatorStatus.Rejected;
            }

            return false;
        }

        private bool IsKnownValidator(Address address)
        {
            if (_validatorMap.ContainsKey(address))
            {
                var validator = _validatorMap.Get<Address, ValidatorEntry>(address);
                return validator.status != ValidatorStatus.Rejected;
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
                joinDate = Runtime.Time,
                lastActivity = Runtime.Time,
                slashes = 0
            };
            _validatorMap.Set(from, entry);

            Runtime.Notify(EventKind.ValidatorAdd, Runtime.Chain.Address, from);
        }

        public void RemoveValidator(Address from)
        {
            Runtime.Expect(from.IsUser, "must be user address");
            Runtime.Expect(IsKnownValidator(from), "not a validator");

            var count = _validatorList.Count();
            Runtime.Expect(count > 1, "cant remove last validator");

            var entry = _validatorMap.Get<Address, ValidatorEntry>(from);

            bool brokenRules = false;

            var diff = Timestamp.Now - entry.lastActivity;
            var maxPeriod = 3600 * 2; // 2 hours
            if (diff > maxPeriod)
            {
                brokenRules = true;
            }

            var requiredStake = EnergyContract.MasterAccountThreshold;
            var stakedAmount = (BigInteger)Runtime.CallContext("energy", "GetStake", from);

            if (stakedAmount < requiredStake)
            {
                brokenRules = true;
            }

            Runtime.Expect(brokenRules, "no rules broken");

            _validatorMap.Remove(from);
            _validatorList.Remove(from);

            Runtime.Notify(EventKind.ValidatorRemove, Runtime.Chain.Address, from);
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
            entry.lastActivity = Runtime.Time;
            _validatorMap.Set<Address, ValidatorEntry>(to, entry);
        }

        public void CreateBlock(Address from)
        {
            Runtime.Expect(IsWitness(from), "witness failed");

            var count = _validatorList.Count();
            if (count > 0)
            {
                Runtime.Expect(Runtime.Nexus.IsKnownValidator(from), "validator failed");
                Runtime.Expect(Runtime.Chain.IsCurrentValidator(from), "current validator mismatch");

                var validator = _validatorMap.Get<Address, ValidatorEntry>(from);
                validator.lastActivity = Runtime.Time;
                _validatorMap.Set<Address, ValidatorEntry>(from, validator);
            }

            Runtime.Notify(EventKind.BlockCreate, from, Runtime.Chain.Address);
        }

        public void CloseBlock(Address from)
        {
            Runtime.Expect(IsActiveValidator(from), "validator failed");
            Runtime.Expect(Runtime.Chain.IsCurrentValidator(from), "current validator mismatch");
            Runtime.Expect(IsWitness(from), "witness failed");

            var count = _validatorList.Count();

            var totalValidators = 0;
            for (int i = 0; i < count; i++)
            {
                var address = _validatorList.Get<Address>(i);
                var validator = _validatorMap.Get<Address, ValidatorEntry>(address);
                if (validator.status == ValidatorStatus.Active)
                {
                    totalValidators++;
                }
            }

            var totalAvailable = Runtime.Chain.GetTokenBalance(Nexus.FuelTokenSymbol, this.Address);
            var amountPerValidator = totalAvailable / count;

            int delivered = 0;
            for (int i = 0; i < count; i++)
            {
                var address = _validatorList.Get<Address>(i);
                var validator = _validatorMap.Get<Address, ValidatorEntry>(address);
                if (validator.status == ValidatorStatus.Active)
                {
                    if (Runtime.Nexus.TransferTokens(Runtime, Nexus.FuelTokenSymbol, this.Address, validator.address, amountPerValidator))
                    {
                        Runtime.Notify(EventKind.TokenReceive, validator.address, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = amountPerValidator, symbol = Nexus.FuelTokenSymbol });
                        delivered = 0;
                    }
                }
            }

            Runtime.Expect(delivered > 0, "failed to claim fees");
        }
    }
}
