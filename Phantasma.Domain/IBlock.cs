using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;

namespace Phantasma.Domain
{
    public interface IBlock
    {
        Address ChainAddress { get; }
        BigInteger Height { get; }
        Timestamp Timestamp { get; }
        Hash PreviousHash { get; }
        byte[] Payload { get; }
        Hash Hash { get; }
    }
}
