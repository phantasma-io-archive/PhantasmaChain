using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;

namespace Phantasma.Domain
{
    public interface IArchive
    {
        MerkleTree MerkleTree { get; }
        string Name { get; }
        Hash Hash { get; }
        BigInteger Size { get; }
        Timestamp Time{ get; }
        byte[] EncryptionKey { get; }

        bool IsOwner(Address address);
    }

}
