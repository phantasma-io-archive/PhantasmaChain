using Phantasma.RpcClient.Client;
using Phantasma.RpcClient.DTOs;

namespace Phantasma.RpcClient.Api
{
    public class PhantasmaGetRootChain : GenericRpcRequestResponseHandlerNoParam<RootChain>
    {
        public PhantasmaGetRootChain(IClient client) : base(client, ApiMethods.getRootChain.ToString()) { }
    }
}
