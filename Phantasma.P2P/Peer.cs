using Phantasma.Blockchain;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using System;
using System.Collections.Generic;

namespace Phantasma.Network.P2P
{
    // TODO those are unused for now
    [Flags]
    public enum PeerCaps
    {
        None = 0,
        Sync = 0x1,
        Mempool = 0x2,
        Archive = 0x4,
        Relay = 0x8,
        Events = 0x10,
        RPC = 0x20,
        REST = 0x40,
    }

    public struct PeerPort
    {
        public readonly string Name;
        public readonly int Port;

        public PeerPort(string name, int port)
        {
            Name = name;
            Port = port;
        }
    }

    public abstract class Peer
    {
        public Address Address { get; private set; }
        public string Version { get; private set; }
        public Endpoint Endpoint { get; private set; }

        public PeerCaps Capabilities { get; set; }

        public Status Status { get; protected set; }

        public BigInteger MinimumFee { get; set; }
        public int MinimumPoW { get; set; }

        public List<PeerPort> Ports { get; set; }

        public abstract void Send(Message msg);
        public abstract Message Receive();

        public Peer(Endpoint endpoint)
        {
            this.Endpoint = endpoint;
            this.Status = Status.Disconnected;
            this.MinimumFee = 1;
            this.MinimumPoW = 0;
        }

        public void SetAddress(Address address)
        {
            this.Address = address;
            this.Status = address.IsNull ? Status.Anonymous : Status.Identified;
        }

        public void UpdateEndpoint(Endpoint endpoint)
        {
            if (endpoint.Protocol != this.Endpoint.Protocol)
            {
                throw new NodeException("Can't update protocol of peer");
            }

            this.Endpoint = endpoint;
        }
    }
}
