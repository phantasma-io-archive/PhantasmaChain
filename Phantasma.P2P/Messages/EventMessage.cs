using System.IO;
using Phantasma.Cryptography;
using Phantasma.Domain;

namespace Phantasma.Network.P2P.Messages
{
    public sealed class EventMessage : Message
    {
        public readonly Event Event;

        public EventMessage(Address address, string host, Event evt) : base(Opcode.EVENT, address, host)
        {
            this.Event = evt;
        }

        internal static EventMessage FromReader(Address address, string host, BinaryReader reader)
        {
            var evt = Event.Unserialize(reader);
            return new EventMessage(address, host, evt);
        }

        protected override void OnSerialize(BinaryWriter writer)
        {
            Event.Serialize(writer);
        }
    }
}