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

        public Event(EventKind kind, byte[] publicKey)
        {
            this.Kind = kind;
            this.PublicKey = publicKey;
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)this.Kind);
            writer.WriteByteArray(this.PublicKey);
            //writer.WriteByteArray(this.Data);
        }
    }
}
