using System.IO;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Storage;
using Phantasma.Storage.Utils;

namespace Phantasma.Blockchain.Swaps
{
    public struct ChainSwap: ISerializable
    {
        public Hash sourceHash;
        public string sourcePlatform;
        public Address sourceAddress;
        public Hash destinationHash;
        public string destinationPlatform;
        public Address destinationAddress;
        public string symbol;
        public BigInteger amount;
        public ChainSwapStatus status;

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteHash(sourceHash);
            writer.WriteVarString(sourcePlatform);
            writer.WriteAddress(sourceAddress);
            writer.WriteHash(destinationHash);
            writer.WriteVarString(destinationPlatform);
            writer.WriteVarString(symbol);
            writer.WriteBigInteger(amount);
            writer.Write((byte)status);
        }

        public void UnserializeData(BinaryReader reader)
        {
            this.sourceHash = reader.ReadHash();
            this.sourcePlatform = reader.ReadString();
            this.sourceAddress = reader.ReadAddress();
            this.destinationHash = reader.ReadHash();
            this.destinationPlatform = reader.ReadVarString();
            this.symbol = reader.ReadVarString();
            this.amount = reader.ReadBigInteger();
            this.status = (ChainSwapStatus)reader.ReadByte();
        }
    }
}
