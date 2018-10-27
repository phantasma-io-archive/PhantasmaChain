using Phantasma.Blockchain.Tokens;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using System;

namespace Phantasma.Blockchain.Contracts.Native
{
    public struct TokenEventData
    {
        public string symbol;
        public BigInteger amount;
        public Address chainAddress;
    }

    public sealed class NexusContract : NativeContract
    {
        internal override ContractKind Kind => ContractKind.Nexus;

        public const int MAX_TOKEN_DECIMALS = 12;

        public NexusContract() : base()
        {
        }

        public Token CreateToken(Address owner, string symbol, string name, BigInteger maxSupply, BigInteger decimals)
        {
            Expect(!string.IsNullOrEmpty(symbol));
            Expect(!string.IsNullOrEmpty(name));
            Expect(maxSupply >= 0);
            Expect(decimals >= 0);
            Expect(decimals <= MAX_TOKEN_DECIMALS);

            Expect(IsWitness(owner));

            symbol = symbol.ToUpperInvariant();

            var token = this.Runtime.Nexus.CreateToken(owner, symbol, name, maxSupply);
            Expect(token != null);

            Runtime.Notify(EventKind.TokenCreate, owner, symbol);

            return token;
        }

        public Chain CreateChain(Address owner, string name, string parentName)
        {
            Expect(!string.IsNullOrEmpty(name));
            Expect(!string.IsNullOrEmpty(parentName));

            Expect(IsWitness(owner));

            name = name.ToLowerInvariant();
            Expect(!name.Equals(parentName, StringComparison.OrdinalIgnoreCase));

            var parent = this.Runtime.Nexus.FindChainByName(parentName);
            Expect(parent != null);

            var chain = this.Runtime.Nexus.CreateChain(owner, name, parent, this.Runtime.Block);
            Expect(chain != null);

            Runtime.Notify(EventKind.ChainCreate, owner, chain.Address);

            return chain;
        }

        public void MintTokens(Address target, string symbol, BigInteger amount)
        {
            Expect(amount > 0);

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Expect(token != null);

            Expect(IsWitness(token.Owner));

            var balances = this.Runtime.Chain.GetTokenBalances(token);
            Expect(token.Mint(balances, target, amount));

            Runtime.Notify(EventKind.TokenMint, target, new TokenEventData() { symbol = symbol, amount = amount, chainAddress = this.Runtime.Chain.Address });
        }

        public void BurnTokens(Address from, string symbol, BigInteger amount)
        {           
            Expect(amount > 0);
            Expect(IsWitness(from));

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Expect(token != null);

            var balances = this.Runtime.Chain.GetTokenBalances(token);
            Expect(token.Burn(balances, from, amount));

            Runtime.Notify(EventKind.TokenBurn, from, new TokenEventData() { symbol = symbol, amount = amount });
        }

        public void TransferTokens(Address target, Address destination, string symbol, BigInteger amount)
        {
            Expect(amount > 0);
            Expect(target != destination);
            Expect(IsWitness(target));

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Expect(token != null);

            var balances = this.Runtime.Chain.GetTokenBalances(token);
            Expect(token.Transfer(balances, target, destination, amount));

            Runtime.Notify(EventKind.TokenSend, target, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, amount = amount, symbol = symbol });
            Runtime.Notify(EventKind.TokenReceive, destination, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, amount = amount, symbol = symbol });
        }

        public BigInteger GetBalance(Address address, string symbol)
        {
            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Expect(token != null);

            var balances = this.Runtime.Chain.GetTokenBalances(token);
            return balances.Get(address);
        }

    }
}
