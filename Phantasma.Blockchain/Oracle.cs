using Phantasma.Cryptography;
using Phantasma.Storage;
using Phantasma.Storage.Utils;
using System.Collections.Generic;
using System.IO;

namespace Phantasma.Blockchain
{
    public enum OracleFeedMode
    {
        First,
        Last,
        Max,
        Min,
        Average
    }

    public struct OracleFeed: ISerializable
    {
        public string Name;
        public Address Address;
        public OracleFeedMode Mode;

        public OracleFeed(string name, Address address, OracleFeedMode mode)
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
            Mode = (OracleFeedMode)reader.ReadByte();
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

    public struct OracleEntry
    {
        public readonly string URL;
        public readonly byte[] Content;

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
        private Dictionary<string, OracleEntry> _entries = new Dictionary<string, OracleEntry>();

        public IEnumerable<OracleEntry> Entries => _entries.Values;

        protected abstract byte[] PullData(string url);

        public byte[] Read(string url)
        {
            if (_entries.ContainsKey(url))
            {
                return _entries[url].Content;
            }

            var content = PullData(url);

            var entry = new OracleEntry(url, content);
            _entries[url] = entry;

            return content;
        }
    }
}
