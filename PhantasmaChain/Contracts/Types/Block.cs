using System;
using System.Numerics;

namespace Phantasma.Contracts.Types
{
    public interface IBlock
    {
        BigInteger Height { get; }
        byte[] Hash { get; }
        byte[] PreviousHash { get; }
        Timestamp Time { get; }
    }
}
