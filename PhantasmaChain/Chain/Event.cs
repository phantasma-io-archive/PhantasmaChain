using System.IO;
using Phantasma.Utils;
using Phantasma.VM.Types;

namespace Phantasma.Blockchain
{
    public enum EventKind
    {
        Token,
        Withdraw,
        Deposit,
        Sale,
        Fill
    }

    public class Event
    {
        public readonly EventKind Kind;
        public readonly Address Address;
        public readonly byte[] Data;

        public Event(EventKind kind, Address address, byte[] data)
        {
            this.Kind = kind;
            this.Address = address;
            this.Data = data;
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)this.Kind);
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
