using Phantasma.Blockchain.Tokens;
using Phantasma.Cryptography;
using Phantasma.Storage;
using Phantasma.Numerics;
using System.Linq;
using Phantasma.Storage.Context;

/*
 * Token script triggers
 * OnMint(symbol, address, amount)
 * OnBurn(symbol, address, amount)
 * OnSend(symbol, address, amount)
 * OnReceive(symbol, address, amount)
*/
namespace Phantasma.Blockchain.Contracts.Native
{
    public class TokenContract : SmartContract
    {
        public override string Name => "token";

        public static readonly string TriggerMint = "OnMint";
        public static readonly string TriggerBurn = "OnBurn";
        public static readonly string TriggerSend = "OnSend";
        public static readonly string TriggerReceive = "OnReceive";

        #region FUNGIBLE TOKENS
        public void SendTokens(Address targetChainAddress, Address from, Address to, string symbol, BigInteger amount)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

            Runtime.Expect(IsAddressOfParentChain(targetChainAddress) || IsAddressOfChildChain(targetChainAddress), "target must be parent or child chain");

            Runtime.Expect(!to.IsInterop, "destination cannot be interop address");

            var targetChain = this.Runtime.Nexus.FindChainByAddress(targetChainAddress);

            Runtime.Expect(this.Runtime.Nexus.TokenExists(symbol), "invalid token");
            var tokenInfo = this.Runtime.Nexus.GetTokenInfo(symbol);
            Runtime.Expect(tokenInfo.Flags.HasFlag(TokenFlags.Fungible), "must be fungible token");

            if (tokenInfo.IsCapped)
            {
                var sourceSupplies = new SupplySheet(symbol, this.Runtime.Chain, Runtime.Nexus);
                var targetSupplies = new SupplySheet(symbol, targetChain, Runtime.Nexus);

                if (IsAddressOfParentChain(targetChainAddress))
                {
                    Runtime.Expect(sourceSupplies.MoveToParent(this.Storage, amount), "source supply check failed");
                }
                else // child chain
                {
                    Runtime.Expect(sourceSupplies.MoveToChild(this.Storage, targetChain.Name, amount), "source supply check failed");
                }
            }

            Runtime.Expect(Runtime.Nexus.BurnTokens(Runtime, symbol, from, amount), "burn failed");

            Runtime.Notify(EventKind.TokenBurn, from, new TokenEventData() { symbol = symbol, value = amount, chainAddress = Runtime.Chain.Address });
            Runtime.Notify(EventKind.TokenEscrow, to, new TokenEventData() { symbol = symbol, value = amount, chainAddress = targetChainAddress });
        }

        public void MintTokens(Address to, string symbol, BigInteger amount)
        {
            Runtime.Expect(amount > 0, "amount must be positive and greater than zero");

            Runtime.Expect(this.Runtime.Nexus.TokenExists(symbol), "invalid token");
            var tokenInfo = this.Runtime.Nexus.GetTokenInfo(symbol);
            Runtime.Expect(tokenInfo.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            Runtime.Expect(!to.IsInterop, "destination cannot be interop address");

            Runtime.Expect(IsWitness(tokenInfo.Owner), "invalid witness");

            Runtime.Expect(Runtime.Nexus.MintTokens(Runtime, symbol, to, amount), "minting failed");

            Runtime.Notify(EventKind.TokenMint, to, new TokenEventData() { symbol = symbol, value = amount, chainAddress = this.Runtime.Chain.Address });
        }

        public void BurnTokens(Address from, string symbol, BigInteger amount)
        {
            Runtime.Expect(amount > 0, "amount must be positive and greater than zero");
            Runtime.Expect(IsWitness(from), "invalid witness");

            Runtime.Expect(this.Runtime.Nexus.TokenExists(symbol), "invalid token");
            var tokenInfo = this.Runtime.Nexus.GetTokenInfo(symbol);
            Runtime.Expect(tokenInfo.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");
            Runtime.Expect(tokenInfo.IsBurnable, "token must be burnable");

            Runtime.Expect(this.Runtime.Nexus.BurnTokens(Runtime, symbol, from, amount), "burning failed");

            Runtime.Notify(EventKind.TokenBurn, from, new TokenEventData() { symbol = symbol, value = amount });
        }

        public void TransferTokens(Address source, Address destination, string symbol, BigInteger amount)
        {
            Runtime.Expect(amount > 0, "amount must be positive and greater than zero");
            Runtime.Expect(source != destination, "source and destination must be different");
            Runtime.Expect(IsWitness(source), "invalid witness");

            if (destination.IsInterop)
            {
                Runtime.Expect(Runtime.Chain.IsRoot, "interop transfers only allowed in main chain");
                Runtime.CallContext("interop", "WithdrawTokens", source, destination, symbol, amount);
                return;
            }

            Runtime.Expect(this.Runtime.Nexus.TokenExists(symbol), "invalid token");
            var tokenInfo = this.Runtime.Nexus.GetTokenInfo(symbol);
            Runtime.Expect(tokenInfo.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");
            Runtime.Expect(tokenInfo.Flags.HasFlag(TokenFlags.Transferable), "token must be transferable");

            Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, symbol, source, destination, amount), "transfer failed");

            Runtime.Notify(EventKind.TokenSend, source, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = amount, symbol = symbol });
            Runtime.Notify(EventKind.TokenReceive, destination, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = amount, symbol = symbol });
        }

        public BigInteger GetBalance(Address address, string symbol)
        {
            Runtime.Expect(this.Runtime.Nexus.TokenExists(symbol), "invalid token");
            var token = this.Runtime.Nexus.GetTokenInfo(symbol);
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            var balances = new BalanceSheet(symbol);
            return balances.Get(this.Storage, address);
        }
        #endregion

        #region NON FUNGIBLE TOKENS
        public BigInteger[] GetTokens(Address address, string symbol)
        {
            Runtime.Expect(this.Runtime.Nexus.TokenExists(symbol), "invalid token");
            var token = this.Runtime.Nexus.GetTokenInfo(symbol);
            Runtime.Expect(!token.IsFungible, "token must be non-fungible");

            var ownerships = new OwnershipSheet(symbol);
            return ownerships.Get(this.Storage, address).ToArray();
        }

        // TODO minting a NFT will require a certain amount of KCAL that is released upon burning
        public BigInteger MintToken(Address to, string symbol, byte[] rom, byte[] ram, BigInteger value)
        {
            Runtime.Expect(this.Runtime.Nexus.TokenExists(symbol), "invalid token");
            var tokenInfo = this.Runtime.Nexus.GetTokenInfo(symbol);
            Runtime.Expect(!tokenInfo.IsFungible, "token must be non-fungible");
            Runtime.Expect(IsWitness(tokenInfo.Owner), "invalid witness");

            Runtime.Expect(!to.IsInterop, "destination cannot be interop address");
            Runtime.Expect(Runtime.Chain.Name == Nexus.RootChainName, "can only mint nft in root chain");

            Runtime.Expect(rom.Length <= TokenContent.MaxROMSize, "ROM size exceeds maximum allowed");
            Runtime.Expect(ram.Length <= TokenContent.MaxRAMSize, "RAM size exceeds maximum allowed");

            var tokenID = this.Runtime.Nexus.CreateNFT(symbol, Runtime.Chain.Address, rom, ram, value);
            Runtime.Expect(tokenID > 0, "invalid tokenID");

            Runtime.Expect(Runtime.Nexus.MintToken(Runtime, symbol, to, tokenID), "minting failed");

            if (tokenInfo.IsBurnable)
            {
                Runtime.Expect(value > 0, "token must have value");
                Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, Nexus.FuelTokenSymbol, tokenInfo.Owner, Runtime.Chain.Address, tokenID), "minting escrow failed");
                Runtime.Notify(EventKind.TokenEscrow, to, new TokenEventData() { symbol = symbol, value = value, chainAddress = Runtime.Chain.Address });
            }
            else
            {
                Runtime.Expect(value == 0, "non-burnable must have value zero");
            }

            Runtime.Notify(EventKind.TokenMint, to, new TokenEventData() { symbol = symbol, value = tokenID, chainAddress = Runtime.Chain.Address });
            return tokenID;
        }

        public void BurnToken(Address from, string symbol, BigInteger tokenID)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

            Runtime.Expect(this.Runtime.Nexus.TokenExists(symbol), "invalid token");
            var tokenInfo = this.Runtime.Nexus.GetTokenInfo(symbol);
            Runtime.Expect(!tokenInfo.IsFungible, "token must be non-fungible");
            Runtime.Expect(tokenInfo.IsBurnable, "token must be burnable");

            var nft = Runtime.Nexus.GetNFT(symbol, tokenID);

            Runtime.Expect(Runtime.Nexus.BurnToken(Runtime, symbol, from, tokenID), "burn failed");

            Runtime.Expect(this.Runtime.Nexus.TransferTokens(Runtime, Nexus.FuelTokenSymbol, Runtime.Chain.Address, from, nft.Value), "energy claim failed");

            Runtime.Notify(EventKind.TokenBurn, from, new TokenEventData() { symbol = symbol, value = tokenID, chainAddress = Runtime.Chain.Address });
            Runtime.Notify(EventKind.TokenClaim, from, new TokenEventData() { symbol = Nexus.FuelTokenName, value = nft.Value, chainAddress = Runtime.Chain.Address });
        }

        public void TransferToken(Address source, Address destination, string symbol, BigInteger tokenID)
        {
            Runtime.Expect(IsWitness(source), "invalid witness");

            Runtime.Expect(source != destination, "source and destination must be different");

            Runtime.Expect(this.Runtime.Nexus.TokenExists(symbol), "invalid token");
            var tokenInfo = this.Runtime.Nexus.GetTokenInfo(symbol);
            Runtime.Expect(!tokenInfo.IsFungible, "token must be non-fungible");

            Runtime.Expect(Runtime.Nexus.TransferToken(Runtime, symbol, source, destination, tokenID), "transfer failed");

            Runtime.Notify(EventKind.TokenSend, source, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = tokenID, symbol = symbol });
            Runtime.Notify(EventKind.TokenReceive, destination, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = tokenID, symbol = symbol });
        }

        public void SendToken(Address targetChainAddress, Address from, Address to, string symbol, BigInteger tokenID)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

            Runtime.Expect(IsAddressOfParentChain(targetChainAddress) || IsAddressOfChildChain(targetChainAddress), "source must be parent or child chain");

            Runtime.Expect(!to.IsInterop, "destination cannot be interop address");

            var targetChain = this.Runtime.Nexus.FindChainByAddress(targetChainAddress);

            Runtime.Expect(this.Runtime.Nexus.TokenExists(symbol), "invalid token");
            var tokenInfo = this.Runtime.Nexus.GetTokenInfo(symbol);
            Runtime.Expect(!tokenInfo.Flags.HasFlag(TokenFlags.Fungible), "must be non-fungible token");

            if (tokenInfo.IsCapped)
            {
                var supplies = new SupplySheet(symbol, this.Runtime.Chain, Runtime.Nexus);

                BigInteger amount = 1;

                if (IsAddressOfParentChain(targetChainAddress))
                {
                    Runtime.Expect(supplies.MoveToParent(this.Storage, amount), "source supply check failed");
                }
                else // child chain
                {
                    Runtime.Expect(supplies.MoveToChild(this.Storage, this.Runtime.Chain.Name, amount), "source supply check failed");
                }
            }

            Runtime.Expect(Runtime.Nexus.TransferToken(Runtime, symbol, from, targetChainAddress, tokenID), "take token failed");

            Runtime.Notify(EventKind.TokenBurn, from, new TokenEventData() { symbol = symbol, value = tokenID, chainAddress = Runtime.Chain.Address });
            Runtime.Notify(EventKind.TokenEscrow, to, new TokenEventData() { symbol = symbol, value = tokenID, chainAddress = targetChainAddress });
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
                var supplies = new SupplySheet(symbol, this.Runtime.Chain, Runtime.Nexus);
                
                if (IsAddressOfParentChain(sourceChain.Address))
                {
                    Runtime.Expect(supplies.MoveFromParent(this.Storage, value), "target supply check failed");
                }
                else // child chain
                {
                    Runtime.Expect(supplies.MoveFromChild(this.Storage, sourceChain.Name, value), "target supply check failed");
                }
            }

            if (tokenInfo.Flags.HasFlag(TokenFlags.Fungible))
            {
                Runtime.Expect(Runtime.Nexus.MintTokens(Runtime, symbol, targetAddress, value), "mint failed");
            }
            else
            {
                Runtime.Expect(Runtime.Nexus.MintToken(Runtime, symbol, targetAddress, value), "mint failed");
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
