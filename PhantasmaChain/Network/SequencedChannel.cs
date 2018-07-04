using System.Collections.Generic;

namespace Phantasma.Network
{
    internal sealed class SequencedChannel
    {
        private ushort _localSequence;
        private ushort _remoteSequence;
        private readonly Queue<Packet> _outgoingPackets;
        private readonly NetPeer _peer;

        public SequencedChannel(NetPeer peer)
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

            _localSequence++;
            Packet packet;
            lock (_outgoingPackets)
            {
                packet = _outgoingPackets.Dequeue();
            }
            packet.Sequence = _localSequence;
            _peer.SendRawData(packet.RawData);
            _peer.Recycle(packet);
            return true;
        }

        public void ProcessPacket(Packet packet)
        {
            if (NetUtils.RelativeSequenceNumber(packet.Sequence, _remoteSequence) > 0)
            {
                _remoteSequence = packet.Sequence;
                _peer.AddIncomingPacket(packet);
            }
        }
    }
}
