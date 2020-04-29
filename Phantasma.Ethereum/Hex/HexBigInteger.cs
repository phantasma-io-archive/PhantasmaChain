using System;
using System.Numerics;
using Nethereum.Hex.HexConvertors;

namespace Nethereum.Hex.HexTypes
{
    public class HexBigInteger : HexRPCType<BigInteger>
    {
        public HexBigInteger(string hex) : base(new HexBigIntegerBigEndianConvertor(), hex)
        {
        }

        public HexBigInteger(BigInteger value) : base(value, new HexBigIntegerBigEndianConvertor())
        {
        }

        
        public override string ToString()
        {
            return Value.ToString();
        }
    }
}