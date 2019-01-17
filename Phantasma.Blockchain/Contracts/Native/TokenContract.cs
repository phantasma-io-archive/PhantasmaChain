using Phantasma.Blockchain.Storage;
using Phantasma.Blockchain.Tokens;
using Phantasma.Cryptography;
using Phantasma.IO;
using Phantasma.Numerics;
using System.Linq;

namespace Phantasma.Blockchain.Contracts.Native
{
    public class TokenContract : SmartContract
    {
        public override string Name => "token";

        #region FUNGIBLE TOKENS
        public void SendTokens(Address targetChain, Address from, Address to, string symbol, BigInteger amount)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

            Runtime.Expect(IsParentChain(targetChain) || IsChildChain(targetChain), "target must be parent or child chain");

            var otherChain = this.Runtime.Nexus.FindChainByAddress(targetChain);
            /*TODO
            var otherConsensus = (ConsensusContract)otherChain.FindContract(ContractKind.Consensus);
            Runtime.Expect(otherConsensus.IsValidReceiver(from));*/

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "must be fungible token");

            if (token.IsCapped)
            {
                var sourceSupplies = this.Runtime.Chain.GetTokenSupplies(token);
                var targetSupplies = otherChain.GetTokenSupplies(token);

                if (IsParentChain(targetChain))
                {
                    Runtime.Expect(sourceSupplies.MoveToParent(amount), "source supply check failed");
                    Runtime.Expect(targetSupplies.MoveFromChild(this.Runtime.Chain, amount), "target supply check failed");
                }
                else // child chain
                {
                    Runtime.Expect(sourceSupplies.MoveToChild(this.Runtime.Chain, amount), "source supply check failed");
                    Runtime.Expect(targetSupplies.MoveFromParent(amount), "target supply check failed");
                }
            }

            var balances = this.Runtime.Chain.GetTokenBalances(token);
            Runtime.Expect(token.Burn(balances, from, amount), "burn failed");

            Runtime.Notify(EventKind.TokenBurn, from, new TokenEventData() { symbol = symbol, value = amount, chainAddress = Runtime.Chain.Address });
            Runtime.Notify(EventKind.TokenEscrow, to, new TokenEventData() { symbol = symbol, value = amount, chainAddress = targetChain });
        }

        public void MintTokens(Address to, string symbol, BigInteger amount)
        {
            Runtime.Expect(amount > 0, "amount must be positive and greater than zero");

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            Runtime.Expect(IsWitness(token.Owner), "invalid witness");

            if (token.IsCapped)
            {
                var supplies = this.Runtime.Chain.GetTokenSupplies(token);
                Runtime.Expect(supplies.Mint(amount), "increasing supply failed");
            }

            var balances = this.Runtime.Chain.GetTokenBalances(token);
            Runtime.Expect(token.Mint(balances, to, amount), "minting failed");

            Runtime.Notify(EventKind.TokenMint, to, new TokenEventData() { symbol = symbol, value = amount, chainAddress = this.Runtime.Chain.Address });
        }

        public void BurnTokens(Address from, string symbol, BigInteger amount)
        {
            Runtime.Expect(amount > 0, "amount must be positive and greater than zero");
            Runtime.Expect(IsWitness(from), "invalid witness");

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            if (token.IsCapped)
            {
                var supplies = this.Runtime.Chain.GetTokenSupplies(token);
                Runtime.Expect(supplies.Burn(amount), "decreasing supply failed");
            }

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

        public BigInteger MintToken(Address to, string symbol, byte[] ram, byte[] rom)
        {
            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(!token.IsFungible, "token must be non-fungible");
            Runtime.Expect(IsWitness(token.Owner), "invalid witness");

            var tokenID = this.Runtime.Nexus.CreateNFT(token, ram, rom);
            Runtime.Expect(tokenID > 0, "invalid tokenID");

            if (token.IsCapped)
            {
                var supplies = this.Runtime.Chain.GetTokenSupplies(token);
                Runtime.Expect(supplies.Mint(1), "increasing supply failed");
            }

            var ownerships = this.Runtime.Chain.GetTokenOwnerships(token);
            Runtime.Expect(token.Mint(), "minting failed");
            Runtime.Expect(ownerships.Give(to, tokenID), "give token failed");

            Runtime.Notify(EventKind.TokenMint, to, new TokenEventData() { symbol = symbol, value = tokenID });
            return tokenID;
        }

        public void BurnToken(Address from, string symbol, BigInteger tokenID)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(!token.IsFungible, "token must be non-fungible");

            if (token.IsCapped)
            {
                var supplies = this.Runtime.Chain.GetTokenSupplies(token);                
                Runtime.Expect(supplies.Burn(1), "decreasing supply failed");
            }

            var ownerships = this.Runtime.Chain.GetTokenOwnerships(token);
            Runtime.Expect(ownerships.Take(from, tokenID), "take token failed");
            Runtime.Expect(token.Burn(), "burn failed");

            Runtime.Expect(this.Runtime.Nexus.DestroyNFT(token, tokenID), "destroy token failed");

            Runtime.Notify(EventKind.TokenBurn, from, new TokenEventData() { symbol = symbol, value = tokenID });
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

        public void SendToken(Address targetChain, Address from, Address to, string symbol, BigInteger tokenID)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

            Runtime.Expect(IsParentChain(targetChain) || IsChildChain(targetChain), "source must be parent or child chain");

            var otherChain = this.Runtime.Nexus.FindChainByAddress(targetChain);

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(!token.Flags.HasFlag(TokenFlags.Fungible), "must be non-fungible token");

            if (token.IsCapped)
            {
                var sourceSupplies = this.Runtime.Chain.GetTokenSupplies(token);
                var targetSupplies = otherChain.GetTokenSupplies(token);

                BigInteger amount = 1;

                if (IsParentChain(targetChain))
                {
                    Runtime.Expect(sourceSupplies.MoveToParent(amount), "source supply check failed");
                    Runtime.Expect(targetSupplies.MoveFromChild(this.Runtime.Chain, amount), "target supply check failed");
                }
                else // child chain
                {
                    Runtime.Expect(sourceSupplies.MoveToChild(this.Runtime.Chain, amount), "source supply check failed");
                    Runtime.Expect(targetSupplies.MoveFromParent(amount), "target supply check failed");
                }
            }

            var ownerships = this.Runtime.Chain.GetTokenOwnerships(token);
            Runtime.Expect(ownerships.Take(from, tokenID), "take token failed");

            Runtime.Notify(EventKind.TokenBurn, from, new TokenEventData() { symbol = symbol, value = tokenID, chainAddress = Runtime.Chain.Address });
            Runtime.Notify(EventKind.TokenEscrow, to, new TokenEventData() { symbol = symbol, value = tokenID, chainAddress = targetChain });
        }

        #endregion

        #region SETTLEMENTS
        // NOTE we should later prevent contracts from manipulating those
        private StorageMap _settledTransactions; //<Hash, bool>

        public bool IsSettled(Hash hash)
        {
            return _settledTransactions.ContainsKey(hash);
        }

        protected void RegisterHashAsKnown(Hash hash)
        {
            _settledTransactions.Set(hash, true);
        }

        private void DoSettlement(Chain sourceChain, Address targetAddress, TokenEventData data)
        {
            var symbol = data.symbol;
            var value = data.value;

            Runtime.Expect(value > 0, "value must be greater than zero");
            Runtime.Expect(targetAddress != Address.Null, "target must not be null");

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");

            if (token.Flags.HasFlag(TokenFlags.Fungible))
            {
                if (token.IsCapped)
                {
                    var sourceSupplies = sourceChain.GetTokenSupplies(token);
                    var targetSupplies = this.Runtime.Chain.GetTokenSupplies(token);

                    if (IsParentChain(sourceChain.Address))
                    {
                        Runtime.Expect(sourceSupplies.MoveToChild(this.Runtime.Chain, value), "source supply check failed");
                        Runtime.Expect(targetSupplies.MoveFromParent(value), "target supply check failed");
                    }
                    else // child chain
                    {
                        Runtime.Expect(sourceSupplies.MoveToParent(value), "source supply check failed");
                        Runtime.Expect(targetSupplies.MoveFromChild(this.Runtime.Chain, value), "target supply check failed");
                    }
                }

                var balances = this.Runtime.Chain.GetTokenBalances(token);
                Runtime.Expect(token.Mint(balances, targetAddress, value), "mint failed");
            }
            else
            {
                var ownerships = this.Runtime.Chain.GetTokenOwnerships(token);
                Runtime.Expect(ownerships.Give(targetAddress, value), "give token failed");
            }

            Runtime.Notify(EventKind.TokenReceive, targetAddress, new TokenEventData() { symbol = symbol, value = value, chainAddress = sourceChain.Address });
        }

        public void SettleBlock(Address sourceChainAddress, Hash hash)
        {
            Runtime.Expect(IsParentChain(sourceChainAddress) || IsChildChain(sourceChainAddress), "source must be parent or child chain");

            Runtime.Expect(!IsSettled(hash), "hash already settled");

            var sourceChain = this.Runtime.Nexus.FindChainByAddress(sourceChainAddress);

            var block = sourceChain.FindBlockByHash(hash);
            Runtime.Expect(block != null, "invalid block");

            int settlements = 0;

            foreach (var txHash in block.TransactionHashes)
            {
                var evts = block.GetEventsForTransaction(txHash);

                foreach (var evt in evts)
                {
                    if (evt.Kind == EventKind.TokenEscrow)
                    {
                        var data = Serialization.Unserialize<TokenEventData>(evt.Data);
                        if (data.chainAddress == this.Runtime.Chain.Address)
                        {
                            DoSettlement(sourceChain, evt.Address, data);
                            settlements++;
                        }
                    }
                }
            }

            Runtime.Expect(settlements > 0, "no settlements in the block");
            RegisterHashAsKnown(hash);
        }
        #endregion
    }
}
