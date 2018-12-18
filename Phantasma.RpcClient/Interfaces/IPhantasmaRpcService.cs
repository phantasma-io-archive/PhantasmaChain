using Phantasma.RpcClient.Api;

namespace Phantasma.RpcClient.Interfaces
{
    public interface IPhantasmaRpcService
    {
        PhantasmaGetAccount GetAccount { get; }
        PhantasmaGetAccountTxs GetAccountTxs { get; }
        PhantasmaGetApplications GetApplications { get; }
        PhantasmaGetBlockByHash GetBlockByHash { get; }
        PhantasmaGetBlockByHeight GetBlockByHeight { get; }
        PhantasmaGetBlockHeight GetBlockHeight { get; }
        PhantasmaGetBlockTxCountByHash GetBlockTxCountByHash { get; }
        PhantasmaGetChains GetChains { get; }
        PhantasmaGetTokens GetTokens { get; }
        PhantasmaGetRootChain GetRootChain { get; }
        PhantasmaGetTxByBlockHashAndIndex GetTxByBlockHashAndIndex { get; }
        PhantasmaGetTxByHash GetTxByHash { get; }
        PhantasmaGetTxConfirmations GetTxConfirmations { get; }
        PhantasmaSendRawTx SendRawTx { get; }
    }
}
