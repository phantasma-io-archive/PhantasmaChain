using System.IO;
using Phantasma.IO;

namespace Phantasma.Network.P2P
{
    internal struct PeerVote
    {
        public readonly uint PeerID;
        public readonly byte[] Message;
        public readonly byte[] Signature;

        public PeerVote(uint peerID, byte[] message, byte[] signature)
        {
            PeerID = peerID;
            Message = message;
            Signature = signature;
        }

        internal void Serialize(BinaryWriter writer) {
            writer.Write(PeerID);
            writer.WriteByteArray(Message);
            writer.WriteByteArray(Signature);
        }

        internal static PeerVote Unserialize(BinaryReader reader)
        {
            var peerID = reader.ReadUInt32();
            var msg = reader.ReadByteArray();
            var sig = reader.ReadByteArray();
            return new PeerVote(peerID, msg, sig); 
        }
    }
}
