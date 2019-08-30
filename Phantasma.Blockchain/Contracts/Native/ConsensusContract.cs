using Phantasma.Blockchain.Tokens;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Storage.Context;

namespace Phantasma.Blockchain.Contracts.Native
{
    public struct ValidatorEntry
    {
        public Address address;
        public Timestamp joinDate;
        public Timestamp lastActivity;
        public int slashes;
    }

    public sealed class ConsensusContract : SmartContract
    {
        public override string Name => "consensus";

        private StorageList _validatorList; //<Address> 
        private StorageMap _validatorMap; // <Address, ValidatorInfo>

        public ConsensusContract() : base()
        {
        }

        public BigInteger GetMaxValidators()
        {
            return 10; // TODO this should be dynamic
        }

        public BigInteger GetRequiredStake()
        {
            return UnitConversion.ToBigInteger(50000, Nexus.StakingTokenDecimals); // TODO this should be dynamic
        }

        public Address[] GetValidators()
        {
            return _validatorList.All<Address>();
        }

        // here we reintroduce this method, as a faster way to check if an address is a validator
        private new bool IsValidator(Address address)
        {
            return _validatorMap.ContainsKey(address);
        }

        public void AddValidator(Address from)
        {
            Runtime.Expect(IsWitness(from), "witness failed");

            var count = _validatorList.Count();
            var max = GetMaxValidators();
            Runtime.Expect(count < max, "no open validators spots");

            var requiredStake = GetRequiredStake();
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
            Runtime.Expect(IsValidator(from), "validator failed");

            var entry = _validatorMap.Get<Address, ValidatorEntry>(from);

            bool brokenRules = false;

            var diff = Timestamp.Now - entry.lastActivity;
            var maxPeriod = 3600 * 2; // 2 hours
            if (diff > maxPeriod)
            {
                brokenRules = true;
            }

            var requiredStake = GetRequiredStake();
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
            if (address == Address.Null)
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

            Runtime.Expect(!to.IsInterop, "destination cannot be interop address");

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

        public void Validate(Address from)
        {
            Runtime.Expect(IsValidator(from), "validator failed");
            Runtime.Expect(Runtime.Epoch.ValidatorAddress == from, "epoch validator mismatch");

            var validator = _validatorMap.Get<Address, ValidatorEntry>(from);
            validator.lastActivity = Runtime.Time;
            _validatorMap.Set<Address, ValidatorEntry>(from, validator);

            Runtime.Notify(EventKind.ValidatorUpdate, Runtime.Chain.Address, from);
        }
    }
}
