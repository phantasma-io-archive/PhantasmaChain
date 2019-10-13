using Phantasma.Core.Types;
using Phantasma.Cryptography;

namespace Phantasma.Domain
{
    public interface INexus
    {
        string Name { get; }
        bool HasGenesis { get; }
        Address GenesisAddress { get; }
        Hash GenesisHash { get; }
        Timestamp GenesisTime { get; }
        Address RootChainAddress { get; }
    }
}
