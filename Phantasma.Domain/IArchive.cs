using System.Numerics;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Storage;

namespace Phantasma.Domain
{
    public enum ArchiveEncryptionMode
    {
        None,
        Private,
        Shared
    }

    public interface IArchiveEncryption: ISerializable
    {
        ArchiveEncryptionMode Mode { get; }

        string EncryptName(string name, PhantasmaKeys keys);
        string DecryptName(string name, PhantasmaKeys keys);
        byte[] Encrypt(byte[] chunk, PhantasmaKeys keys);
        byte[] Decrypt(byte[] chunk, PhantasmaKeys keys);
    }

    public interface IArchive
    {
        MerkleTree MerkleTree { get; }
        string Name { get; }
        Hash Hash { get; }
        BigInteger Size { get; }
        Timestamp Time{ get; }
        IArchiveEncryption Encryption { get; }
        bool IsOwner(Address address);
    }

}
