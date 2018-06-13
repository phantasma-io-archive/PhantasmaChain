using Phantasma.Utils;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.Network.Kademlia
{
    public class KademliaResource
    {
        public readonly ID Key;
        public readonly byte[] Data;

        public KademliaResource(IEnumerable<byte> data)
        {
            this.Data = data.ToArray();
            this.Key = ID.FromBytes(this.Data);
        }
    }
}
