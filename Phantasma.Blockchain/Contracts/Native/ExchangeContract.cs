using Phantasma.Blockchain.Storage;
using Phantasma.Blockchain.Tokens;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Cryptography.EdDSA;
using Phantasma.IO;
using Phantasma.Numerics;

namespace Phantasma.Blockchain.Contracts.Native
{
    public enum ExchangeOrderSide
    {
        Buy,
        Sell
    }

    public struct ExchangeOrder
    {
        public readonly Timestamp Timestamp;
        public readonly Address Creator;
        public readonly BigInteger Quantity;
        public readonly BigInteger Rate;
        public readonly ExchangeOrderSide Side;

        public ExchangeOrder(Timestamp timestamp, Address creator, BigInteger quantity, BigInteger rate, ExchangeOrderSide side)
        {
            Timestamp = timestamp;
            Creator = creator;
            Quantity = quantity;
            Rate = rate;
            Side = side;
        }
    }

    public struct TokenSwap
    {
        public Address buyer;
        public Address seller;
        public string baseSymbol;
        public string quoteSymbol;
        public BigInteger value;
        public BigInteger price;
    }

    public sealed class ExchangeContract : SmartContract
    {
        public override string Name => "exchange";

        internal StorageMap _orders; //<string, Collection<ExchangeOrder>
        internal StorageMap _fills; //<Hash, BigInteger>

        public ExchangeContract() : base()
        {
        }

        public void OpenOrder(Address from, string baseSymbol, string quoteSymbol, BigInteger quantity, BigInteger rate, ExchangeOrderSide side)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

            Runtime.Expect(Runtime.Nexus.TokenExists(baseSymbol), "invalid base token");
            var baseToken = Runtime.Nexus.GetTokenInfo(baseSymbol);
            Runtime.Expect(baseToken.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            Runtime.Expect(Runtime.Nexus.TokenExists(quoteSymbol), "invalid quote token");
            var quoteToken = Runtime.Nexus.GetTokenInfo(quoteSymbol);
            Runtime.Expect(quoteToken.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            //var tokenABI = Chain.FindABI(NativeABI.Token);
            //Runtime.Expect(baseTokenContract.ABI.Implements(tokenABI));

            var pair = baseSymbol + "_" + quoteSymbol;

            switch (side)
            {
                case ExchangeOrderSide.Sell:
                    {
                        var balances = Runtime.Chain.GetTokenBalances(baseSymbol);
                        var balance = balances.Get(this.Storage, from);
                        Runtime.Expect(balance >= quantity, "not enought balance");

                        Runtime.Expect(Runtime.Nexus.TransferTokens(baseSymbol, this.Storage, balances, from, Runtime.Chain.Address, quantity), "transfer failed");

                        break;
                    }

                case ExchangeOrderSide.Buy:
                    {
                        var balances = Runtime.Chain.GetTokenBalances(quoteSymbol);
                        var balance = balances.Get(this.Storage, from);

                        var expectedAmount = quantity / rate;
                        Runtime.Expect(balance >= expectedAmount, "not enought balance");

                        // TODO check this
                        Runtime.Expect(Runtime.Nexus.TransferTokens(quoteSymbol, this.Storage, balances, from, Runtime.Chain.Address, expectedAmount), "transfer failed");
                        break;
                    }

                default: throw new ContractException("invalid order side");
            }

            var order = new ExchangeOrder(Runtime.Time, from, quantity, rate, side);
            var list = _orders.Get<string, StorageList>(pair);
            list.Add(order);
        }

        #region OTC TRADES
        public void SwapTokens(Address buyer, Address seller, string baseSymbol, string quoteSymbol, BigInteger amount, BigInteger price, byte[] signature)
        {
            Runtime.Expect(IsWitness(buyer), "invalid witness");
            Runtime.Expect(seller != buyer, "invalid seller");

            Runtime.Expect(Runtime.Nexus.TokenExists(baseSymbol), "invalid base token");
            var baseToken = Runtime.Nexus.GetTokenInfo(baseSymbol);
            Runtime.Expect(baseToken.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            var baseBalances = Runtime.Chain.GetTokenBalances(baseSymbol);
            var baseBalance = baseBalances.Get(this.Storage, seller);
            Runtime.Expect(baseBalance >= amount, "invalid amount");

            var swap = new TokenSwap()
            {
                baseSymbol = baseSymbol,
                quoteSymbol = quoteSymbol,
                buyer = buyer,
                seller = seller,
                price = price,
                value = amount,
            };

            var msg = Serialization.Serialize(swap);
            Runtime.Expect(Ed25519.Verify(signature, msg, seller.PublicKey), "invalid signature");

            Runtime.Expect(Runtime.Nexus.TokenExists(quoteSymbol), "invalid quote token");
            var quoteToken = Runtime.Nexus.GetTokenInfo(quoteSymbol);
            Runtime.Expect(quoteToken.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            var quoteBalances = Runtime.Chain.GetTokenBalances(quoteSymbol);
            var quoteBalance = quoteBalances.Get(this.Storage, buyer);
            Runtime.Expect(quoteBalance >= price, "invalid balance");

            Runtime.Expect(Runtime.Nexus.TransferTokens(quoteSymbol, this.Storage, quoteBalances, buyer, seller, price), "payment failed");
            Runtime.Expect(Runtime.Nexus.TransferTokens(baseSymbol, this.Storage, baseBalances, seller, buyer, amount), "transfer failed");

            Runtime.Notify(EventKind.TokenSend, seller, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = baseSymbol, value = amount });
            Runtime.Notify(EventKind.TokenSend, buyer, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = quoteSymbol, value = price });

            Runtime.Notify(EventKind.TokenReceive, seller, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = quoteSymbol, value = price });
            Runtime.Notify(EventKind.TokenReceive, buyer, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = baseSymbol, value = amount });
        }

        public void SwapToken(Address buyer, Address seller, string baseSymbol, string quoteSymbol, BigInteger tokenID, BigInteger price, byte[] signature)
        {
            Runtime.Expect(IsWitness(buyer), "invalid witness");
            Runtime.Expect(seller != buyer, "invalid seller");

            Runtime.Expect(Runtime.Nexus.TokenExists(baseSymbol), "invalid base token");
            var baseToken = Runtime.Nexus.GetTokenInfo(baseSymbol);
            Runtime.Expect(!baseToken.Flags.HasFlag(TokenFlags.Fungible), "token must be non-fungible");

            var ownerships = Runtime.Chain.GetTokenOwnerships(baseSymbol);
            var owner = ownerships.GetOwner(this.Storage, tokenID);
            Runtime.Expect(owner == seller, "invalid owner");

            var swap = new TokenSwap()
            {
                baseSymbol = baseSymbol,
                quoteSymbol = quoteSymbol,
                buyer = buyer,
                seller = seller,
                price = price,
                value = tokenID,
            };

            var msg = Serialization.Serialize(swap);
            Runtime.Expect(Ed25519.Verify(signature, msg, seller.PublicKey), "invalid signature");

            Runtime.Expect(Runtime.Nexus.TokenExists(quoteSymbol), "invalid quote token");
            var quoteToken = Runtime.Nexus.GetTokenInfo(quoteSymbol);
            Runtime.Expect(quoteToken.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            var balances = Runtime.Chain.GetTokenBalances(quoteSymbol);
            var balance = balances.Get(this.Storage, buyer);
            Runtime.Expect(balance >= price, "invalid balance");

            Runtime.Expect(Runtime.Nexus.TransferTokens(quoteSymbol, this.Storage, balances, buyer, owner, price), "payment failed");
            Runtime.Expect(Runtime.Nexus.TransferToken(baseSymbol, this.Storage, ownerships, owner, buyer, tokenID), "transfer failed");

            Runtime.Notify(EventKind.TokenSend, seller, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = baseSymbol, value = tokenID });
            Runtime.Notify(EventKind.TokenSend, buyer, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = quoteSymbol, value = price });

            Runtime.Notify(EventKind.TokenReceive, seller, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = quoteSymbol, value = price });
            Runtime.Notify(EventKind.TokenReceive, buyer, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = baseSymbol, value = tokenID });
        }
        #endregion
    }
}
