using System.IO;
using Phantasma.Cryptography;
using Phantasma.IO;

namespace Phantasma.Blockchain.Contracts
{
    public enum EventKind
    {
        ChainCreate,
        TokenCreate,
        TokenSend,
        TokenReceive,
        TokenMint,
        TokenBurn,
    }

    public class Event
    {
        public readonly EventKind Kind;
        public readonly Address Address;
        public readonly byte[] Data;

        public Event(EventKind kind, Address address, byte[] data = null)
        {
            this.Kind = kind;
            this.Address = address;
            this.Data = data;
        }

        public T GetValue<T>()
        {
            return (T)(object)Kind;
        }

        public void Serialize(BinaryWriter writer)
        {
            var n = (byte)(object)this.Kind;
            writer.Write(n);
            writer.WriteAddress(this.Address);
            writer.WriteByteArray(this.Data);
        }

        internal static Event Unserialize(BinaryReader reader)
        {
            var kind = (EventKind)reader.ReadByte();
            var address = reader.ReadAddress();
            var data = reader.ReadByteArray();
            return new Event(kind, address, data);
        }
    }
}
