using System.IO;
using Phantasma.Blockchain.Contracts;
using Phantasma.Cryptography;
using Phantasma.Storage;
using Phantasma.Storage.Utils;

namespace Phantasma.Blockchain
{
    public struct PlatformInfo: ISerializable
    {
        public string Name;
        public string Symbol; // for fuel
        public Address Address;
        //public flags;

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteVarString(Name);
            writer.WriteVarString(Symbol);
            writer.WriteAddress(Address);
        }

        public void UnserializeData(BinaryReader reader)
        {
            this.Name = reader.ReadVarString();
            this.Symbol = reader.ReadVarString();
            this.Address = reader.ReadAddress();
        }
    }

    public struct InteropBlock
    {
        public string Platform;
        public Hash Hash;
        public Hash[] Transactions;
    }

    public struct InteropTransaction
    {
        public string Platform;
        public Hash Hash;
        public Event[] Events;
    }

    public static class InteropUtils
    {
        public static KeyPair GenerateInteropKeys(KeyPair genesisKeys, string platformName)
        {
            var temp = platformName + "!" + genesisKeys.ToWIF();
            var privateKey = CryptoExtensions.Sha256(temp);
            var key = new KeyPair(privateKey);
            return key;
        }
    }
}
