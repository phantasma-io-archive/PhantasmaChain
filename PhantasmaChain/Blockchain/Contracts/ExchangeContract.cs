using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Mathematics;

namespace Phantasma.Blockchain.Contracts
{
    public enum ExchangeOrderSide
    {
        Buy,
        Sell
    }

    public struct ExchangeOrder
    {
        public readonly Timestamp timestamp;
        public readonly Address creator;
        public readonly Address token;
        public readonly BigInteger quantity;
        public readonly BigInteger rate;
    }

    public sealed class ExchangeContract : NativeContract
    {
        internal override NativeContractKind Kind => NativeContractKind.Exchange;


        public ExchangeContract() : base()
        {
        }

        public void Stake(BigInteger amount)
        {
        }

        public void Unstake(BigInteger amount)
        {
        }
    }
}
