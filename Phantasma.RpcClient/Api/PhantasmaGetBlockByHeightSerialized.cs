using System;
using System.Threading.Tasks;
using Phantasma.RpcClient.Client;

namespace Phantasma.RpcClient.Api
{
    public class PhantasmaGetBlockByHeightSerialized : RpcRequestResponseHandler<string>
    {
        public PhantasmaGetBlockByHeightSerialized(IClient client) : base(client, ApiMethods.getBlockByHeight.ToString()) { }

        public Task<string> SendRequestAsync(string chain, int height, object id = null)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            return SendRequestAsync(id, chain, height, 1);
        }

        public RpcRequest BuildRequest(string chain, int height, object id = null)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            return BuildRequest(id, chain, height, 1);
        }
    }

}
