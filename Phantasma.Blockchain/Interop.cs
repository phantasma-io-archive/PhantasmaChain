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
        public Address InteropAddress { get; private set; }
        public string ExternalAddress { get; private set; }
        public Address ChainAddress { get; private set; }

        public PlatformInfo(string name, string symbol, Address interopAddress, string externalAddress, Address chainAddress) : this()
        {
            Name = name;
            Symbol = symbol;
            InteropAddress = interopAddress;
            ExternalAddress = externalAddress;
            ChainAddress = chainAddress;
        }

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteVarString(Name);
            writer.WriteVarString(Symbol);
            writer.WriteAddress(InteropAddress);
            writer.WriteVarString(ExternalAddress);
            writer.WriteAddress(ChainAddress);
        }

        public void UnserializeData(BinaryReader reader)
        {
            this.Name = reader.ReadVarString();
            this.Symbol = reader.ReadVarString();
            this.InteropAddress = reader.ReadAddress();
            this.ExternalAddress = reader.ReadVarString();
            this.ChainAddress = reader.ReadAddress();
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
