using Phantasma.Utils;
using System.IO;

namespace Phantasma.Core
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
        public readonly byte[] PublicKey;
        public readonly byte[] Data;

        public Event(EventKind kind, byte[] publicKey, byte[] data)
        {
            this.Kind = kind;
            this.PublicKey = publicKey;
            this.Data = data;
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)this.Kind);
            writer.WriteByteArray(this.PublicKey);
            writer.WriteByteArray(this.Data);
        }

        internal static Event Unserialize(BinaryReader reader)
        {
            var kind = (EventKind)reader.ReadByte();
            var pubKey = reader.ReadByteArray();
            var data = reader.ReadByteArray();
            return new Event(kind, pubKey, data);
        }
    }
}
