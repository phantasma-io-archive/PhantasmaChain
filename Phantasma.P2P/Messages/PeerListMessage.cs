using Phantasma.Blockchain;
using Phantasma.Cryptography;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Phantasma.Network.P2P.Messages
{
    internal class PeerListMessage : Message
    {
        private PeerInfo[] _peers;
        public IEnumerable<PeerInfo> Peers => _peers;

        public PeerListMessage(Nexus nexus, Address address, IEnumerable<PeerInfo> peers) : base(nexus, Opcode.PEER_List, address)
        {
            this._peers = peers.ToArray();
        }

        internal static PeerListMessage FromReader(Nexus nexus, Address address, BinaryReader reader)
        {
            var peerCount = reader.ReadUInt32();
            var peers = new PeerInfo[peerCount];
            for (int i = 0; i < peerCount; i++ )
            {
                peers[i] = PeerInfo.Unserialize(reader);
            }

            return new PeerListMessage(nexus, address, peers);
        }

        protected override void OnSerialize(BinaryWriter writer)
        {
            throw new System.NotImplementedException();
        }
    }
}