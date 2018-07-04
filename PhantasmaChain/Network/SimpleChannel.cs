using System.Collections.Generic;

namespace Phantasma.Network
{
    internal sealed class SimpleChannel
    {
        private readonly Queue<Packet> _outgoingPackets;
        private readonly NetPeer _peer;

        public SimpleChannel(NetPeer peer)
        {
            _outgoingPackets = new Queue<Packet>();
            _peer = peer;
        }

        public void AddToQueue(Packet packet)
        {
            lock (_outgoingPackets)
            {
                _outgoingPackets.Enqueue(packet);
            }
        }

        public bool SendNextPacket()
        {
            if (_outgoingPackets.Count == 0)
                return false;

            Packet packet;
            lock (_outgoingPackets)
            {
                packet = _outgoingPackets.Dequeue();
            }
            _peer.SendRawData(packet.RawData);
            _peer.Recycle(packet);
            return true;
        }
    }
}
