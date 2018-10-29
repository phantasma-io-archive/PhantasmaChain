using Phantasma.Cryptography;
using Phantasma.Numerics;

namespace Phantasma.Blockchain.Contracts.Native
{
    public sealed class BankContract : NativeContract
    {
        public override ContractKind Kind => ContractKind.Bank;

        public BigInteger GetRate(string symbol)
        {
            Expect(symbol == Nexus.NativeTokenSymbol);

            return TokenUtils.ToBigInteger(0.08m, Nexus.NativeTokenDecimals);
        }

        // SOUL => stable
        public void Claim(Address target, BigInteger amount)
        {
            Expect(IsWitness(target));

            var nativeToken = Runtime.Nexus.FindTokenBySymbol(Nexus.NativeTokenSymbol);
            Expect(nativeToken != null);

            var stableToken = Runtime.Nexus.FindTokenBySymbol(Nexus.StableTokenSymbol);
            Expect(stableToken != null);

            var nativeBalances = Runtime.Chain.GetTokenBalances(nativeToken);
            Expect(nativeToken.Transfer(nativeBalances, target, Runtime.Chain.Address, amount));

            var stableAmount = amount * GetRate(Nexus.NativeTokenSymbol);

            var stableBalances = Runtime.Chain.GetTokenBalances(stableToken);
            Expect(stableToken.Mint(stableBalances, target, stableAmount));

            Runtime.Notify(EventKind.TokenSend, target, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, amount = amount, symbol = Nexus.NativeTokenSymbol });
            Runtime.Notify(EventKind.TokenMint, target, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, amount = stableAmount, symbol = Nexus.StableTokenSymbol });
        }

        // stable => SOUL
        public void Redeem(Address target, BigInteger amount)
        {
            Expect(IsWitness(target));

            var nativeToken = Runtime.Nexus.FindTokenBySymbol(Nexus.NativeTokenSymbol);
            Expect(nativeToken != null);

            var stableToken = Runtime.Nexus.FindTokenBySymbol(Nexus.StableTokenSymbol);
            Expect(stableToken != null);

            var stableBalances = Runtime.Chain.GetTokenBalances(stableToken);
            Expect(stableToken.Burn(stableBalances, target, amount));

            var expectedAmount = amount / GetRate(Nexus.NativeTokenSymbol);
            Expect(expectedAmount > 0);

            var nativeBalances = Runtime.Chain.GetTokenBalances(nativeToken);
            Expect(nativeToken.Transfer(nativeBalances, Runtime.Chain.Address, target, expectedAmount));

            Runtime.Notify(EventKind.TokenReceive, target, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, amount = expectedAmount, symbol = Nexus.NativeTokenSymbol });
            Runtime.Notify(EventKind.TokenBurn, target, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, amount = amount, symbol = Nexus.StableTokenSymbol });
        }
    }
}
