using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Phantasma.RpcClient.Client;
using Phantasma.RpcClient.DTOs;

namespace Phantasma.RpcClient.Api
{
    public class PhantasmaGetTokenTransfers : RpcRequestResponseHandler<List<TransactionDto>>
    {
        public PhantasmaGetTokenTransfers(IClient client) : base(client, ApiMethods.getTokenTransfers.ToString()) { }

        public Task<List<TransactionDto>> SendRequestAsync(string tokenSymbol, int amount, object id = null)
        {
            if (tokenSymbol == null) throw new ArgumentNullException(nameof(tokenSymbol));
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            return SendRequestAsync(id, tokenSymbol, amount);
        }

        public RpcRequest BuildRequest(string tokenSymbol, int amount, object id = null)
        {
            if (tokenSymbol == null) throw new ArgumentNullException(nameof(tokenSymbol));
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));

            return BuildRequest(id, tokenSymbol);
        }

    }
}
