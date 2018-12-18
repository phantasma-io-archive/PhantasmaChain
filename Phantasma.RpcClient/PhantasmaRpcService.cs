using Phantasma.RpcClient.Api;
using Phantasma.RpcClient.Client;
using Phantasma.RpcClient.Interfaces;

namespace Phantasma.RpcClient
{
    public class PhantasmaRpcService : RpcClientWrapper, IPhantasmaRpcService
    {
        public PhantasmaRpcService(IClient client) : base(client)
        {
            GetAccount = new PhantasmaGetAccount(client);
            GetAccountTxs = new PhantasmaGetAccountTxs(client);
            GetApplications = new PhantasmaGetApplications(client);
            GetBlockByHash = new PhantasmaGetBlockByHash(client);
            GetBlockByHeight = new PhantasmaGetBlockByHeight(client);
            GetBlockHeight = new PhantasmaGetBlockHeight(client);
            GetBlockTxCountByHash = new PhantasmaGetBlockTxCountByHash(client);
            GetChains = new PhantasmaGetChains(client);
            GetRootChain = new PhantasmaGetRootChain(client);
            GetTokens = new PhantasmaGetTokens(client);
            GetTxByBlockHashAndIndex = new PhantasmaGetTxByBlockHashAndIndex(client);
            GetTxByHash = new PhantasmaGetTxByHash(client);
            GetTxConfirmations = new PhantasmaGetTxConfirmations(client);
            SendRawTx = new PhantasmaSendRawTx(client);
        }

        public PhantasmaGetAccount GetAccount { get; }
        public PhantasmaGetAccountTxs GetAccountTxs { get; }
        public PhantasmaGetApplications GetApplications { get; }
        public PhantasmaGetBlockByHash GetBlockByHash { get; }
        public PhantasmaGetBlockByHeight GetBlockByHeight { get; }
        public PhantasmaGetBlockHeight GetBlockHeight { get; }
        public PhantasmaGetBlockTxCountByHash GetBlockTxCountByHash { get; }
        public PhantasmaGetChains GetChains { get; }
        public PhantasmaGetTokens GetTokens { get; }
        public PhantasmaGetRootChain GetRootChain { get; }
        public PhantasmaGetTxByBlockHashAndIndex GetTxByBlockHashAndIndex { get; }
        public PhantasmaGetTxByHash GetTxByHash { get; }
        public PhantasmaGetTxConfirmations GetTxConfirmations { get; set; }
        public PhantasmaSendRawTx SendRawTx { get; }
    }
}
