using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Phantasma.Network
{
    internal class PeerListMessage : Message
    {
        private PeerInfo[] _peers;
        public IEnumerable<PeerInfo> Peers => _peers;

        public PeerListMessage(IEnumerable<PeerInfo> peers)
        {
            this._peers = peers.ToArray();
        }

        internal static PeerListMessage FromReader(BinaryReader reader)
        {
            var peerCount = reader.ReadUInt32();
            var peers = new PeerInfo[peerCount];
            for (int i = 0; i < peerCount; i++ )
            {
                peers[i] = PeerInfo.Unserialize(reader);
            }

            return new PeerListMessage(peers);
        }
    }
}