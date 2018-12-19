using Phantasma.RpcClient.Client;
using Phantasma.RpcClient.DTOs;

namespace Phantasma.RpcClient.Api
{
    public class PhantasmaGetTokens : GenericRpcRequestResponseHandlerNoParam<TokenList>
    {
        public PhantasmaGetTokens(IClient client) : base(client, ApiMethods.getTokens.ToString()) { }
    }
}
