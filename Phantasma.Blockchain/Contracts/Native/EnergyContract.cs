using Phantasma.Blockchain.Storage;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;

namespace Phantasma.Blockchain.Contracts.Native
{
    public struct EnergyStakeInfo
    {
        public BigInteger stake;
        public Timestamp timestamp;
    }

    public sealed class EnergyContract : SmartContract
    {
        public override string Name => "energy";

        private StorageMap _entryMap; // <Address, EnergyStakeInfo>

        public readonly static BigInteger EnergyRacioDivisor = 500; // used as 1/500, will generate 0.002 per staked token
 
        public EnergyContract() : base()
        {
        }
 
        public void Stake(Address address, BigInteger stakeAmount)
        {
            Runtime.Expect(stakeAmount >= EnergyRacioDivisor, "invalid amount");
            Runtime.Expect(IsWitness(address), "witness failed");

            var stakeToken = Runtime.Nexus.StakingToken;
            var stakeBalances = Runtime.Chain.GetTokenBalances(stakeToken);
            var balance = stakeBalances.Get(address);
            Runtime.Expect(balance >= stakeAmount, "not enough balance");

            Runtime.Expect(stakeBalances.Subtract(address, stakeAmount), "balance subtract failed");
            Runtime.Expect(stakeBalances.Add(Runtime.Chain.Address, stakeAmount), "balance add failed");

            var fuelToken = Runtime.Nexus.FuelToken;
            var fuelBalances = Runtime.Chain.GetTokenBalances(stakeToken);
            var fuelAmount = stakeAmount / EnergyRacioDivisor;
            Runtime.Expect(fuelToken.Mint(fuelBalances, address, fuelAmount), "fuel minting failed");

            var entry = new EnergyStakeInfo()
            {
                stake = stakeAmount,
                timestamp = Timestamp.Now,
            };
            _entryMap.Set(address, entry);

            Runtime.Notify(EventKind.TokenStake, address, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = stakeToken.Symbol, value = stakeAmount });
            Runtime.Notify(EventKind.TokenMint, address, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = fuelToken.Symbol, value = fuelAmount});
        }

        public BigInteger Unstake(Address address)
        {
            Runtime.Expect(IsWitness(address), "witness failed");

            var entry = _entryMap.Get<Address, ValidatorInfo>(address);

            var diff = Timestamp.Now - entry.timestamp;
            var days = diff / 86400; // convert seconds to days

            Runtime.Expect(days >= 1, "waiting period required");

            var amount = entry.stake;
            var token = Runtime.Nexus.StakingToken;
            var balances = Runtime.Chain.GetTokenBalances(token);
            var balance = balances.Get(Runtime.Chain.Address);
            Runtime.Expect(balance >= amount, "not enough balance");

            Runtime.Expect(balances.Subtract(Runtime.Chain.Address, amount), "balance subtract failed");
            Runtime.Expect(balances.Add(address, amount), "balance add failed");

            _entryMap.Remove(address);

            Runtime.Notify(EventKind.TokenUnstake, address, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = token.Symbol, value = amount });

            return amount;
        }

        public BigInteger GetStake(Address address)
        {
            Runtime.Expect(_entryMap.ContainsKey(address), "not a validator address");
            var entry = _entryMap.Get<Address, ValidatorInfo>(address);
            return entry.stake;
        }
    }
}
