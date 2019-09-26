using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.Storage;
using Phantasma.Storage.Utils;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Phantasma.Blockchain
{
    public struct OracleFeed: IFeed, ISerializable
    {
        public string Name { get; private set; }
        public Address Address { get; private set; }
        public FeedMode Mode { get; private set; }

        public OracleFeed(string name, Address address, FeedMode mode)
        {
            Name = name;
            Address = address;
            Mode = mode;
        }

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteVarString(Name);
            writer.WriteAddress(Address);
            writer.Write((byte)Mode);
        }

        public void UnserializeData(BinaryReader reader)
        {
            Name = reader.ReadVarString();
            Address = reader.ReadAddress();
            Mode = (FeedMode)reader.ReadByte();
        }

        public byte[] ToByteArray()
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    SerializeData(writer);
                }

                return stream.ToArray();
            }
        }

        public static OracleFeed Unserialize(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                using (var reader = new BinaryReader(stream))
                {
                    var entity = new OracleFeed();
                    entity.UnserializeData(reader);
                    return entity;
                }
            }
        }
    }

    public struct OracleEntry: IOracleEntry
    {
        public string URL { get; private set; }
        public byte[] Content { get; private set; }

        public OracleEntry(string uRL, byte[] content)
        {
            URL = uRL;
            Content = content;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is OracleEntry))
            {
                return false;
            }

            var entry = (OracleEntry)obj;
            return URL == entry.URL &&
                   EqualityComparer<byte[]>.Default.Equals(Content, entry.Content);
        }

        public override int GetHashCode()
        {
            var hashCode = 1993480784;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(URL);
            hashCode = hashCode * -1521134295 + EqualityComparer<byte[]>.Default.GetHashCode(Content);
            return hashCode;
        }
    }

    public abstract class OracleReader
    {
        public const string interopTag = "interop://";
        public const string priceTag = "price://";

        private Dictionary<string, OracleEntry> _entries = new Dictionary<string, OracleEntry>();

        public IEnumerable<OracleEntry> Entries => _entries.Values;

        protected abstract byte[] PullData(string url);
        protected abstract decimal PullPrice(string baseSymbol, string quoteSymbol);
        protected abstract InteropBlock PullPlatformBlock(string platformName, Hash hash);
        protected abstract InteropTransaction PullPlatformTransaction(string platformName, Hash hash);

        public readonly Nexus Nexus;

        public OracleReader(Nexus nexus)
        {
            this.Nexus = nexus;
        }

        public byte[] Read(string url)
        {
            if (_entries.ContainsKey(url))
            {
                return _entries[url].Content;
            }

            byte[] content;

            if (url.StartsWith(interopTag))
            {
                url = url.Substring(interopTag.Length);
                var args = url.Split('/');

                var platformName = args[0];
                if (Nexus.PlatformExists(platformName))
                {
                    args = args.Skip(1).ToArray();
                    return ReadChainOracle(platformName, args);
                }
                else
                { 
                    throw new OracleException("invalid oracle platform: " + platformName);
                }
            }
            else
            if (url.StartsWith(priceTag))
            {
                url = url.Substring(priceTag.Length);
                var symbols = url.Split('/');

                if (symbols.Length < 1 || symbols.Length > 2)
                {
                    throw new OracleException("invalid oracle price request");
                }

                var baseSymbol = symbols[0];
                var quoteSymbol = symbols.Length > 1 ? symbols[1] : DomainSettings.FiatTokenSymbol;

                if (!Nexus.TokenExists(baseSymbol))
                {
                    throw new OracleException("unknown token: " + baseSymbol);
                }

                if (!Nexus.TokenExists(quoteSymbol))
                {
                    throw new OracleException("unknown token: " + quoteSymbol);
                }

                var tokenInfo = Nexus.GetTokenInfo(quoteSymbol);

                var price = PullPrice(baseSymbol, quoteSymbol);
                var val = UnitConversion.ToBigInteger(price, tokenInfo.Decimals);
                return val.ToUnsignedByteArray();
            }
            else
            {
                content = PullData(url);
            }
        
            var entry = new OracleEntry(url, content);
            _entries[url] = entry;

            return content;
        }

        public byte[] ReadChainOracle(string platformName, string[] input)
        {
            if (input == null || input.Length != 2)
            {
                throw new OracleException("missing oracle input");
            }

            var cmd = input[0].ToLower();
            switch (cmd)
            {
                case "tx":
                case "transaction":
                    {
                        Hash hash;
                        if (Hash.TryParse(input[1], out hash))
                        {
                            var tx = PullPlatformTransaction(platformName, hash);
                            return Serialization.Serialize(tx);
                        }
                        else
                        {
                            throw new OracleException("invalid transaction hash");
                        }
                    }

                case "block":
                    {
                        Hash hash;
                        if (Hash.TryParse(input[1], out hash))
                        {
                            var block = PullPlatformBlock(platformName, hash);
                            return Serialization.Serialize(block);
                        }
                        else
                        {
                            throw new OracleException("invalid block hash");
                        }
                    }

                default:
                    throw new OracleException("unknown platform oracle");
            }
        }
    }
}
