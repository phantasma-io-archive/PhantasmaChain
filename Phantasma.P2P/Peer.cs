using Phantasma.Blockchain;
using Phantasma.Cryptography;

namespace Phantasma.Network.P2P
{
    public abstract class Peer
    {
        public Address Address { get; private set; }
        public readonly Endpoint Endpoint;

        public Status Status { get; protected set; }

        public abstract void Send(Message msg);
        public abstract Message Receive();

        public Peer(Endpoint endpoint)
        {
            this.Endpoint = endpoint;
            this.Status = Status.Disconnected;
        }

        public void SetAddress(Address address)
        {
            this.Address = address;
            this.Status = address != Address.Null ? Status.Identified : Status.Anonymous;
        }
    }
}
