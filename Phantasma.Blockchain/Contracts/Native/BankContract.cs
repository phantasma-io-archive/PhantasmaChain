using Phantasma.Blockchain.Tokens;
using Phantasma.Cryptography;
using Phantasma.Numerics;

namespace Phantasma.Blockchain.Contracts.Native
{
    public sealed class BankContract : SmartContract
    {
        public override string Name => "bank";

        public BigInteger GetRate(string symbol)
        {
            //Runtime.Expect(symbol == Nexus.NativeTokenSymbol, "invalid token");

            if (symbol == Nexus.FuelTokenSymbol)
            {
                return UnitConversion.ToBigInteger(0.08m, Nexus.FuelTokenDecimals);
            }

            return 0;
        }

        // SOUL => native
        public void Claim(Address target, BigInteger amount)
        {
            Runtime.Expect(IsWitness(target), "invalid witness");

            var nativeToken = Runtime.Nexus.FindTokenBySymbol(Nexus.FuelTokenSymbol);
            Runtime.Expect(nativeToken != null, "invalid native token");
            Runtime.Expect(nativeToken.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            var stableToken = Runtime.Nexus.FindTokenBySymbol(Nexus.StableTokenSymbol);
            Runtime.Expect(stableToken != null, "invalid stable token");

            var nativeBalances = Runtime.Chain.GetTokenBalances(nativeToken);
            Runtime.Expect(nativeToken.Transfer(this.Storage, nativeBalances, target, Runtime.Chain.Address, amount), "transfer failed");

            var stableAmount = amount * GetRate(Nexus.FuelTokenSymbol);

            var stableBalances = Runtime.Chain.GetTokenBalances(stableToken);
            var supplies = Runtime.Chain.GetTokenSupplies(stableToken);
            Runtime.Expect(stableToken.Mint(this.Storage, stableBalances, supplies, target, stableAmount), "mint failed");

            Runtime.Notify(EventKind.TokenSend, target, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = amount, symbol = Nexus.FuelTokenSymbol });
            Runtime.Notify(EventKind.TokenMint, target, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = stableAmount, symbol = Nexus.StableTokenSymbol });
        }

        // stable => native
        public void Redeem(Address target, BigInteger amount)
        {
            Runtime.Expect(IsWitness(target), "invalid witness");

            var nativeToken = Runtime.Nexus.FindTokenBySymbol(Nexus.FuelTokenSymbol);
            Runtime.Expect(nativeToken != null, "invalid native token");
            Runtime.Expect(nativeToken.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            var stableToken = Runtime.Nexus.FindTokenBySymbol(Nexus.StableTokenSymbol);
            Runtime.Expect(stableToken != null, "invalid stable token");

            var stableBalances = Runtime.Chain.GetTokenBalances(stableToken);
            var supplies = Runtime.Chain.GetTokenSupplies(stableToken);
            Runtime.Expect(stableToken.Burn(this.Storage, stableBalances, supplies, target, amount), "burn failed");

            var expectedAmount = amount / GetRate(Nexus.FuelTokenSymbol);
            Runtime.Expect(expectedAmount > 0, "swap amount should greater than zero");

            var nativeBalances = Runtime.Chain.GetTokenBalances(nativeToken);
            Runtime.Expect(nativeToken.Transfer(this.Storage, nativeBalances, Runtime.Chain.Address, target, expectedAmount), "transfer failed");

            Runtime.Notify(EventKind.TokenReceive, target, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = expectedAmount, symbol = Nexus.FuelTokenSymbol });
            Runtime.Notify(EventKind.TokenBurn, target, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = amount, symbol = Nexus.StableTokenSymbol });
        }
    }
}
