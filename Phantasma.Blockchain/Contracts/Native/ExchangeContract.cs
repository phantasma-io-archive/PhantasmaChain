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

    public sealed class ExchangeContract : NativeContract
    {
        public override ContractKind Kind => ContractKind.Exchange;

        private List<ExchangeOrder> orders = new List<ExchangeOrder>();
        private Dictionary<Hash, BigInteger> fills = new Dictionary<Hash, BigInteger>();

        public ExchangeContract() : base()
        {
        }

        public void OpenOrder(Address from, Address baseToken, Address quoteToken, BigInteger quantity, BigInteger rate, ExchangeOrderSide side)
        {
            Expect(IsWitness(from));

            Expect(baseToken!= null);
            Expect(quoteToken != null);

            var tokenABI = Chain.FindABI(NativeABI.Token);
            //Expect(baseTokenContract.ABI.Implements(tokenABI));

            /*switch (side)
            {
                case ExchangeOrderSide.Sell:
                    {
                        var balance = tokenABI["BalanceOf"].Invoke<BigInteger>(baseTokenContract, from);
                        Expect(balance >= quantity);

                        tokenABI["Transfer"].Invoke(baseTokenContract, from, this.Address, quantity);

                        break;
                    }

                case ExchangeOrderSide.Buy:
                    {
                        var balance = tokenABI["BalanceOf"].Invoke<BigInteger>(quoteTokenContract, from);
                        var expectedAmount = quantity / rate;
                        Expect(expectedAmount > 0);
                        Expect(balance >= expectedAmount);

                        tokenABI["Transfer"].Invoke(quoteTokenContract, from, this.Address, expectedAmount);

                        break;
                    }

                default: throw new ContractException("invalid order side");
            }
            */

        }

        public void Unstake(BigInteger amount)
        {
        }
    }
}
