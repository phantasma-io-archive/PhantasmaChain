using Phantasma.Blockchain.Storage;
using Phantasma.Blockchain.Tokens;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
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

    public sealed class ExchangeContract : SmartContract
    {
        public override string Name => "exchange";

        private StorageMap _orders; //<string, Collection<ExchangeOrder>
        private StorageMap fills; //<Hash, BigInteger>

        public ExchangeContract() : base()
        {
        }

        public void OpenOrder(Address from, string baseSymbol, string quoteSymbol, BigInteger quantity, BigInteger rate, ExchangeOrderSide side)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

            var baseToken = Runtime.Nexus.FindTokenBySymbol(baseSymbol);
            Runtime.Expect(baseToken != null, "invalid base token");
            Runtime.Expect(baseToken.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            var quoteToken = Runtime.Nexus.FindTokenBySymbol(quoteSymbol);
            Runtime.Expect(quoteToken != null, "invalid quote token");
            Runtime.Expect(quoteToken.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            //var tokenABI = Chain.FindABI(NativeABI.Token);
            //Runtime.Expect(baseTokenContract.ABI.Implements(tokenABI));

            var pair = baseSymbol + "_" + quoteSymbol;

            switch (side)
            {
                case ExchangeOrderSide.Sell:
                    {
                        var balances = Runtime.Chain.GetTokenBalances(baseToken);
                        var balance = balances.Get(from);
                        Runtime.Expect(balance >= quantity, "not enought balance");

                        Runtime.Expect(baseToken.Transfer(balances, from, Runtime.Chain.Address, quantity), "transfer failed");

                        break;
                    }

                case ExchangeOrderSide.Buy:
                    {
                        var balances = Runtime.Chain.GetTokenBalances(quoteToken);
                        var balance = balances.Get(from);

                        var expectedAmount = quantity / rate;
                        Runtime.Expect(balance >= expectedAmount, "not enought balance");

                        Runtime.Expect(baseToken.Transfer(balances, from, Runtime.Chain.Address, expectedAmount), "transfer failed");
                        break;
                    }

                default: throw new ContractException("invalid order side");
            }

            var order = new ExchangeOrder(Timestamp.Now, from, quantity, rate, side);
            var list = _orders.Get<string, StorageList>(pair);
            list.Add(order);
        }
    }
}
