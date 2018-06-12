namespace Phantasma.Kademlia
{
    public class RpcError
    {
        public bool HasError { get { return TimeoutError || IDMismatchError || PeerError || ProtocolError; } }

        public bool TimeoutError { get; set; }
        public bool IDMismatchError { get; set; }
        public bool PeerError { get; set; }
        public bool ProtocolError { get; set; }
        public string PeerErrorMessage { get; set; }
        public string ProtocolErrorMessage { get; set; }
    }
}
