using Phantasma.Core;
using Phantasma.Core.Log;
using System;
using System.Net;
using System.Threading;

namespace LunarLabs.Parser.JSON
{
    public class JSONRPC_Client
    {
        private WebClient client;

        public JSONRPC_Client()
        {
            client = new WebClient() { Encoding = System.Text.Encoding.UTF8 }; 
        }

        public DataNode SendRequest(Logger logger, string url, string method, params object[] parameters)
        {
            Throw.IfNull(logger, nameof(logger));

            DataNode paramData = DataNode.CreateArray("params");

            if (parameters!=null && parameters.Length > 0)
            {
                foreach (var obj in parameters)
                {
                    paramData.AddField(null, obj);
                }
            }

            var jsonRpcData = DataNode.CreateObject(null);
            jsonRpcData.AddField("jsonrpc", "2.0");
            jsonRpcData.AddField("method", method);
            jsonRpcData.AddField("id", "1");

            jsonRpcData.AddNode(paramData);

            int retries = 5;
            string contents = null;

            int retryDelay = 500;

            while (retries > 0)
            {
                try
                {
                    client.Headers.Add("Content-Type", "application/json-rpc");
                    var json = JSONWriter.WriteToString(jsonRpcData);
                    contents = client.UploadString(url, json);
                }
                catch (Exception e)
                {
                    retries--;
                    if (retries <= 0)
                    {
                        logger.Error(e.ToString());
                        return null;
                    }
                    else
                    {
                        logger.Warning($"Retrying connection to {url} after {retryDelay}ms...");
                        Thread.Sleep(retryDelay);
                        retryDelay *= 2;
                        continue;
                    }
                }

                break;
            }

            if (string.IsNullOrEmpty(contents))
            {
                return null;
            }

            //File.WriteAllText("response.json", contents);

            var root = JSONReader.ReadFromString(contents);

            if (root == null)
            {
                return null;
            }

            if (root.HasNode("result"))
            {
                return root["result"];
            }

            return root;
        }
   }
}