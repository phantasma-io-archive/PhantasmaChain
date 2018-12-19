using System;
using System.Threading.Tasks;
using Phantasma.RpcClient.Client;
using Phantasma.RpcClient.DTOs;

namespace Phantasma.RpcClient.Api
{
    public class PhantasmaGetTxConfirmations : RpcRequestResponseHandler<TxConfirmationDto>
    {
        public PhantasmaGetTxConfirmations(IClient client) : base(client, ApiMethods.getConfirmations.ToString()) { }

        public Task<TxConfirmationDto> SendRequestAsync(string hash, object id = null)
        {
            if (hash == null) throw new ArgumentNullException(nameof(hash));
            return SendRequestAsync(id, hash);
        }

        public RpcRequest BuildRequest(string hash, object id = null)
        {
            if (hash == null) throw new ArgumentNullException(nameof(hash));
            return BuildRequest(id, hash);
        }
    }
}
