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
            var hash = CryptoUtils.RIPEMD160(data);
            this.Key = new ID(hash);
        }
    }
}
