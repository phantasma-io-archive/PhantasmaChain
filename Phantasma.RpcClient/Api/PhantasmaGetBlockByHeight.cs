using System;
using System.Threading.Tasks;
using Phantasma.RpcClient.Client;
using Phantasma.RpcClient.DTOs;

namespace Phantasma.RpcClient.Api
{
    public class PhantasmaGetBlockByHeight : RpcRequestResponseHandler<Block>
    {
        public PhantasmaGetBlockByHeight(IClient client) : base(client, ApiMethods.getBlockByHeight.ToString()) { }

        public Task<Block> SendRequestAsync(string chain, uint height, object id = null)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            return SendRequestAsync(id, chain, height);
        }

        public RpcRequest BuildRequest(string chain, uint height, object id = null)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            return BuildRequest(id, chain, height);
        }
    }
}
