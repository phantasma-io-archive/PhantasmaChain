using System;
using LunarLabs.Parser;
using LunarLabs.WebServer.Core;
using LunarLabs.WebServer.HTTP;
using LunarLabs.WebServer.Protocols;
using Phantasma.Core;
using Phantasma.Core.Log;
using Phantasma.Cryptography;

namespace Phantasma.API
{
    public class RPCServer : Runnable
    {
        public int Port { get; }
        public string EndPoint { get; }
        public readonly NexusAPI API;

        private readonly HTTPServer _server;

        public RPCServer(NexusAPI api, string endPoint, int port, LoggerCallback logger = null)
        {
            if (string.IsNullOrEmpty(endPoint))
            {
                endPoint = "/";
            }

            Port = port;
            EndPoint = endPoint;
            API = api;

            var settings = new ServerSettings() { Environment = ServerEnvironment.Prod, Port = port, MaxPostSizeInBytes = 1024 * 128 };

            _server = new HTTPServer(settings, logger);

            var rpc = new RPCPlugin(_server, endPoint);

            // TODO do this automatically via reflection instead of doing it one by one manually
            rpc.RegisterHandler("getAccount", GetAccount);
            rpc.RegisterHandler("getAddressTransactions", GetAddressTransactions);
            rpc.RegisterHandler("getAddressTxCount", GetAddressTxCount);
            rpc.RegisterHandler("getApps", GetApps);
            rpc.RegisterHandler("getBlockByHash", GetBlockByHash);
            rpc.RegisterHandler("getBlockByHeight", GetBlockByHeight);
            rpc.RegisterHandler("getBlockHeight", GetBlockHeight);
            rpc.RegisterHandler("getBlockTransactionCountByHash", GetBlockTransactionCountByHash);
            rpc.RegisterHandler("getChains", GetChains);
            rpc.RegisterHandler("getConfirmations", GetConfirmations);
            rpc.RegisterHandler("getTransactionByHash", GetTransactionByHash);
            rpc.RegisterHandler("getTransactionByBlockHashAndIndex", GetTransactionByBlockHashAndIndex);
            rpc.RegisterHandler("getTokens", GetTokens);
            rpc.RegisterHandler("getTokenBalance", GetTokenBalance);
            rpc.RegisterHandler("getTokenTransfers", GetTokenTransfers);
            rpc.RegisterHandler("getTokenTransferCount", GetTokenTransferCount);
            rpc.RegisterHandler("sendRawTransaction", SendRawTransaction);

            //todo new
            // todo add limits to amounts
            rpc.RegisterHandler("getRootChain", GetRootChain);
        }

        private object GetAccount(DataNode paramNode)
        {
            var result = API.GetAccount(paramNode.GetNodeByIndex(0).ToString());
            CheckForError(result);
            return result;
        }

        private object GetAddressTxCount(DataNode paramNode)
        {
            var address = paramNode.GetNodeByIndex(0).ToString();
            var chain = paramNode.GetNodeByIndex(1) != null ? paramNode.GetNodeByIndex(1).ToString() : "";
            var result = API.GetAddressTransactionCount(address, chain);
            CheckForError(result);
            return result;
        }

        #region Blocks
        private object GetBlockHeight(DataNode paramNode)
        {
            var chain = paramNode.GetNodeByIndex(0).ToString();
            var result = API.GetBlockHeightFromChainName(chain) ?? API.GetBlockHeightFromChainAddress(chain);
            CheckForError(result);
            return result;
        }

        private object GetBlockTransactionCountByHash(DataNode paramNode)
        {
            var result = API.GetBlockTransactionCountByHash(paramNode.GetNodeByIndex(0).ToString());
            CheckForError(result);
            return result;
        }

        private object GetBlockByHash(DataNode paramNode)
        {
            var serialized = paramNode.GetNodeByIndex(1) != null
                ? int.Parse(paramNode.GetNodeByIndex(1).ToString())
                : 0;
            var result = API.GetBlockByHash(paramNode.GetNodeByIndex(0).ToString(), serialized);
            CheckForError(result);
            return result;
        }

        private object GetBlockByHeight(DataNode paramNode)
        {
            var chain = paramNode.GetNodeByIndex(0).ToString();
            var height = ushort.Parse(paramNode.GetNodeByIndex(1).ToString());
            //optional, defaults to 0
            var serialized = paramNode.GetNodeByIndex(2) != null
                ? int.Parse(paramNode.GetNodeByIndex(2).ToString())
                : 0;

            var result = API.GetBlockByHeight(chain, height, serialized);
            if (result == null)
            {
                if (Address.IsValidAddress(chain))
                {
                    result = API.GetBlockByHeight(Address.FromText(chain), height, serialized);
                }
            }

            CheckForError(result);
            return result;
        }
        #endregion

        private object GetChains(DataNode paramNode)
        {
            var result = API.GetChains();
            CheckForError(result);
            return result;
        }

        #region Transactions
        private object GetTransactionByHash(DataNode paramNode)
        {
            var result = API.GetTransaction(paramNode.GetNodeByIndex(0).ToString());
            CheckForError(result);
            return result;
        }

        private object GetTransactionByBlockHashAndIndex(DataNode paramNode)
        {
            int index = int.Parse(paramNode.GetNodeByIndex(0).ToString());
            var result = API.GetTransactionByBlockHashAndIndex(paramNode.GetNodeByIndex(0).ToString(), index);
            CheckForError(result);
            return result;
        }

        private object GetAddressTransactions(DataNode paramNode)
        {
            var amountTx = int.Parse(paramNode.GetNodeByIndex(1).ToString());
            var result = API.GetAddressTransactions(paramNode.GetNodeByIndex(0).ToString(), amountTx);
            CheckForError(result);
            return result;
        }

        #endregion

        private object GetTokens(DataNode paramNode)
        {
            var result = API.GetTokens();
            CheckForError(result);
            return result;
        }

        private object GetTokenBalance(DataNode paramNode)
        {
            var address = paramNode.GetNodeByIndex(0).ToString();
            var tokenSymbol = paramNode.GetNodeByIndex(1).ToString();
            var chain = paramNode.GetNodeByIndex(2).ToString();
            var result = API.GetTokenBalance(address, tokenSymbol, chain);
            CheckForError(result);
            return result;
        }

        private object GetTokenTransfers(DataNode paramNode)
        {
            var tokenSymbol = paramNode.GetNodeByIndex(0).ToString();
            int amount = int.Parse(paramNode.GetNodeByIndex(1).ToString());
            var result = API.GetTokenTransfers(tokenSymbol, amount);
            CheckForError(result);
            return result;
        }

        private object GetTokenTransferCount(DataNode paramNode)
        {
            var tokenSymbol = paramNode.GetNodeByIndex(0).ToString();
            var result = API.GetTokenTransferCount(tokenSymbol);
            CheckForError(result);
            return result;
        }

        private object GetConfirmations(DataNode paramNode)
        {
            var result = API.GetConfirmations(paramNode.GetNodeByIndex(0).ToString());
            CheckForError(result);
            return result;
        }

        private object SendRawTransaction(DataNode paramNode)
        {
            var signedTx = paramNode.GetNodeByIndex(0).ToString();
            var result = API.SendRawTransaction(signedTx);
            CheckForError(result);
            return result;
        }

        private object GetApps(DataNode paramNode)
        {
            var result = API.GetApps();
            CheckForError(result);
            return result;
        }

        protected override void OnStop()
        {
            _server.Stop();
        }

        protected override bool Run()
        {
            _server.Run();
            return true;
        }

        private static void CheckForError(IAPIResult response)
        {
            if (response is ErrorResult)
            {
                var temp = (ErrorResult)response;
                throw new RPCException(temp.error);
            }
        }

        // new 
        private object GetRootChain(DataNode paramNode)
        {
            var result = API.GetRootChain();
            CheckForError(result);
            return result;
        }
    }
}

