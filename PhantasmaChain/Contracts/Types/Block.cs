using System;
using System.Numerics;

namespace Phantasma.Contracts.Types
{
    public class Block
    {
        public BigInteger Height { get; }
        public byte[] Hash { get; }
        public byte[] PreviousHash { get; }
        public Timestamp Time;
    }
}
