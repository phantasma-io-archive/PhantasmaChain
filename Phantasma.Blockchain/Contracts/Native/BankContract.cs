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

            Runtime.Expect(Runtime.Nexus.TokenExists(Nexus.FuelTokenSymbol), "invalid native token");
            var nativeToken = Runtime.Nexus.GetTokenInfo(Nexus.FuelTokenSymbol);
            Runtime.Expect(nativeToken.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            var stableToken = Runtime.Nexus.GetTokenInfo(Nexus.StableTokenSymbol);
            Runtime.Expect(Runtime.Nexus.TokenExists(Nexus.StableTokenSymbol), "invalid stable token");

            Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, nativeToken.Symbol, target, Runtime.Chain.Address, amount), "transfer failed");

            var stableAmount = amount * GetRate(Nexus.FuelTokenSymbol);

            Runtime.Expect(Runtime.Nexus.MintTokens(Runtime, stableToken.Symbol, target, stableAmount), "mint failed");

            Runtime.Notify(EventKind.TokenSend, target, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = amount, symbol = Nexus.FuelTokenSymbol });
            Runtime.Notify(EventKind.TokenMint, target, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = stableAmount, symbol = Nexus.StableTokenSymbol });
        }

        // stable => native
        public void Redeem(Address target, BigInteger amount)
        {
            Runtime.Expect(IsWitness(target), "invalid witness");

            var nativeToken = Runtime.Nexus.GetTokenInfo(Nexus.FuelTokenSymbol);
            Runtime.Expect(Runtime.Nexus.TokenExists(Nexus.FuelTokenSymbol), "invalid native token");
            Runtime.Expect(nativeToken.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            var stableToken = Runtime.Nexus.GetTokenInfo(Nexus.StableTokenSymbol);
            Runtime.Expect(Runtime.Nexus.TokenExists(Nexus.StableTokenSymbol), "invalid stable token");

            Runtime.Expect(Runtime.Nexus.BurnTokens(Runtime, stableToken.Symbol, target, amount), "burn failed");

            var expectedAmount = amount / GetRate(Nexus.FuelTokenSymbol);
            Runtime.Expect(expectedAmount > 0, "swap amount should greater than zero");

            Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, nativeToken.Symbol, Runtime.Chain.Address, target, expectedAmount), "transfer failed");

            Runtime.Notify(EventKind.TokenReceive, target, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = expectedAmount, symbol = Nexus.FuelTokenSymbol });
            Runtime.Notify(EventKind.TokenBurn, target, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = amount, symbol = Nexus.StableTokenSymbol });
        }
    }
}
