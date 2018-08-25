using Phantasma.Cryptography;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Phantasma.Network.Messages
{
    internal class PeerListMessage : Message
    {
        private PeerInfo[] _peers;
        public IEnumerable<PeerInfo> Peers => _peers;

        public PeerListMessage(Address address, IEnumerable<PeerInfo> peers) : base(Opcode.PEER_List, address)
        {
            this._peers = peers.ToArray();
        }

        internal static PeerListMessage FromReader(Address address, BinaryReader reader)
        {
            var peerCount = reader.ReadUInt32();
            var peers = new PeerInfo[peerCount];
            for (int i = 0; i < peerCount; i++ )
            {
                peers[i] = PeerInfo.Unserialize(reader);
            }

            return new PeerListMessage(address, peers);
        }
    }
}