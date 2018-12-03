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
    }

    public sealed class StakeContract : SmartContract
    {
        public override string Name => "stake";

        private const string ENTRY_MAP = "_mmap";
        private const string ENTRY_LIST = "_mlst";

        public StakeContract() : base()
        {
        }

        public int GetMaxValidators()
        {
            return 3; // TODO this should be dynamic
        }

        public BigInteger GetRequiredStake()
        {
            return TokenUtils.ToBigInteger(50000, Nexus.NativeTokenDecimals); // TODO this should be dynamic
        }

        public void Stake(Address address, BigInteger amount)
        {
            Runtime.Expect(IsWitness(address), "witness failed");

            var list = Storage.FindCollectionForContract<Address>(ENTRY_LIST);
            var count = list.Count();
            var max = GetMaxValidators();
            Runtime.Expect(count < max, "no open validators spots");

            var stakeAmount = GetRequiredStake();

            var balances = Runtime.Chain.GetTokenBalances(Runtime.Nexus.NativeToken);
            var balance = balances.Get(address);
            Runtime.Expect(balance >= stakeAmount, "not enough balance");

            Runtime.Expect(balances.Subtract(address, stakeAmount), "balance subtract failed");
            Runtime.Expect(balances.Add(Runtime.Chain.Address, stakeAmount), "balance add failed");

            list.Add(address);

            var map = Storage.FindMapForContract<Address, ValidatorInfo>(ENTRY_MAP);

            var entry = new ValidatorInfo()
            {
                address = address,
                stake = stakeAmount,
                timestamp = Timestamp.Now,
            };
            map.Set(address, entry);
        }

        public void Unstake(Address address)
        {
            Runtime.Expect(IsWitness(address), "witness failed");

            var map = Storage.FindMapForContract<Address, ValidatorInfo>(ENTRY_MAP);
            Runtime.Expect(map.ContainsKey(address), "not a validator address");

            var entry = map.Get(address);

            var diff = Timestamp.Now - entry.timestamp;
            var days = diff / 86400; // convert seconds to days

            Runtime.Expect(days >= 30, "waiting period required");

            var stakeAmount = entry.stake;
            var balances = Runtime.Chain.GetTokenBalances(Runtime.Nexus.NativeToken);
            var balance = balances.Get(Runtime.Chain.Address);
            Runtime.Expect(balance >= stakeAmount, "not enough balance");

            Runtime.Expect(balances.Subtract(Runtime.Chain.Address, stakeAmount), "balance subtract failed");
            Runtime.Expect(balances.Add(address, stakeAmount), "balance add failed");

            map.Remove(address);

            var list = Storage.FindCollectionForContract<Address>(ENTRY_LIST);
            list.Remove(address);
        }

        public BigInteger GetStake(Address address)
        {
            throw new System.NotImplementedException();
        }

    }
}
