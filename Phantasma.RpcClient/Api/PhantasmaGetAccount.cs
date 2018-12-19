using System;
using System.Threading.Tasks;
using Phantasma.RpcClient.Client;
using Phantasma.RpcClient.DTOs;

namespace Phantasma.RpcClient.Api
{
    public class PhantasmaGetAccount : RpcRequestResponseHandler<AccountDto>
    {
        public PhantasmaGetAccount(IClient client) : base(client, ApiMethods.getAccount.ToString()) { }

        public Task<AccountDto> SendRequestAsync(string address, object id = null)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));
            return SendRequestAsync(id, address);
        }

        public RpcRequest BuildRequest(string address, object id = null)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));
            return BuildRequest(id, address);
        }
    }
}
