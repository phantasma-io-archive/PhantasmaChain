using Phantasma.Blockchain.Tokens;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using System;
using System.Linq;

namespace Phantasma.Blockchain.Contracts.Native
{
    public struct TokenEventData
    {
        public string symbol;
        public BigInteger value;
        public Address chainAddress;
    }

    public sealed class NexusContract : NativeContract
    {
        public override ContractKind Kind => ContractKind.Nexus;

        public const int MAX_TOKEN_DECIMALS = 12;

        public NexusContract() : base()
        {
        }

        public Token CreateToken(Address owner, string symbol, string name, BigInteger maxSupply, BigInteger decimals, TokenFlags flags)
        {
            Expect(!string.IsNullOrEmpty(symbol));
            Expect(!string.IsNullOrEmpty(name));
            Expect(maxSupply >= 0);
            Expect(decimals >= 0);
            Expect(decimals <= MAX_TOKEN_DECIMALS);

            Expect(IsWitness(owner));

            symbol = symbol.ToUpperInvariant();

            var token = this.Runtime.Nexus.CreateToken(Runtime.Chain, owner, symbol, name, maxSupply, (int)decimals, flags);
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

        #region FUNGIBLE TOKENS
        public void MintTokens(Address target, string symbol, BigInteger amount)
        {
            Expect(amount > 0);

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Expect(token != null);
            Expect(token.Flags.HasFlag(TokenFlags.Fungible));

            Expect(IsWitness(token.Owner));

            var balances = this.Runtime.Chain.GetTokenBalances(token);
            Expect(token.Mint(balances, target, amount));

            Runtime.Notify(EventKind.TokenMint, target, new TokenEventData() { symbol = symbol, value = amount, chainAddress = this.Runtime.Chain.Address });
        }

        public void BurnTokens(Address from, string symbol, BigInteger amount)
        {           
            Expect(amount > 0);
            Expect(IsWitness(from));

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Expect(token != null);
            Expect(token.Flags.HasFlag(TokenFlags.Fungible));

            var balances = this.Runtime.Chain.GetTokenBalances(token);
            Expect(token.Burn(balances, from, amount));

            Runtime.Notify(EventKind.TokenBurn, from, new TokenEventData() { symbol = symbol, value = amount });
        }

        public void TransferTokens(Address source, Address destination, string symbol, BigInteger amount)
        {
            Expect(amount > 0);
            Expect(source != destination);
            Expect(IsWitness(source));

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Expect(token != null);
            Expect(token.Flags.HasFlag(TokenFlags.Fungible));
            Expect(token.Flags.HasFlag(TokenFlags.Transferable));

            var balances = this.Runtime.Chain.GetTokenBalances(token);
            Expect(token.Transfer(balances, source, destination, amount));

            Runtime.Notify(EventKind.TokenSend, source, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = amount, symbol = symbol });
            Runtime.Notify(EventKind.TokenReceive, destination, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = amount, symbol = symbol });
        }

        public BigInteger GetBalance(Address address, string symbol)
        {
            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Expect(token != null);
            Expect(token.Flags.HasFlag(TokenFlags.Fungible));

            var balances = this.Runtime.Chain.GetTokenBalances(token);
            return balances.Get(address);
        }
        #endregion

        #region NON FUNGIBLE TOKENS
        public BigInteger[] GetTokens(Address address, string symbol)
        {
            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Expect(token != null);
            Expect(!token.Flags.HasFlag(TokenFlags.Fungible));

            var ownerships = this.Runtime.Chain.GetTokenOwnerships(token);
            return ownerships.Get(address).ToArray();
        }

        public BigInteger MintToken(Address from, string symbol, byte[] data)
        {
            Expect(IsWitness(from));

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Expect(token != null);
            Expect(!token.Flags.HasFlag(TokenFlags.Fungible));

            var tokenID = this.Runtime.Chain.CreateNFT(token, data);
            Expect(tokenID > 0);

            var ownerships = this.Runtime.Chain.GetTokenOwnerships(token);
            Expect(ownerships.Give(from, tokenID));

            Runtime.Notify(EventKind.TokenMint, from, new TokenEventData() { symbol = symbol, value = tokenID });
            return tokenID;
        }

        public void BurnToken(Address from, string symbol, BigInteger tokenID)
        {
            Expect(IsWitness(from));

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Expect(token != null);
            Expect(!token.Flags.HasFlag(TokenFlags.Fungible));

            var ownerships = this.Runtime.Chain.GetTokenOwnerships(token);
            Expect(ownerships.Take(from, tokenID));

            Expect(this.Runtime.Chain.DestroyNFT(token, tokenID));

            Runtime.Notify(EventKind.TokenBurn, from, new TokenEventData() { symbol = symbol, value = tokenID});
        }

        public void TransferToken(Address source, Address destination, string symbol, BigInteger tokenID)
        {
            Expect(source != destination);
            Expect(IsWitness(source));

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Expect(token != null);
            Expect(!token.Flags.HasFlag(TokenFlags.Fungible));

            var ownerships = this.Runtime.Chain.GetTokenOwnerships(token);
            Expect(ownerships.Take(source, tokenID));
            Expect(ownerships.Give(destination, tokenID));

            Runtime.Notify(EventKind.TokenSend, source, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = tokenID, symbol = symbol });
            Runtime.Notify(EventKind.TokenReceive, destination, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = tokenID, symbol = symbol });
        }

        #endregion
    }
}
