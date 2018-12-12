using LunarLabs.Parser;
using LunarLabs.Parser.JSON;
using LunarLabs.WebServer.Core;
using LunarLabs.WebServer.HTTP;
using LunarLabs.WebServer.Protocols;
using Phantasma.Core;
using Phantasma.Cryptography;

namespace Phantasma.API
{
    public class RPCServer : Runnable
    {
        public int Port { get; private set; }
        public string EndPoint { get; private set; }

        private HTTPServer _server;
        private RPCPlugin _rpc;

        private NexusAPI _API;

        public RPCServer(NexusAPI API, string endPoint, int port, Logger logger = null)
        {
            if (logger == null)
            {
                logger = new NullLogger();
            }

            if (string.IsNullOrEmpty(endPoint))
            {
                endPoint = "/";
            }

            this.Port = port;
            this.EndPoint = endPoint;
            this._API = API;

            var settings = new ServerSettings() { Environment = ServerEnvironment.Prod, Port = port };

            _server = new HTTPServer(settings, logger);

            _rpc = new RPCPlugin(_server, endPoint);

            // TODO do this automatically via reflection instead of doing it one by one manually
            _rpc.RegisterHandler("getAccount", GetAccount);
            _rpc.RegisterHandler("getBlockNumber", GetBlockNumber);
            _rpc.RegisterHandler("getBlockTransactionCountByHash", GetBlockTransactionCountByHash);
            _rpc.RegisterHandler("getBlockByHash", GetBlockByHash);
            _rpc.RegisterHandler("getChains", GetChains);
            _rpc.RegisterHandler("getTransactionByBlockHashAndIndex", GetTransactionByBlockHashAndIndex);
            _rpc.RegisterHandler("getAddressTransactions", GetAddressTransactions);
            _rpc.RegisterHandler("getTokens", GetTokens);
            _rpc.RegisterHandler("getConfirmations", GetConfirmations);
            _rpc.RegisterHandler("sendRawTransaction", SendRawTransaction);

        }

        private object GetAccount(DataNode paramNode)
        {
            var address = Address.FromText(paramNode.GetNodeByIndex(0).ToString());
            return _API.GetAccount(address);
        }

        #region Blocks
        private object GetBlockNumber(DataNode paramNode)
        {
            var chain = paramNode.GetNodeByIndex(0).ToString();
            return _API.GetBlockNumber(chain) ?? _API.GetBlockNumber(Address.FromText(chain));
        }

        private object GetBlockTransactionCountByHash(DataNode paramNode)
        {
            var blockHash = Hash.Parse(paramNode.GetNodeByIndex(0).ToString());
            return _API.GetBlockTransactionCountByHash(blockHash);
        }

        private object GetBlockByHash(DataNode paramNode)
        {
            var blockHash = Hash.Parse(paramNode.GetNodeByIndex(0).ToString());
            return _API.GetBlockByHash(blockHash);
        }

        private object GetBlockByNumber(DataNode paramNode)
        {
            var chain = paramNode.GetNodeByIndex(0).ToString();
            var height = ushort.Parse(paramNode.GetNodeByIndex(1).ToString());
            return _API.GetBlockByHeight(chain, height) ?? _API.GetBlockByHeight(Address.FromText(chain), height);
        }
        #endregion

        private object GetChains(DataNode paramNode){
            return _API.GetChains();
        }

        #region Transactions
        private object GetTransactionByHash(DataNode paramNode)
        {
            var hash = Hash.Parse(paramNode.GetNodeByIndex(0).ToString());
            return _API.GetTransaction(hash);
        }

        private object GetTransactionByBlockHashAndIndex(DataNode paramNode)
        {
            var blockHash = Hash.Parse(paramNode.GetNodeByIndex(0).ToString());
            int index = int.Parse(paramNode.GetNodeByIndex(0).ToString());
            return _API.GetTransactionByBlockHashAndIndex(blockHash, index);
        }

        private object GetAddressTransactions(DataNode paramNode)
        {
            var address = Address.FromText(paramNode.GetNodeByIndex(0).ToString());
            var amountTx = int.Parse(paramNode.GetNodeByIndex(1).ToString());
            return _API.GetAddressTransactions(address, amountTx);
        }

        #endregion

        private object GetTokens(DataNode paramNode)
        {
            return _API.GetTokens();
        }

        private object GetConfirmations(DataNode paramNode)
        {
            var hash = Hash.Parse(paramNode.GetNodeByIndex(0).ToString());
            return _API.GetConfirmations(hash);
        }

        private object SendRawTransaction(DataNode paramNode)
        {
            var signedTx = paramNode.GetNodeByIndex(0).ToString();
            return _API.SendRawTransaction(signedTx);
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
