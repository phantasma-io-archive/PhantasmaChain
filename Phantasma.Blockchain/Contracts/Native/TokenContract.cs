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

            Runtime.Expect(IsAddressOfParentChain(targetChain) || IsAddressOfChildChain(targetChain), "target must be parent or child chain");

            var otherChain = this.Runtime.Nexus.FindChainByAddress(targetChain);
            /*TODO
            var otherConsensus = (ConsensusContract)otherChain.FindContract(ContractKind.Consensus);
            Runtime.Expect(otherConsensus.IsValidReceiver(from));*/

            Runtime.Expect(this.Runtime.Nexus.TokenExists(symbol), "invalid token");
            var tokenInfo = this.Runtime.Nexus.GetTokenInfo(symbol);
            Runtime.Expect(tokenInfo.Flags.HasFlag(TokenFlags.Fungible), "must be fungible token");

            SupplySheet sourceSupplies;

            if (tokenInfo.IsCapped)
            {
                sourceSupplies = this.Runtime.Chain.GetTokenSupplies(symbol);
                var targetSupplies = otherChain.GetTokenSupplies(symbol);

                if (IsAddressOfParentChain(targetChain))
                {
                    Runtime.Expect(sourceSupplies.MoveToParent(amount), "source supply check failed");
                }
                else // child chain
                {
                    Runtime.Expect(sourceSupplies.MoveToChild(this.Runtime.Chain, amount), "source supply check failed");
                }
            }
            else
            {
                sourceSupplies = null;
            }

            var balances = this.Runtime.Chain.GetTokenBalances(symbol);
            Runtime.Expect(Runtime.Nexus.BurnTokens(symbol, this.Storage, balances, sourceSupplies, from, amount), "burn failed");

            Runtime.Notify(EventKind.TokenBurn, from, new TokenEventData() { symbol = symbol, value = amount, chainAddress = Runtime.Chain.Address });
            Runtime.Notify(EventKind.TokenEscrow, to, new TokenEventData() { symbol = symbol, value = amount, chainAddress = targetChain });
        }

        public void MintTokens(Address to, string symbol, BigInteger amount)
        {
            Runtime.Expect(amount > 0, "amount must be positive and greater than zero");

            Runtime.Expect(this.Runtime.Nexus.TokenExists(symbol), "invalid token");
            var tokenInfo = this.Runtime.Nexus.GetTokenInfo(symbol);
            Runtime.Expect(tokenInfo.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            Runtime.Expect(IsWitness(tokenInfo.Owner), "invalid witness");

            SupplySheet supplies;

            if (tokenInfo.IsCapped)
            {
                supplies = this.Runtime.Chain.GetTokenSupplies(symbol);
                Runtime.Expect(supplies.Mint(amount), "increasing supply failed");
            }
            else
            {
                supplies = null;
            }

            var balances = this.Runtime.Chain.GetTokenBalances(symbol);
            Runtime.Expect(Runtime.Nexus.MintTokens(symbol, this.Storage, balances, supplies, to, amount), "minting failed");

            Runtime.Notify(EventKind.TokenMint, to, new TokenEventData() { symbol = symbol, value = amount, chainAddress = this.Runtime.Chain.Address });
        }

        public void BurnTokens(Address from, string symbol, BigInteger amount)
        {
            Runtime.Expect(amount > 0, "amount must be positive and greater than zero");
            Runtime.Expect(IsWitness(from), "invalid witness");

            Runtime.Expect(this.Runtime.Nexus.TokenExists(symbol), "invalid token");
            var tokenInfo = this.Runtime.Nexus.GetTokenInfo(symbol);
            Runtime.Expect(tokenInfo.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            SupplySheet supplies;
            if (tokenInfo.IsCapped)
            {
                supplies = this.Runtime.Chain.GetTokenSupplies(symbol);
                Runtime.Expect(supplies.Burn(amount), "decreasing supply failed");
            }
            else
            {
                supplies = null;
            }

            var balances = this.Runtime.Chain.GetTokenBalances(symbol);
            Runtime.Expect(this.Runtime.Nexus.BurnTokens(symbol, this.Storage, balances, supplies, from, amount), "burning failed");

            Runtime.Notify(EventKind.TokenBurn, from, new TokenEventData() { symbol = symbol, value = amount });
        }

        public void TransferTokens(Address source, Address destination, string symbol, BigInteger amount)
        {
            Runtime.Expect(amount > 0, "amount must be positive and greater than zero");
            Runtime.Expect(source != destination, "source and destination must be different");
            Runtime.Expect(IsWitness(source), "invalid witness");

            Runtime.Expect(this.Runtime.Nexus.TokenExists(symbol), "invalid token");
            var tokenInfo = this.Runtime.Nexus.GetTokenInfo(symbol);
            Runtime.Expect(tokenInfo.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");
            Runtime.Expect(tokenInfo.Flags.HasFlag(TokenFlags.Transferable), "token must be transferable");

            var balances = this.Runtime.Chain.GetTokenBalances(symbol);
            Runtime.Expect(Runtime.Nexus.TransferTokens(symbol, this.Storage, balances, source, destination, amount), "transfer failed");

            Runtime.Notify(EventKind.TokenSend, source, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = amount, symbol = symbol });
            Runtime.Notify(EventKind.TokenReceive, destination, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = amount, symbol = symbol });
        }

        public BigInteger GetBalance(Address address, string symbol)
        {
            Runtime.Expect(this.Runtime.Nexus.TokenExists(symbol), "invalid token");
            var token = this.Runtime.Nexus.GetTokenInfo(symbol);
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            var balances = this.Runtime.Chain.GetTokenBalances(symbol);
            return balances.Get(this.Storage, address);
        }
        #endregion

        #region NON FUNGIBLE TOKENS
        public BigInteger[] GetTokens(Address address, string symbol)
        {
            Runtime.Expect(this.Runtime.Nexus.TokenExists(symbol), "invalid token");
            var token = this.Runtime.Nexus.GetTokenInfo(symbol);
            Runtime.Expect(!token.IsFungible, "token must be non-fungible");

            var ownerships = this.Runtime.Chain.GetTokenOwnerships(symbol);
            return ownerships.Get(this.Storage, address).ToArray();
        }

        // TODO minting a NFT will require a certain amount of KCAL that is released upon burning
        public BigInteger MintToken(Address to, string symbol, byte[] ram, byte[] rom)
        {
            Runtime.Expect(this.Runtime.Nexus.TokenExists(symbol), "invalid token");
            var tokenInfo = this.Runtime.Nexus.GetTokenInfo(symbol);
            Runtime.Expect(!tokenInfo.IsFungible, "token must be non-fungible");
            Runtime.Expect(IsWitness(tokenInfo.Owner), "invalid witness");

            Runtime.Expect(rom.Length <= TokenContent.MaxROMSize, "ROM size exceeds maximum allowed");
            Runtime.Expect(ram.Length <= TokenContent.MaxRAMSize, "RAM size exceeds maximum allowed");

            var tokenID = this.Runtime.Nexus.CreateNFT(symbol, Runtime.Chain.Address, to, ram, rom);
            Runtime.Expect(tokenID > 0, "invalid tokenID");

            if (tokenInfo.IsCapped)
            {
                var supplies = this.Runtime.Chain.GetTokenSupplies(symbol);
                Runtime.Expect(supplies.Mint(1), "increasing supply failed");
            }

            var ownerships = this.Runtime.Chain.GetTokenOwnerships(symbol);
            Runtime.Expect(Runtime.Nexus.MintToken(symbol), "minting failed");
            Runtime.Expect(ownerships.Give(this.Storage, to, tokenID), "give token failed");

            Runtime.Notify(EventKind.TokenMint, to, new TokenEventData() { symbol = symbol, value = tokenID });
            return tokenID;
        }

        public void BurnToken(Address from, string symbol, BigInteger tokenID)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

            Runtime.Expect(this.Runtime.Nexus.TokenExists(symbol), "invalid token");
            var tokenInfo = this.Runtime.Nexus.GetTokenInfo(symbol);
            Runtime.Expect(!tokenInfo.IsFungible, "token must be non-fungible");

            if (tokenInfo.IsCapped)
            {
                var supplies = this.Runtime.Chain.GetTokenSupplies(symbol);
                Runtime.Expect(supplies.Burn(1), "decreasing supply failed");
            }

            var ownerships = this.Runtime.Chain.GetTokenOwnerships(symbol);
            Runtime.Expect(ownerships.Take(this.Storage, from, tokenID), "take token failed");
            Runtime.Expect(Runtime.Nexus.BurnToken(symbol), "burn failed");

            Runtime.Expect(this.Runtime.Nexus.DestroyNFT(symbol, tokenID), "destroy token failed");

            Runtime.Notify(EventKind.TokenBurn, from, new TokenEventData() { symbol = symbol, value = tokenID });
        }

        public void TransferToken(Address source, Address destination, string symbol, BigInteger tokenID)
        {
            Runtime.Expect(IsWitness(source), "invalid witness");

            Runtime.Expect(source != destination, "source and destination must be different");

            Runtime.Expect(this.Runtime.Nexus.TokenExists(symbol), "invalid token");
            var tokenInfo = this.Runtime.Nexus.GetTokenInfo(symbol);
            Runtime.Expect(!tokenInfo.IsFungible, "token must be non-fungible");

            var ownerships = this.Runtime.Chain.GetTokenOwnerships(symbol);
            Runtime.Expect(ownerships.Take(this.Storage, source, tokenID), "take token failed");
            Runtime.Expect(ownerships.Give(this.Storage, destination, tokenID), "give token failed");

            this.Runtime.Nexus.EditNFTLocation(symbol, tokenID, Runtime.Chain.Address, destination);

            Runtime.Notify(EventKind.TokenSend, source, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = tokenID, symbol = symbol });
            Runtime.Notify(EventKind.TokenReceive, destination, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = tokenID, symbol = symbol });
        }

        public void SendToken(Address targetChain, Address from, Address to, string symbol, BigInteger tokenID)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

            Runtime.Expect(IsAddressOfParentChain(targetChain) || IsAddressOfChildChain(targetChain), "source must be parent or child chain");

            var otherChain = this.Runtime.Nexus.FindChainByAddress(targetChain);

            Runtime.Expect(this.Runtime.Nexus.TokenExists(symbol), "invalid token");
            var tokenInfo = this.Runtime.Nexus.GetTokenInfo(symbol);
            Runtime.Expect(!tokenInfo.Flags.HasFlag(TokenFlags.Fungible), "must be non-fungible token");

            if (tokenInfo.IsCapped)
            {
                var sourceSupplies = this.Runtime.Chain.GetTokenSupplies(symbol);
                var targetSupplies = otherChain.GetTokenSupplies(symbol);

                BigInteger amount = 1;

                if (IsAddressOfParentChain(targetChain))
                {
                    Runtime.Expect(sourceSupplies.MoveToParent(amount), "source supply check failed");
                }
                else // child chain
                {
                    Runtime.Expect(sourceSupplies.MoveToChild(this.Runtime.Chain, amount), "source supply check failed");
                }
            }

            var ownerships = this.Runtime.Chain.GetTokenOwnerships(symbol);
            Runtime.Expect(ownerships.Take(this.Storage, from, tokenID), "take token failed");

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

            Runtime.Expect(this.Runtime.Nexus.TokenExists(symbol), "invalid token");
            var tokenInfo = this.Runtime.Nexus.GetTokenInfo(symbol);

            if (tokenInfo.IsCapped)
            {
                var sourceSupplies = sourceChain.GetTokenSupplies(symbol);
                var targetSupplies = this.Runtime.Chain.GetTokenSupplies(symbol);

                if (IsAddressOfParentChain(sourceChain.Address))
                {
                    Runtime.Expect(targetSupplies.MoveFromParent(value), "target supply check failed");
                }
                else // child chain
                {
                    Runtime.Expect(targetSupplies.MoveFromChild(this.Runtime.Chain, value), "target supply check failed");
                }
            }

            if (tokenInfo.Flags.HasFlag(TokenFlags.Fungible))
            {
                var balances = this.Runtime.Chain.GetTokenBalances(symbol);
                var supplies = tokenInfo.IsCapped ? this.Runtime.Chain.GetTokenSupplies(symbol) : null;
                Runtime.Expect(Runtime.Nexus.MintTokens(symbol, this.Storage, balances, supplies, targetAddress, value), "mint failed");
            }
            else
            {
                var ownerships = this.Runtime.Chain.GetTokenOwnerships(symbol);
                Runtime.Expect(ownerships.Give(this.Storage, targetAddress, value), "give token failed");
                Runtime.Expect(Runtime.Nexus.MintToken(symbol), "mint failed");

                this.Runtime.Nexus.EditNFTLocation(symbol, value, Runtime.Chain.Address, targetAddress);
            }

            Runtime.Notify(EventKind.TokenReceive, targetAddress, new TokenEventData() { symbol = symbol, value = value, chainAddress = sourceChain.Address });
        }

        public void SettleBlock(Address sourceChainAddress, Hash hash)
        {
            Runtime.Expect(IsAddressOfParentChain(sourceChainAddress) || IsAddressOfChildChain(sourceChainAddress), "source must be parent or child chain");

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
