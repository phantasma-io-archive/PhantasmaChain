using Phantasma.Blockchain;
using Phantasma.Cryptography;
using System;

namespace Phantasma.Network.P2P
{
    // TODO those are unused for now
    [Flags]
    public enum PeerCaps
    {
        Chain = 0x1,
        Archive = 0x2,
        Relay = 0x4,
        Event = 0x8,
    }

    public abstract class Peer
    {
        public Address Address { get; private set; }
        public readonly Endpoint Endpoint;

        public PeerCaps Capabilities { get; private set; }

        public Status Status { get; protected set; }

        public abstract void Send(Message msg);
        public abstract Message Receive();

        public Peer(Endpoint endpoint)
        {
            this.Endpoint = endpoint;
            this.Status = Status.Disconnected;
        }

        public void SetCaps(PeerCaps caps)
        {
            this.Capabilities = caps;
        }

        public void SetAddress(Address address)
        {
            this.Address = address;
            this.Status = address != Address.Null ? Status.Identified : Status.Anonymous;
        }
    }
}
