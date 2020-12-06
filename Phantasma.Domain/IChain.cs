using System.Numerics;
using Phantasma.Core.Types;
using Phantasma.Cryptography;

namespace Phantasma.Domain
{
    public interface IChain
    {
        string Name { get; }
        Address Address { get; }
        BigInteger Height { get; }
    }
}
