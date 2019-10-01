using Phantasma.Cryptography;
using Phantasma.Storage;
using System.IO;

namespace Phantasma.Blockchain
{
    public enum BrokerResult
    {
        Ready,
        Skip,
        Error
    }

    public struct ChainSwap: ISerializable
    {
        public Hash sourceHash;
        public Hash destinationHash;

        public ChainSwap(Hash sourceHash, Hash destinationHash)
        {
            this.sourceHash = sourceHash;
            this.destinationHash = destinationHash;
        }

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteHash(sourceHash);
            writer.WriteHash(destinationHash);
        }

        public void UnserializeData(BinaryReader reader)
        {
            sourceHash = reader.ReadHash();
            destinationHash = reader.ReadHash();
        }
    }
}
