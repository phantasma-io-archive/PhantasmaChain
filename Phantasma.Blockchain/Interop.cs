using System.IO;
using Phantasma.Blockchain.Contracts;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Storage;
using Phantasma.Storage.Utils;

namespace Phantasma.Blockchain
{
    public struct PlatformInfo: IPlatform, ISerializable
    {
        public string Name { get; private set; }
        public string Symbol { get; private set; } // for fuel
        public Address Address { get; private set; }

        public PlatformInfo(string name, string symbol, Address address) : this()
        {
            Name = name;
            Symbol = symbol;
            Address = address;
        }

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
        public static string Seed = "";

        public static KeyPair GenerateInteropKeys(KeyPair genesisKeys, string platformName)
        {
            var temp = $"{platformName.ToUpper()}!{genesisKeys.ToWIF()}{Seed}";
            var privateKey = CryptoExtensions.Sha256(temp);
            var key = new KeyPair(privateKey);
            return key;
        }
    }
}
