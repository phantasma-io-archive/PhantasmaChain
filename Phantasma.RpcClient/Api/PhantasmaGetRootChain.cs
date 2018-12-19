using Phantasma.RpcClient.Client;
using Phantasma.RpcClient.DTOs;

namespace Phantasma.RpcClient.Api
{
    public class PhantasmaGetRootChain : GenericRpcRequestResponseHandlerNoParam<RootChainDto>
    {
        public PhantasmaGetRootChain(IClient client) : base(client, ApiMethods.getRootChain.ToString()) { }
    }
}
