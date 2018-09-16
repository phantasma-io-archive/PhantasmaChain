using System.IO;
using Phantasma.Cryptography;
using Phantasma.Core;
using Phantasma.Core.Utils;

namespace Phantasma.Network.P2P
{
    public struct PeerInfo
    {
        public readonly uint PeerID;
        public readonly Address Address;
        public readonly Endpoint EndPoint;

        public PeerInfo(uint peerID, Address address, Endpoint endpoint)
        {
            Throw.If(address == Address.Null, nameof(address));

            PeerID = peerID;
            Address = address;
            EndPoint = endpoint;
        }

        internal void Serialize(BinaryWriter writer)
        {
            writer.Write(PeerID);
            writer.WriteAddress(Address);
            this.EndPoint.Serialize(writer);
        }

        internal static PeerInfo Unserialize(BinaryReader reader)
        {
            var peerID = reader.ReadUInt32();
            var address = reader.ReadAddress();
            var endpoint = Endpoint.Unserialize(reader);
            return new PeerInfo(peerID, address, endpoint);
        }
    }
}
