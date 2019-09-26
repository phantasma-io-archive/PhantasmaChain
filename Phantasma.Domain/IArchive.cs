using Phantasma.Cryptography;
using Phantasma.Numerics;
using System;

namespace Phantasma.Domain
{
    [Flags]
    public enum ArchiveFlags
    {
        None = 0x0,
        Compressed = 0x1,
        Encrypted = 0x2,
    }

    // TODO support this
    public struct ArchiveMetadata
    {
        public readonly string Key;
        public readonly string Value;

        public ArchiveMetadata(string key, string value)
        {
            Key = key;
            Value = value;
        }
    }

    public interface IArchive
    {
        ArchiveFlags Flags { get;  }
        MerkleTree MerkleTree { get; }
        BigInteger Size { get; }
        byte[] Key { get; }
    }

}
