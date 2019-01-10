using Phantasma.Blockchain.Storage;
using Phantasma.Blockchain.Tokens;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;

namespace Phantasma.Blockchain.Contracts.Native
{
    public struct ValidatorInfo
    {
        public Address address;
        public BigInteger stake;
        public Timestamp timestamp;
        public int slashes;
    }

    public sealed class StakeContract : SmartContract
    {
        public override string Name => "stake";

        private StorageList _entryList; //<Address> 
        private StorageMap _entryMap; // <Address, ValidatorInfo>

        public StakeContract() : base()
        {
        }

        public BigInteger GetMaxValidators()
        {
            return 3; // TODO this should be dynamic
        }

        public BigInteger GetActiveValidators()
        {
            return _entryList.Count();
        }

        public BigInteger GetRequiredStake()
        {
            return TokenUtils.ToBigInteger(50000, Nexus.NativeTokenDecimals); // TODO this should be dynamic
        }

        public Address[] GetValidators()
        {
            return _entryList.All<Address>();
        }

        // here we reintroduce this method, as a faster way to check if an address is a validator
        private new bool IsValidator(Address address)
        {
            return _entryMap.ContainsKey(address);
        }

        public void Stake(Address address)
        {
            Runtime.Expect(IsWitness(address), "witness failed");

            var count = _entryList.Count();
            var max = GetMaxValidators();
            Runtime.Expect(count < max, "no open validators spots");

            var stakeAmount = GetRequiredStake();

            var balances = Runtime.Chain.GetTokenBalances(Runtime.Nexus.NativeToken);
            var balance = balances.Get(address);
            Runtime.Expect(balance >= stakeAmount, "not enough balance");

            Runtime.Expect(balances.Subtract(address, stakeAmount), "balance subtract failed");
            Runtime.Expect(balances.Add(Runtime.Chain.Address, stakeAmount), "balance add failed");

            _entryList.Add(address);

            var entry = new ValidatorInfo()
            {
                address = address,
                stake = stakeAmount,
                timestamp = Timestamp.Now,
                slashes = 0
            };
            _entryMap.Set(address, entry);
        }

        public void Unstake(Address address)
        {
            Runtime.Expect(IsValidator(address), "validator failed");
            Runtime.Expect(IsWitness(address), "witness failed");

            var entry = _entryMap.Get<Address, ValidatorInfo>(address);

            var diff = Timestamp.Now - entry.timestamp;
            var days = diff / 86400; // convert seconds to days

            Runtime.Expect(days >= 30, "waiting period required");

            var stakeAmount = entry.stake;
            var balances = Runtime.Chain.GetTokenBalances(Runtime.Nexus.NativeToken);
            var balance = balances.Get(Runtime.Chain.Address);
            Runtime.Expect(balance >= stakeAmount, "not enough balance");

            Runtime.Expect(balances.Subtract(Runtime.Chain.Address, stakeAmount), "balance subtract failed");
            Runtime.Expect(balances.Add(address, stakeAmount), "balance add failed");

            _entryMap.Remove(address);

            _entryList.Remove(address);
        }

        public BigInteger GetStake(Address address)
        {
            Runtime.Expect(_entryMap.ContainsKey(address), "not a validator address");
            var entry = _entryMap.Get<Address, ValidatorInfo>(address);
            return entry.stake;
        }

        public BigInteger GetIndexOfValidator(Address address)
        {
            if (address == Address.Null)
            {
                return -1;
            }

            var index = _entryList.IndexOf(address);
            return index;
        }

        public Address GetValidatorByIndex(BigInteger index)
        {
            Runtime.Expect(index >= 0, "invalid validator index");

            var count = _entryList.Count();
            Runtime.Expect(index < count, "invalid validator index");

            var address = _entryList.Get<Address>(index);
            return address;
        }
    }
}
