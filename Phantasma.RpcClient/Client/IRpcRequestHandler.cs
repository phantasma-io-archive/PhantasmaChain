namespace Phantasma.RpcClient.Client
{
    public interface IRpcRequestHandler
    {
        string MethodName { get; }
        IClient Client { get; }
    }
}