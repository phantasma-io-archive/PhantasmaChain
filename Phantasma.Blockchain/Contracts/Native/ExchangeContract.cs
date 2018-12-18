using Phantasma.Blockchain.Tokens;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using System;
using System.Collections.Generic;

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
        public readonly Address Token;
        public readonly BigInteger Quantity;
        public readonly BigInteger Rate;
        public readonly ExchangeOrderSide Side;

        public ExchangeOrder(Timestamp timestamp, Address creator, Address token, BigInteger quantity, BigInteger rate, ExchangeOrderSide side)
        {
            Timestamp = timestamp;
            Creator = creator;
            Token = token;
            Quantity = quantity;
            Rate = rate;
            Side = side;
        }
    }

    public sealed class ExchangeContract : SmartContract
    {
        public override string Name => "exchange";

        private List<ExchangeOrder> orders = new List<ExchangeOrder>();
        private Dictionary<Hash, BigInteger> fills = new Dictionary<Hash, BigInteger>();

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

            var tokenABI = Chain.FindABI(NativeABI.Token);
            //Runtime.Expect(baseTokenContract.ABI.Implements(tokenABI));

            /*switch (side)
            {
                case ExchangeOrderSide.Sell:
                    {
                        var balance = tokenABI["BalanceOf"].Invoke<LargeInteger>(baseTokenContract, from);
                        Runtime.Expect(balance >= quantity);

                        tokenABI["Transfer"].Invoke(baseTokenContract, from, this.Address, quantity);

                        break;
                    }

                case ExchangeOrderSide.Buy:
                    {
                        var balance = tokenABI["BalanceOf"].Invoke<LargeInteger>(quoteTokenContract, from);
                        var Runtime.ExpectedAmount = quantity / rate;
                        Runtime.Expect(Runtime.ExpectedAmount > 0);
                        Runtime.Expect(balance >= Runtime.ExpectedAmount);

                        tokenABI["Transfer"].Invoke(quoteTokenContract, from, this.Address, Runtime.ExpectedAmount);

                        break;
                    }

                default: throw new ContractException("invalid order side");
            }
            */

        }
    }
}
