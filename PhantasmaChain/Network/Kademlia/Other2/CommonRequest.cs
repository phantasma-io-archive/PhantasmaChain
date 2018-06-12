using System.Numerics;

namespace Phantasma.Kademlia
{
    /// <summary>
    /// For passing to Node handlers with common parameters.
    /// </summary>
    public class CommonRequest
    {
        public object Protocol { get; set; }
        public string ProtocolName { get; set; }
        public BigInteger RandomID { get; set; }
        public BigInteger Sender { get; set; }
        public BigInteger Key { get; set; }
        public string Value { get; set; }
        public bool IsCached { get; set; }
        public int ExpirationTimeSec { get; set; }
    }
}
