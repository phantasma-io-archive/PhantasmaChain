using System.Threading.Tasks;
using Phantasma.RpcClient.Client;
using IClient = Phantasma.RpcClient.Client.IClient;

namespace Phantasma.RpcClient
{
    public class GenericRpcRequestResponseHandlerNoParam<TResponse> : RpcRequestResponseHandlerNoParam<TResponse>
    {
        public GenericRpcRequestResponseHandlerNoParam(IClient client, string methodName) : base(client, methodName)
        {
        }

        public new Task<TResponse> SendRequestAsync(object id = null)
        {
            return base.SendRequestAsync(id);
        }
    }
}
