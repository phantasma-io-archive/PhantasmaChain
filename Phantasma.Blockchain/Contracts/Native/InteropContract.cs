using Phantasma.Blockchain.Tokens;
using Phantasma.Cryptography;
using Phantasma.Numerics;

namespace Phantasma.Blockchain.Contracts.Native
{
    public sealed class InteropContract : SmartContract
    {
        public override string Name => "interop";

        public InteropContract() : base()
        {
        }

        // receive from external chain
        public void DepositTokens(Hash hash, Address destination, string symbol, BigInteger amount)
        {
            Runtime.Expect(amount > 0, "amount must be positive and greater than zero");
            Runtime.Expect(destination != Address.Null, "invalid destination");
            Runtime.Expect(IsWitness(Runtime.Nexus.GenesisAddress), "invalid witness");

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Transferable), "token must be transferable");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.External), "token must be external");

            var source = Address.Null;

            var balances = this.Runtime.Chain.GetTokenBalances(token);
            var supplies = token.IsCapped ? Runtime.Chain.GetTokenSupplies(token) : null; 
            Runtime.Expect(token.Mint(this.Storage, balances, supplies, destination, amount), "mint failed");

            Runtime.Notify(EventKind.TokenReceive, destination, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = amount, symbol = symbol });
        }

        // send to external chain
        public void WithdrawTokens(Address destination, string symbol, BigInteger amount)
        {
            Runtime.Expect(amount > 0, "amount must be positive and greater than zero");
            Runtime.Expect(destination != Address.Null, "invalid destination");
            Runtime.Expect(IsWitness(Runtime.Nexus.GenesisAddress), "invalid witness");

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Transferable), "token must be transferable");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.External), "token must be external");

            var source = Address.Null;

            var balances = this.Runtime.Chain.GetTokenBalances(token);
            var supplies = token.IsCapped ? Runtime.Chain.GetTokenSupplies(token) : null;
            Runtime.Expect(token.Burn(this.Storage, balances, supplies, destination, amount), "burn failed");

            Runtime.Notify(EventKind.TokenSend, destination, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = amount, symbol = symbol });
        }
    }
}
