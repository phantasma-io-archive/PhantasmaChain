using System.Linq;

namespace Phantasma.Contracts.Types
{
    public interface Address
    {
        byte[] PublicKey { get; }
    }
}
