using Phantasma.RpcClient.Client;
using Phantasma.RpcClient.DTOs;

namespace Phantasma.RpcClient.Api
{
    public class PhantasmaGetApplications : GenericRpcRequestResponseHandlerNoParam<AppList>
    {
        public PhantasmaGetApplications(IClient client) : base(client, ApiMethods.getApps.ToString()) { }
    }
}
