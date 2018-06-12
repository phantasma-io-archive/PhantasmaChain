using System.Collections.Generic;
using System.Numerics;

namespace Phantasma.Kademlia.Protocols
{
    public abstract class BaseResponse
    {
        public BigInteger RandomID { get; set; }
    }

    public class ErrorResponse : BaseResponse
    {
        public string ErrorMessage { get; set; }
    }

    public class ContactResponse
    {
        public BigInteger Contact { get; set; }
        public object Protocol { get; set; }
        public string ProtocolName { get; set; }
    }

    public class FindNodeResponse : BaseResponse
    {
        public List<ContactResponse> Contacts { get; set; }
    }

    public class FindValueResponse : BaseResponse
    {
        public List<ContactResponse> Contacts { get; set; }
        public string Value { get; set; }
    }

    public class PingResponse : BaseResponse { }

    public class StoreResponse : BaseResponse { }

}
