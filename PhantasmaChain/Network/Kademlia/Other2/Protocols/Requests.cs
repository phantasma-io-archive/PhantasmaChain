using System.Numerics;

using Phantasma.Kademlia.Common;

namespace Phantasma.Kademlia.Protocols
{
    public abstract class BaseRequest
    {
        public object Protocol { get; set; }
        public string ProtocolName { get; set; }
        public BigInteger RandomID { get; set; }
        public BigInteger Sender { get; set; }

        public BaseRequest()
        {
            RandomID = ID.RandomID.Value;
        }
    }

    public abstract class BaseSubnetRequest : BaseRequest
    {
        public int Subnet { get; set; }
    }

    public class FindNodeRequest : BaseSubnetRequest
    {
        public BigInteger Key { get; set; }
    }

    public class FindValueRequest : BaseSubnetRequest
    {
        public BigInteger Key { get; set; }
    }

    public class PingRequest : BaseSubnetRequest { }

    public class StoreRequest : BaseSubnetRequest
    {
        public BigInteger Key { get; set; }
        public string Value { get; set; }
        public bool IsCached { get; set; }
        public int ExpirationTimeSec { get; set; }
    }
}
