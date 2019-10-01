using System.Collections.Generic;
using System.IO;
using System.Linq;
using Phantasma.Blockchain.Contracts;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Storage;
using Phantasma.Storage.Utils;

namespace Phantasma.Blockchain
{
    public struct PlatformInfo : IPlatform, ISerializable
    {
        public string Name { get; private set; }
        public string Symbol { get; private set; } // for fuel
        public string ExternalAddress { get; private set; }
        public Address ChainAddress { get; private set; }
        public Address[] InteropAddresses { get; private set; }

        public PlatformInfo(string name, string symbol, string externalAddress, Address chainAddress, IEnumerable<Address> interopAddresses) : this()
        {
            Name = name;
            Symbol = symbol;
            ExternalAddress = externalAddress;
            ChainAddress = chainAddress;
            InteropAddresses = interopAddresses.ToArray();
        }

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteVarString(Name);
            writer.WriteVarString(Symbol);
            writer.WriteVarString(ExternalAddress);
            writer.WriteAddress(ChainAddress);
            writer.WriteVarInt(InteropAddresses.Length);
            foreach (var address in InteropAddresses)
            {
                writer.WriteAddress(address);
            }
        }

        public void UnserializeData(BinaryReader reader)
        {
            this.Name = reader.ReadVarString();
            this.Symbol = reader.ReadVarString();
            this.ExternalAddress = reader.ReadVarString();
            this.ChainAddress = reader.ReadAddress();
            var interopCount = (int)reader.ReadVarInt();
            this.InteropAddresses = new Address[interopCount];
            for (int i = 0; i < interopCount; i++)
            {
                InteropAddresses[i] = reader.ReadAddress();
            }
        }
    }

    public static class InteropUtils
    {
        public static string Seed = "";

        public static PhantasmaKeys GenerateInteropKeys(PhantasmaKeys genesisKeys, string platformName)
        {
            var temp = $"{platformName.ToUpper()}!{genesisKeys.ToWIF()}{Seed}";
            var privateKey = CryptoExtensions.Sha256(temp);
            var key = new PhantasmaKeys(privateKey);
            return key;
        }
    }
}
