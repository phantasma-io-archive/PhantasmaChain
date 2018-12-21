using System;
using System.Threading.Tasks;
using Phantasma.RpcClient.Client;

namespace Phantasma.RpcClient.Api
{
    public class PhantasmaGetTokenTransferCount : RpcRequestResponseHandler<int>
    {
        public PhantasmaGetTokenTransferCount(IClient client) : base(client, ApiMethods.getTokenTransferCount.ToString()) { }

        public Task<int> SendRequestAsync(string tokenSymbol, object id = null)
        {
            if (tokenSymbol == null) throw new ArgumentNullException(nameof(tokenSymbol));

            return SendRequestAsync(id, tokenSymbol);
        }

        public RpcRequest BuildRequest(string tokenSymbol, object id = null)
        {
            if (tokenSymbol == null) throw new ArgumentNullException(nameof(tokenSymbol));

            return BuildRequest(id, tokenSymbol);
        }
    }
}
