using System.Collections.Generic;
using Phantasma.RpcClient.Client;
using Phantasma.RpcClient.DTOs;

namespace Phantasma.RpcClient.Api
{
    public class PhantasmaGetChains : GenericRpcRequestResponseHandlerNoParam<IList<Chain>>
    {
        public PhantasmaGetChains(IClient client) : base(client, ApiMethods.getChains.ToString()) { }
    }
}
