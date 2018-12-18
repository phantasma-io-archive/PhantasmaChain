using System;
using System.Threading.Tasks;
using Phantasma.RpcClient.Client;
using Phantasma.RpcClient.DTOs;
using IClient = Phantasma.RpcClient.Client.IClient;
using RpcRequest = Phantasma.RpcClient.Client.RpcRequest;

namespace Phantasma.RpcClient.Api
{
    public class PhantasmaSendRawTx : RpcRequestResponseHandler<SendRawTx>
    {
        public PhantasmaSendRawTx(IClient client) : base(client, ApiMethods.sendRawTransaction.ToString()) { }

        public Task<SendRawTx> SendRequestAsync(string signedTx, object id = null)
        {
            if (signedTx == null) throw new ArgumentNullException(nameof(signedTx));
            return SendRequestAsync(id, signedTx);
        }

        public RpcRequest BuildRequest(string signedTx, object id = null)
        {
            if (signedTx == null) throw new ArgumentNullException(nameof(signedTx));
            return BuildRequest(id, signedTx);
        }
    }
}
