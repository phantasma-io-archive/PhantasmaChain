using System.IO;
using Phantasma.Core;
using Phantasma.Utils;

namespace Phantasma.Network
{
    public struct PeerInfo
    {
        public readonly uint PeerID;
        public readonly byte[] PublicKey;
        public readonly Endpoint EndPoint;

        public PeerInfo(uint peerID, byte[] publicKey, Endpoint endpoint)
        {
            Throw.IfNull(publicKey, nameof(publicKey));
            Throw.IfNot(publicKey.Length == KeyPair.PublicKeyLength, nameof(publicKey));

            PeerID = peerID;
            PublicKey = publicKey;
            EndPoint = endpoint;
        }

        internal void Serialize(BinaryWriter writer)
        {
            writer.Write(PeerID);
            writer.Write(PublicKey);
            this.EndPoint.Serialize(writer);
        }

        internal static PeerInfo Unserialize(BinaryReader reader)
        {
            var peerID = reader.ReadUInt32();
            var publicKey = reader.ReadBytes(KeyPair.PublicKeyLength);
            var endpoint = Endpoint.Unserialize(reader);
            return new PeerInfo(peerID, publicKey, endpoint);
        }
    }
}
