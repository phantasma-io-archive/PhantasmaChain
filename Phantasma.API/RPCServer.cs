using LunarLabs.Parser;
using LunarLabs.WebServer.Core;
using LunarLabs.WebServer.HTTP;
using LunarLabs.WebServer.Protocols;
using Phantasma.Core;
using Phantasma.Cryptography;

namespace Phantasma.API
{
    public class RPCServer : Runnable
    {
        public int Port { get; }
        public string EndPoint { get; }

        private readonly HTTPServer _server;

        private readonly NexusAPI _api;

        public RPCServer(NexusAPI api, string endPoint, int port, Logger logger = null)
        {
            if (logger == null)
            {
                logger = new NullLogger();
            }

            if (string.IsNullOrEmpty(endPoint))
            {
                endPoint = "/";
            }

            Port = port;
            EndPoint = endPoint;
            _api = api;

            var settings = new ServerSettings() { Environment = ServerEnvironment.Prod, Port = port, MaxPostSizeInBytes = 1024 * 128 };

            _server = new HTTPServer(settings, logger);

            var rpc = new RPCPlugin(_server, endPoint);

            // TODO do this automatically via reflection instead of doing it one by one manually
            rpc.RegisterHandler("getAccount", GetAccount);
            rpc.RegisterHandler("getAddressTransactions", GetAddressTransactions);
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
            rpc.RegisterHandler("sendRawTransaction", SendRawTransaction);

        }

        private object GetAccount(DataNode paramNode)
        {
            var address = Address.FromText(paramNode.GetNodeByIndex(0).ToString());
            return _api.GetAccount(address);
        }

        #region Blocks
        private object GetBlockHeight(DataNode paramNode)
        {
            var chain = paramNode.GetNodeByIndex(0).ToString();
            return _api.GetBlockNumber(chain) ?? _api.GetBlockNumber(Address.FromText(chain));
        }

        private object GetBlockTransactionCountByHash(DataNode paramNode)
        {
            var blockHash = Hash.Parse(paramNode.GetNodeByIndex(0).ToString());
            return _api.GetBlockTransactionCountByHash(blockHash);
        }

        private object GetBlockByHash(DataNode paramNode)
        {
            var blockHash = Hash.Parse(paramNode.GetNodeByIndex(0).ToString());
            return _api.GetBlockByHash(blockHash);
        }

        private object GetBlockByHeight(DataNode paramNode)
        {
            var chain = paramNode.GetNodeByIndex(0).ToString();
            var height = ushort.Parse(paramNode.GetNodeByIndex(1).ToString());
            return _api.GetBlockByHeight(chain, height) ?? _api.GetBlockByHeight(Address.FromText(chain), height);
        }
        #endregion

        private object GetChains(DataNode paramNode)
        {
            return _api.GetChains();
        }

        #region Transactions
        private object GetTransactionByHash(DataNode paramNode)
        {
            var hash = Hash.Parse(paramNode.GetNodeByIndex(0).ToString());
            return _api.GetTransaction(hash);
        }

        private object GetTransactionByBlockHashAndIndex(DataNode paramNode)
        {
            var blockHash = Hash.Parse(paramNode.GetNodeByIndex(0).ToString());
            int index = int.Parse(paramNode.GetNodeByIndex(0).ToString());
            return _api.GetTransactionByBlockHashAndIndex(blockHash, index);
        }

        private object GetAddressTransactions(DataNode paramNode)
        {
            var address = Address.FromText(paramNode.GetNodeByIndex(0).ToString());
            var amountTx = int.Parse(paramNode.GetNodeByIndex(1).ToString());
            return _api.GetAddressTransactions(address, amountTx);
        }

        #endregion

        private object GetTokens(DataNode paramNode)
        {
            return _api.GetTokens();
        }

        private object GetConfirmations(DataNode paramNode)
        {
            var hash = Hash.Parse(paramNode.GetNodeByIndex(0).ToString());
            return _api.GetConfirmations(hash);
        }

        private object SendRawTransaction(DataNode paramNode)
        {
            var signedTx = paramNode.GetNodeByIndex(0).ToString();
            return _api.SendRawTransaction(signedTx);
        }

        private object GetApps(DataNode paramNode)
        {
            return _api.GetApps();
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
    }
}
