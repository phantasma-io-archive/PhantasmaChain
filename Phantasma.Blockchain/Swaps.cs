using Phantasma.Cryptography;
using Phantasma.Storage;
using Phantasma.Storage.Utils;
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
        public string sourcePlatform;
        public string sourceChain;
        public Hash sourceHash;

        public string destinationPlatform;
        public string destinationChain;
        public Hash destinationHash;

        public ChainSwap(string sourcePlatform, string sourceChain, Hash sourceHash, string destinationPlatform, string destinationChain, Hash destinationHash)
        {
            this.sourcePlatform = sourcePlatform;
            this.sourceChain = sourceChain;
            this.sourceHash = sourceHash;
            this.destinationPlatform = destinationPlatform;
            this.destinationChain = destinationChain;
            this.destinationHash = destinationHash;
        }

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteVarString(sourcePlatform);
            writer.WriteVarString(sourceChain);
            writer.WriteHash(sourceHash);
            writer.WriteVarString(destinationPlatform);
            writer.WriteVarString(destinationChain);
            writer.WriteHash(destinationHash);
        }

        public void UnserializeData(BinaryReader reader)
        {
            sourcePlatform = reader.ReadVarString();
            sourceChain = reader.ReadVarString();
            sourceHash = reader.ReadHash();
            destinationPlatform = reader.ReadVarString();
            destinationChain = reader.ReadVarString();
            destinationHash = reader.ReadHash();
        }
    }

    public interface ITokenSwapper
    {
        Hash SettleSwap(string sourcePlatform, string destPlatform, Hash sourceHash);
        ChainSwap[] GetPendingSwaps(Address address);
    }
}
