using System.IO;
using Phantasma.Blockchain.Contracts;
using Phantasma.Cryptography;
using Phantasma.Storage.Utils;

namespace Phantasma.Network.P2P.Messages
{
    public sealed class EventMessage : Message
    {
        public readonly Event Event;

        public EventMessage(Address address, Event evt) : base(Opcode.EVENT, address)
        {
            this.Event = evt;
        }

        internal static EventMessage FromReader(Address address, BinaryReader reader)
        {
            var evt = Event.Unserialize(reader);
            return new EventMessage(address, evt);
        }

        protected override void OnSerialize(BinaryWriter writer)
        {
            Event.Serialize(writer);
        }
    }
}