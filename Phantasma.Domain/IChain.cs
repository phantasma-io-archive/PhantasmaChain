using Phantasma.Cryptography;
using Phantasma.Numerics;

namespace Phantasma.Domain
{
    public interface IChain
    {
        string Name { get; }
        Address Address { get; }
        BigInteger BlockHeight { get; }
        IBlock LastBlock { get; }
        bool IsRoot { get; }
    }
}
