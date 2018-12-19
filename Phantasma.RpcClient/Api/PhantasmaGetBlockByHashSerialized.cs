using System;
using System.Threading.Tasks;
using Phantasma.RpcClient.Client;

namespace Phantasma.RpcClient.Api
{
    public class PhantasmaGetBlockByHashSerialized : RpcRequestResponseHandler<string>
    {
        public PhantasmaGetBlockByHashSerialized(IClient client) : base(client, ApiMethods.getBlockByHash.ToString()) { }

        public Task<string> SendRequestAsync(string hash, object id = null)
        {
            if (hash == null) throw new ArgumentNullException(nameof(hash));
            return SendRequestAsync(id, hash,  1);
        }

        public RpcRequest BuildRequest(string hash, object id = null)
        {
            if (hash == null) throw new ArgumentNullException(nameof(hash));
            return BuildRequest(id, hash, 1);
        }
    }
}
