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
            Runtime.Expect(!string.IsNullOrEmpty(symbol), "symbol required");
            Runtime.Expect(!string.IsNullOrEmpty(name), "name required");
            Runtime.Expect(maxSupply >= 0, "supply cant be negative");
            Runtime.Expect(decimals >= 0, "decimals cant be negative");
            Runtime.Expect(decimals <= MAX_TOKEN_DECIMALS, $"decimals cant exceed {MAX_TOKEN_DECIMALS}");

            Runtime.Expect(IsWitness(owner), "invalid witness");

            symbol = symbol.ToUpperInvariant();

            var token = this.Runtime.Nexus.CreateToken(Runtime.Chain, owner, symbol, name, maxSupply, (int)decimals, flags);
            Runtime.Expect(token != null, "invalid token");

            Runtime.Notify(EventKind.TokenCreate, owner, symbol);

            return token;
        }

        public Chain CreateChain(Address owner, string name, string parentName)
        {
            Runtime.Expect(!string.IsNullOrEmpty(name), "name required");
            Runtime.Expect(!string.IsNullOrEmpty(parentName), "parent chain required");

            Runtime.Expect(IsWitness(owner), "invalid witness");

            name = name.ToLowerInvariant();
            Runtime.Expect(!name.Equals(parentName, StringComparison.OrdinalIgnoreCase), "same name as parent");

            var parent = this.Runtime.Nexus.FindChainByName(parentName);
            Runtime.Expect(parent != null, "invalid parent");

            var chain = this.Runtime.Nexus.CreateChain(owner, name, parent, this.Runtime.Block);
            Runtime.Expect(chain != null, "chain creation failed");

            Runtime.Notify(EventKind.ChainCreate, owner, chain.Address);

            return chain;
        }

        #region FUNGIBLE TOKENS
        public void MintTokens(Address target, string symbol, BigInteger amount)
        {
            Runtime.Expect(amount > 0, "amount must be positive and greater than zero");

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            Runtime.Expect(IsWitness(token.Owner), "invalid witness");

            var balances = this.Runtime.Chain.GetTokenBalances(token);
            Runtime.Expect(token.Mint(balances, target, amount), "minting failed");

            Runtime.Notify(EventKind.TokenMint, target, new TokenEventData() { symbol = symbol, value = amount, chainAddress = this.Runtime.Chain.Address });
        }

        public void BurnTokens(Address from, string symbol, BigInteger amount)
        {           
            Runtime.Expect(amount > 0, "amount must be positive and greater than zero");
            Runtime.Expect(IsWitness(from), "invalid witness");

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            var balances = this.Runtime.Chain.GetTokenBalances(token);
            Runtime.Expect(token.Burn(balances, from, amount), "burning failed");

            Runtime.Notify(EventKind.TokenBurn, from, new TokenEventData() { symbol = symbol, value = amount });
        }

        public void TransferTokens(Address source, Address destination, string symbol, BigInteger amount)
        {
            Runtime.Expect(amount > 0, "amount must be positive and greater than zero");
            Runtime.Expect(source != destination, "source and destination must be different");
            Runtime.Expect(IsWitness(source), "invalid witness");

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Transferable), "token must be transferable");

            var balances = this.Runtime.Chain.GetTokenBalances(token);
            Runtime.Expect(token.Transfer(balances, source, destination, amount), "transfer failed");

            Runtime.Notify(EventKind.TokenSend, source, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = amount, symbol = symbol });
            Runtime.Notify(EventKind.TokenReceive, destination, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = amount, symbol = symbol });
        }

        public BigInteger GetBalance(Address address, string symbol)
        {
            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            var balances = this.Runtime.Chain.GetTokenBalances(token);
            return balances.Get(address);
        }
        #endregion

        #region NON FUNGIBLE TOKENS
        public BigInteger[] GetTokens(Address address, string symbol)
        {
            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(!token.IsFungible, "token must be non-fungible");

            var ownerships = this.Runtime.Chain.GetTokenOwnerships(token);
            return ownerships.Get(address).ToArray();
        }

        public BigInteger MintToken(Address from, string symbol, byte[] data)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(!token.IsFungible, "token must be non-fungible");

            var tokenID = this.Runtime.Chain.CreateNFT(token, data);
            Runtime.Expect(tokenID > 0, "invalid tokenID");

            var ownerships = this.Runtime.Chain.GetTokenOwnerships(token);
            Runtime.Expect(ownerships.Give(from, tokenID), "give token failed");

            Runtime.Notify(EventKind.TokenMint, from, new TokenEventData() { symbol = symbol, value = tokenID });
            return tokenID;
        }

        public void BurnToken(Address from, string symbol, BigInteger tokenID)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(!token.IsFungible, "token must be non-fungible");

            var ownerships = this.Runtime.Chain.GetTokenOwnerships(token);
            Runtime.Expect(ownerships.Take(from, tokenID), "take token failed");

            Runtime.Expect(this.Runtime.Chain.DestroyNFT(token, tokenID), "destroy token failed");

            Runtime.Notify(EventKind.TokenBurn, from, new TokenEventData() { symbol = symbol, value = tokenID});
        }

        public void TransferToken(Address source, Address destination, string symbol, BigInteger tokenID)
        {
            Runtime.Expect(IsWitness(source), "invalid witness");

            Runtime.Expect(source != destination, "source and destination must be different");

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(!token.IsFungible, "token must be non-fungible");

            var ownerships = this.Runtime.Chain.GetTokenOwnerships(token);
            Runtime.Expect(ownerships.Take(source, tokenID), "take token failed");
            Runtime.Expect(ownerships.Give(destination, tokenID), "give token failed");

            Runtime.Notify(EventKind.TokenSend, source, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = tokenID, symbol = symbol });
            Runtime.Notify(EventKind.TokenReceive, destination, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = tokenID, symbol = symbol });
        }

        public void SetTokenViewer(Address source, string symbol, string url)
        {
            Runtime.Expect(IsWitness(source), "invalid witness");

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(!token.IsFungible, "token must be non-fungible");

            Runtime.Expect(token.Owner == source, "owner expected");

            token.SetViewer(url);

            Runtime.Notify(EventKind.TokenInfo, source, url);
        }

        #endregion
    }
}
