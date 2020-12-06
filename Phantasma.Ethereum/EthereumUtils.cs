using System;
using System.Numerics;
using System.Threading;
using LunarLabs.Parser;
using Phantasma.Numerics;
using Phantasma.Core.Utils;
using Phantasma.Ethereum.Signer;

namespace Phantasma.Ethereum
{
    public static class EthereumUtils
    {
        public static byte[] SignTransaction(EthereumKey keys, int nonce, string receiveAddress, BigInteger amount)
        {
            var privateKey = "0x"+Base16.Encode(keys.PrivateKey);
            var senderAddress = keys.Address;

            var signer = new MessageSigner();

            var realAmount = System.Numerics.BigInteger.Parse(amount.ToString());

            //Create a transaction from scratch
            var tx = new Transaction(receiveAddress, realAmount, nonce, 10000000000000, 21000);
            tx.Sign(new EthECKey(keys.PrivateKey, true));

            var encoded = tx.GetRLPEncoded();

            return encoded;
        }

        public static string LastError = null;

        public static int GetAccountNonce(string rpcEndpoint, string address)
        {
            var response = QueryRPC(rpcEndpoint, "eth_getTransactionCount", new object[] { address, "latest"});
            if (response == null)
            {
                throw new Exception("Connection failure");
            }

            var hex = response.GetString("result", null);
            if (string.IsNullOrEmpty(hex))
            {
                return -1;
            }

            int result = Convert.ToInt32(hex, 16);
            return result;
        }

        // returns hash if sucessful, null otherwise
        public static string SendRawTransaction(string rpcEndpoint, byte[] signedTxBytes)
        {
            var hexTx = "0x" + Base16.Encode(signedTxBytes);
            var response = QueryRPC(rpcEndpoint, "eth_sendRawTransaction", new object[] { hexTx });
            if (response == null)
            {
                throw new Exception("Connection failure");
            }

            return response.GetString("result", null);
        }

        public static DataNode QueryRPC(string rpcEndpoint, string method, object[] _params, int id = 1, bool numeric = false)
        {
            var paramData = DataNode.CreateArray("params");
            foreach (var entry in _params)
            {
                paramData.AddField(null, (numeric) ? (int)entry : entry);
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
                var response = RequestUtils.Request(RequestType.POST, rpcEndpoint, jsonRpcData);

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

                //Logger("RPC Error: " + LastError);
                rpcEndpoint = null;
                retryCount++;
                Thread.Sleep(1000);

            } while (retryCount < 10);

            return null;
        }

    }
}
