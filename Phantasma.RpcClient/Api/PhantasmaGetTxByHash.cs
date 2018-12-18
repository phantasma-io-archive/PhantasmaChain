using System;
using System.Threading.Tasks;
using Phantasma.RpcClient.Client;
using Phantasma.RpcClient.DTOs;

namespace Phantasma.RpcClient.Api
{
    public class PhantasmaGetTxByHash : RpcRequestResponseHandler<Transaction>
    {
        public PhantasmaGetTxByHash(IClient client) : base(client, ApiMethods.getTransactionByHash.ToString()) { }

        public Task<Transaction> SendRequestAsync(string txHash, object id = null)
        {
            if (txHash == null) throw new ArgumentNullException(nameof(txHash));
            return SendRequestAsync(id, txHash);
        }

        public RpcRequest BuildRequest(string txHash, object id = null)
        {
            if (txHash == null) throw new ArgumentNullException(nameof(txHash));
            return BuildRequest(id, txHash);
        }
    }
}
