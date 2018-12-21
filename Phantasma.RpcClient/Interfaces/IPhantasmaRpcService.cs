using Phantasma.RpcClient.Api;

namespace Phantasma.RpcClient.Interfaces
{
    public interface IPhantasmaRpcService
    {
        PhantasmaGetAccount GetAccount { get; }
        PhantasmaGetAddressTxs GetAddressTxs { get; }
        PhantasmaGetAddressTxCount GetAddressTxCount { get; }
        PhantasmaGetApplications GetApplications { get; }
        PhantasmaGetBlockByHash GetBlockByHash { get; }
        PhantasmaGetBlockByHashSerialized GetBlockByHashSerialized { get; }
        PhantasmaGetBlockByHeight GetBlockByHeight { get; }
        PhantasmaGetBlockByHeightSerialized GetBlockByHeightSerialized { get; }
        PhantasmaGetBlockHeight GetBlockHeight { get; }
        PhantasmaGetBlockTxCountByHash GetBlockTxCountByHash { get; }
        PhantasmaGetChains GetChains { get; }
        PhantasmaGetTokens GetTokens { get; }
        PhantasmaGetTokenTransfers GetTokenTransfers { get; }
        PhantasmaGetTokenTransferCount GetTokenTransferCount { get; set; }
        PhantasmaGetRootChain GetRootChain { get; }
        PhantasmaGetTxByBlockHashAndIndex GetTxByBlockHashAndIndex { get; }
        PhantasmaGetTxByHash GetTxByHash { get; }
        PhantasmaGetTxConfirmations GetTxConfirmations { get; }
        PhantasmaSendRawTx SendRawTx { get; }
    }
}
