using System;
using System.Threading.Tasks;
using Phantasma.RpcClient.Client;
using Phantasma.RpcClient.DTOs;

namespace Phantasma.RpcClient.Api
{
    public class PhantasmaGetTxByBlockHashAndIndex : RpcRequestResponseHandler<TransactionDto>
    {
        public PhantasmaGetTxByBlockHashAndIndex(IClient client) : base(client, ApiMethods.getTransactionByBlockHashAndIndex.ToString()) { }

        public Task<TransactionDto> SendRequestAsync(string blockHash, int index, object id = null)
        {
            if (blockHash == null) throw new ArgumentNullException(nameof(blockHash));
            return SendRequestAsync(id, blockHash, index);
        }

        public RpcRequest BuildRequest(string blockHash, int index, object id = null)
        {
            if (blockHash == null) throw new ArgumentNullException(nameof(blockHash));
            return BuildRequest(id, blockHash, index);
        }
    }
}
