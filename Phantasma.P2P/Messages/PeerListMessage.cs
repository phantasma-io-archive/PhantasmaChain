using Phantasma.Cryptography;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Phantasma.Network.P2P.Messages
{
    public class PeerListMessage : Message
    {
        private Endpoint[] _peers;
        public IEnumerable<Endpoint> Peers => _peers;

        public PeerListMessage(Address address, IEnumerable<Endpoint> peers) : base(Opcode.PEER_List, address)
        {
            this._peers = peers.ToArray();
        }

        internal static PeerListMessage FromReader(Address address, BinaryReader reader)
        {
            var peerCount = reader.ReadUInt32();
            var peers = new Endpoint[peerCount];
            for (int i = 0; i < peerCount; i++ )
            {
                peers[i] = Endpoint.Unserialize(reader);
            }

            return new PeerListMessage(address, peers);
        }

        protected override void OnSerialize(BinaryWriter writer)
        {
            throw new System.NotImplementedException();
        }

        public override IEnumerable<string> GetDescription()
        {
            return Peers.Select(x => x.ToString());
        }
    }
}