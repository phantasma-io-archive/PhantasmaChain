using LunarLabs.Parser;
using LunarLabs.Parser.JSON;
using Phantasma.Neo.Cryptography;
using Phantasma.Neo.Utils;
using System;
using System.Numerics;
using PBigInteger = Phantasma.Numerics.BigInteger;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Phantasma.Cryptography;

namespace Phantasma.Neo.Core
{
    public abstract class NeoRPC : NeoAPI
    {
        public readonly string neoscanUrl;

        public NeoRPC(string neoscanURL)
        {
            this.neoscanUrl = neoscanURL;
        }

        public static NeoRPC ForMainNet(NEONodesKind kind = NEONodesKind.COZ)
        {
            return new RemoteRPCNode(10332, "http://neoscan.io", kind);
        }

        public static NeoRPC ForTestNet()
        {
            return new RemoteRPCNode(20332, "https://neoscan-testnet.io", NEONodesKind.NEO_ORG);
        }

        public static NeoRPC ForPrivateNet()
        {
            return new LocalRPCNode(30333, "http://localhost:4000");
        }

        #region RPC API
        public string rpcEndpoint { get; set; }
        private static object rpcEndpointUpdateLocker = new object();

        protected abstract string GetRPCEndpoint();

        private void LogData(DataNode node, int ident = 0)
        {
            var tabs = new string('\t', ident);
            Logger($"{tabs}{node}");
            foreach (DataNode child in node.Children)
                LogData(child, ident + 1);
        }

        public DataNode QueryRPC(string method, object[] _params, int id = 1, bool numeric = false)
        {
            var paramData = DataNode.CreateArray("params");
            foreach (var entry in _params)
            {
                if (numeric)
                {
                    paramData.AddField(null, (int)entry);
                }
                else if (entry.GetType() == typeof(BigInteger))
                { 
                    /*
                     * TODO sufficient for neo2 but needs a better solution in the future.
                     * Could fail if entry > maxInt.
                     */
                    paramData.AddField(null, (int)(BigInteger)entry);
                }
                else
                {
                    paramData.AddField(null, entry);
                }
            }

            var jsonRpcData = DataNode.CreateObject(null);
            jsonRpcData.AddField("jsonrpc", "2.0");
            jsonRpcData.AddField("method", method);
            jsonRpcData.AddNode(paramData);
            jsonRpcData.AddField("id", id);

            //Logger("QueryRPC: " + method);
            //LogData(jsonRpcData);

            int retryCount = 0;
            do
            {
                string currentRpcEndpoint; // Using local var to avoid it being nullified by another thread right before RequestUtils.Request() call.
                lock (rpcEndpointUpdateLocker)
                {
                    if (rpcEndpoint == null)
                    {
                        rpcEndpoint = GetRPCEndpoint();
                        Logger("Update RPC Endpoint: " + rpcEndpoint);
                    }
                    currentRpcEndpoint = rpcEndpoint;
                }

                var response = RequestUtils.Request(RequestType.POST, currentRpcEndpoint, jsonRpcData);

                if (response != null)
                {
                    if (response.HasNode("result"))
                    {
                        LastError = null;
                        return response;
                    }

                    if (response.HasNode("error"))
                    {
                        var error = response["error"];
                        LastError = error.GetString("message");
                    }
                    else
                    {
                        LastError = "Unknown RPC error";
                    }
                }
                else
                {
                    LastError = "Connection failure";
                }

                Logger("RPC Error: " + LastError);
                rpcEndpoint = null;
                retryCount++;
                Thread.Sleep(1000);

            } while (retryCount < 10);

            return null;
        }
        #endregion

        public override bool HasPlugin(string pluginName)
        {
            var response = QueryRPC("listplugins", new object[]{});
            var result = new Dictionary<string, decimal>();
            var resultNode = response.GetNode("result");

            foreach (var entry in resultNode.Children)
            {
                foreach (var en in entry.Children)
                {
                    if (string.Equals(en.Name, "name")
                            && string.Equals(en.Value, pluginName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            Console.WriteLine("FALSE");
            return false;
        }

        public override string GetNep5Transfers(UInt160 scriptHash, DateTime timestamp)
        {
            if (!HasPlugin("RpcNep5Tracker"))
            {
                return null;
            }

            var unixTimestamp = (timestamp.ToUniversalTime()
                    - (new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc))).TotalSeconds;

            var response = QueryRPC("getnep5transfers", new object[] { scriptHash.ToAddress(), unixTimestamp });
            string json = JSONWriter.WriteToString(response);

            return json;
        }

        public override string GetUnspents(UInt160 scriptHash)
        {
            if (!HasPlugin("RpcSystemAssetTrackerPlugin"))
            {
                return null;
            }

            var response = QueryRPC("getunspents", new object[] { scriptHash.ToAddress() });
            string json = JSONWriter.WriteToString(response);

            return json;
        }

        public override Dictionary<string, decimal> GetAssetBalancesOf(UInt160 scriptHash)
        {
            var response = QueryRPC("getaccountstate", new object[] { scriptHash.ToAddress() });
            var result = new Dictionary<string, decimal>();

            var resultNode = response.GetNode("result");
            var balances = resultNode.GetNode("balances");

            foreach (var entry in balances.Children)
            {
                var assetID = entry.GetString("asset");
                var amount = entry.GetDecimal("value");

                var symbol = SymbolFromAssetID(assetID);

                result[symbol] = amount;
            }

            return result;
        }

        public override byte[] GetStorage(string scriptHash, byte[] key)
        {
            var response = QueryRPC("getstorage", new object[] { key.ByteToHex() });
            var result = response.GetString("result");
            if (string.IsNullOrEmpty(result))
            {
                return null;
            }
            return result.HexToBytes();
        }

        // Note: This current implementation requires NeoScan running at port 4000
        public override Dictionary<string, List<UnspentEntry>> GetUnspent(UInt160 hash)
        {
            var url = this.neoscanUrl +"/api/main_net/v1/get_balance/" + hash.ToAddress();
            var json = RequestUtils.GetWebRequest(url);

            var root = LunarLabs.Parser.JSON.JSONReader.ReadFromString(json);
            var unspents = new Dictionary<string, List<UnspentEntry>>();

            root = root["balance"];

            foreach (var child in root.Children)
            {
                var symbol = child.GetString("asset");

                List<UnspentEntry> list = new List<UnspentEntry>();
                unspents[symbol] = list;

                var unspentNode = child.GetNode("unspent");
                foreach (var entry in unspentNode.Children)
                {
                    var txid = entry.GetString("txid");
                    var val = entry.GetDecimal("value");
                    var temp = new UnspentEntry() { hash = new UInt256(LuxUtils.ReverseHex(txid).HexToBytes()), value = val, index = entry.GetUInt32("n") };
                    list.Add(temp);
                }
            }

            return unspents;
        }

        // Note: This current implementation requires NeoScan running at port 4000
        public override List<UnspentEntry> GetClaimable(UInt160 hash, out decimal amount)
        {
            var url = this.neoscanUrl + "/api/main_net/v1/get_claimable/" + hash.ToAddress();
            var json = RequestUtils.GetWebRequest(url);

            var root = LunarLabs.Parser.JSON.JSONReader.ReadFromString(json);
            var result = new List<UnspentEntry>();

            amount = root.GetDecimal("unclaimed");

            root = root["claimable"];

            foreach (var child in root.Children)
            {
                var txid = child.GetString("txid");
                var index = child.GetUInt32("n");
                var value = child.GetDecimal("unclaimed");

                result.Add(new UnspentEntry() { hash = new UInt256(LuxUtils.ReverseHex(txid).HexToBytes()), index = index, value = value });
            }

            return result;
        }

        public bool SendRawTransaction(string hexTx)
        {
            var response = QueryRPC("sendrawtransaction", new object[] { hexTx });
            if (response == null)
            {
                throw new Exception("Connection failure");
            }

            try
            {
                var temp = response["result"];

                bool result;

                if (temp.HasNode("succeed"))
                {
                    result = temp.GetBool("succeed");
                }
                else
                {
                    result = temp.AsBool();
                }

                return result;
            }
            catch
            {
                return false;
            }
        }

        protected override bool SendTransaction(Transaction tx)
        {
            var rawTx = tx.Serialize(true);
            var hexTx = rawTx.ByteToHex();

            return SendRawTransaction(hexTx);
        }

        public override InvokeResult InvokeScript(byte[] script)
        {
            var invoke = new InvokeResult();
            invoke.state = VMState.NONE;

            var response = QueryRPC("invokescript", new object[] { script.ByteToHex()});

            if (response != null)
            {
                var root = response["result"];
                if (root != null)
                {
                    var stack = root["stack"];
                    invoke.result = ParseStack(stack);

                    invoke.gasSpent = root.GetDecimal("gas_consumed");
                    var temp = root.GetString("state");

                    if (temp.Contains("FAULT"))
                    {
                        invoke.state = VMState.FAULT;
                    }
                    else
                    if (temp.Contains("HALT"))
                    {
                        invoke.state = VMState.HALT;
                    }
                    else
                    {
                        invoke.state = VMState.NONE;
                    }
                }
            }

            return invoke;
        }

        public override string GetTransactionHeight(UInt256 hash)
        {
            var response = QueryRPC("gettransactionheight", new object[] { hash.ToString() });
            if (response != null && response.HasNode("result"))
            {
                return response.GetString("result");
            }
            else
            {
                return null;
            }
        }

        public override Dictionary<string, BigInteger> GetSwapBlocks(string hash, string address, string height = null)
        {
            if (!HasPlugin("EventTracker"))
            {
                return new Dictionary<string, BigInteger>();
            }

            var objects = new List<object>() {hash, address};

            if (!string.IsNullOrEmpty(height))
            {
                objects.Add(BigInteger.Parse(height));
            }

            var response = QueryRPC("getblockids", objects.ToArray());
            if (response != null && response.HasNode("result"))
            {
                var blockIds = new Dictionary<string, BigInteger>();

                var blocks = response["result"];
                //LogData(blocks);
                for (var i = 0; i < blocks.ChildCount; i++)
                {
                    var blockHash = blocks[i].GetString("block_hash");
                    if (blockHash.StartsWith("0x"))
                    {
                        blockHash = Hash.Parse(blockHash.Substring(2)).ToString();
                    }

                    blockIds.Add(blockHash, new BigInteger(blocks[i].GetInt32("block_index")));
                }

                return blockIds;
            }
            else
            {
                return new Dictionary<string, BigInteger>();
            }
        }

        public override ApplicationLog[] GetApplicationLog(UInt256 hash)
        {
            if (!HasPlugin("ApplicationLogs"))
            {
                return null;
            }

            var response = QueryRPC("getapplicationlog", new object[] { hash.ToString() });
            if (response != null && response.HasNode("result"))
            {
                //var json = LunarLabs.Parser.JSON.JSONReader.ReadFromString(response);
                List<ApplicationLog> appLogList = new List<ApplicationLog>();

                var executions = response["result"]["executions"];
                //LogData(executions);
                for (var i = 0; i < executions.ChildCount; i++)
                {
                    VMState vmstate;
                    if (Enum.TryParse(executions[i].GetString("vmstate"), out vmstate))
                    {
                        //LogData(executions[i]["notifications"][0]["state"]["value"]);
                        var notifications = executions[i]["notifications"];
                        for (var j = 0; j < notifications.ChildCount; j++)
                        {
                            var states = notifications[j]["state"]["value"];
                            string txevent = "";
                            UInt160 source = UInt160.Zero;
                            UInt160 target = UInt160.Zero;
                            PBigInteger amount = 0;
                            var contract = notifications[j].GetString("contract"); 

                            if(states[0].GetString("type") == "ByteArray")
                                txevent = (states[0].GetString("value"));

                            if(states[1].GetString("type") == "ByteArray" 
                                    && !string.IsNullOrEmpty(states[1].GetString("value")))
                                source = UInt160.Parse(states[1].GetString("value"));

                            if(states[2].GetString("type") == "ByteArray" 
                                    && !string.IsNullOrEmpty(states[2].GetString("value")))
                                target = UInt160.Parse(states[2].GetString("value"));

                            if (states[3].GetString("type") == "ByteArray")
                            {
                                amount = PBigInteger.FromUnsignedArray(states[3].GetString("value").HexToBytes(), true); // needs to be Phantasma.Numerics.BigInteger for now.
                            }
                            appLogList.Add(new ApplicationLog(vmstate, contract, txevent, source, target, amount));
                        }
                    }
                }

                return appLogList.ToArray();
            }
            else
            {
                return null;
            }
        }

        public override Transaction GetTransaction(UInt256 hash)
        {
            var response = QueryRPC("getrawtransaction", new object[] { hash.ToString() });
            if (response != null && response.HasNode("result"))
            {
                var result = response.GetString("result");
                var bytes = result.HexToBytes();
                return Transaction.Unserialize(bytes);
            }
            else
            {
                return null;
            }
        }

        public override BigInteger GetBlockHeight()
        {
            var response = QueryRPC("getblockcount", new object[] { });
            var blockCount = response.GetUInt32("result");
            return blockCount;
        }

        public override List<Block> GetBlockRange(PBigInteger start, PBigInteger end)
        {
            List<Task<DataNode>> taskList = new List<Task<DataNode>>();
            List<Block> blockList = new List<Block>();

            for (var i = start; i < end; i++)
            {
                var height = i;
                object[] heightData = new object[] { (int)height };

                taskList.Add(
                        new Task<DataNode>(() => 
                        {
                            return QueryRPC("getblock", heightData, 1, true);
                        })
                );
            }

            foreach (var task in taskList)
            {
                task.Start();
            }

            Task.WaitAll(taskList.ToArray());

            foreach (var task in taskList)
            {
                var response = task.Result;

                if (response == null || !response.HasNode("result"))
                {
                    return null;
                }

                var result = response.GetString("result");

                var bytes = result.HexToBytes();

                using (var stream = new MemoryStream(bytes))
                {
                    using (var reader = new BinaryReader(stream))
                    {
                        var block = Block.Unserialize(reader);
                        blockList.Add(block);
                    }
                }
            }

            return blockList;
        }

        public override Block GetBlock(BigInteger height)
        {
            object[] heightData = new object[] { (int)height };
            var response = QueryRPC("getblock", heightData, 1, true);
            if (response == null || !response.HasNode("result"))
            {
                return null;
            }

            var result = response.GetString("result");

            var bytes = result.HexToBytes();

            using (var stream = new MemoryStream(bytes))
            {
                using (var reader = new BinaryReader(stream))
                {
                    var block = Block.Unserialize(reader);
                    return block;
                }
            }
        }

        public override Block GetBlock(UInt256 hash)
        {
            var response = QueryRPC("getblock", new object[] { hash.ToString() });
            if (response == null || !response.HasNode("result"))
            {
                return null;
            }

            var result = response.GetString("result");

            var bytes = result.HexToBytes();

            using (var stream = new MemoryStream(bytes))
            {
                using (var reader = new BinaryReader(stream))
                {
                    var block = Block.Unserialize(reader);
                    return block;
                }
            }
        }
    }

    public class LocalRPCNode : NeoRPC
    {
        private int port;

        public LocalRPCNode(int port, string neoscanURL) : base(neoscanURL)
        {
            this.port = port;
        }

        protected override string GetRPCEndpoint()
        {
            return $"http://localhost:{port}";
        }
    }

    public enum NEONodesKind
    {
        NEO_ORG,
        COZ,
        TRAVALA
    }

    public class RemoteRPCNode : NeoRPC
    {
        private int rpcIndex = 0;

        private string[] nodes;

        public RemoteRPCNode(string neoscanURL, params string[] nodes) : base(neoscanURL)
        {
            this.nodes = nodes;
        }

        public RemoteRPCNode(int port, string neoscanURL, NEONodesKind kind) : base(neoscanURL)
        {
            switch (kind)
            {
                case NEONodesKind.NEO_ORG:
                    {
                        nodes = new string[5];
                        for (int i = 0; i < nodes.Length; i++)
                        {
                            nodes[i] = $"http://seed{i}.neo.org:{port}";
                        }
                        break;
                    }

                case NEONodesKind.COZ:
                    {
                        if (port == 10331)
                        {
                            port = 443;
                        }

                        nodes = new string[5];
                        for (int i = 0; i < nodes.Length; i++)
                        {
                            nodes[i] = $"http://seed{i}.cityofzion.io:{port}";
                        }
                        break;
                    }

                case NEONodesKind.TRAVALA:
                    {
                        nodes = new string[5];
                        for (int i = 0; i < nodes.Length; i++)
                        {
                            nodes[i] = $"http://seed{i}.travala.com:{port}";
                        }
                        break;
                    }
            }
        }

        protected override string GetRPCEndpoint()
        {
            rpcIndex++;
            if (rpcIndex >= nodes.Length)
            {
                rpcIndex = 0;
            }

            return nodes[rpcIndex];
        }
    }
}
