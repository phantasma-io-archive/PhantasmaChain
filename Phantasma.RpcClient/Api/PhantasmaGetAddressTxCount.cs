using System;
using System.Threading.Tasks;
using Phantasma.RpcClient.Client;

namespace Phantasma.RpcClient.Api
{
    public class PhantasmaGetAddressTxCount : RpcRequestResponseHandler<int>
    {
        public PhantasmaGetAddressTxCount(IClient client) : base(client, ApiMethods.getAddressTxCount.ToString()) { }
        public Task<int> SendRequestAsync(string address, string chain = null, object id = null)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));
            if (string.IsNullOrEmpty(chain))
            {
                return SendRequestAsync(id, address);
            }
            return SendRequestAsync(id, address, chain);
        }

        public RpcRequest BuildRequest(string address, string chain = null, object id = null)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));
            if (string.IsNullOrEmpty(chain))
            {
                return BuildRequest(id, address);
            }
            return BuildRequest(id, address, chain);
        }
    }
}
