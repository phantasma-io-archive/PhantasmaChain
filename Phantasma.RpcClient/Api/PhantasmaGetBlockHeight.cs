using System;
using System.Threading.Tasks;
using Phantasma.RpcClient.Client;

namespace Phantasma.RpcClient.Api
{
    public class PhantasmaGetBlockHeight : RpcRequestResponseHandler<int>
    {
        public PhantasmaGetBlockHeight(IClient client) : base(client, ApiMethods.getBlockHeight.ToString()) { }

        public Task<int> SendRequestAsync(string chain, object id = null)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            return SendRequestAsync(id, chain);
        }

        public RpcRequest BuildRequest(string chain, object id = null)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            return BuildRequest(id, chain);
        }
    }
}
