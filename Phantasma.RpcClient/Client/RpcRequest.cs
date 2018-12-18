    namespace Phantasma.RpcClient.Client
{
    public class RpcRequest
    {
        public RpcRequest(object id, string method, params object[] parameterList)
        {
            Id = id;
            Method = method;
            RawParameters = parameterList;
        }

        public object Id { get; }
        public string Method { get; }
        public object[] RawParameters { get; }
    }
}
