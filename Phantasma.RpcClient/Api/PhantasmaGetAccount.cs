using System;
using System.Threading.Tasks;
using Phantasma.RpcClient.Client;
using Phantasma.RpcClient.DTOs;
using IClient = Phantasma.RpcClient.Client.IClient;
using RpcRequest = Phantasma.RpcClient.Client.RpcRequest;

namespace Phantasma.RpcClient.Api
{
    public class PhantasmaGetAccount : RpcRequestResponseHandler<Account>
    {
        public PhantasmaGetAccount(IClient client) : base(client, ApiMethods.getAccount.ToString()) { }

        public Task<Account> SendRequestAsync(string address, object id = null)
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
