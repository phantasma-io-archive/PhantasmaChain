using LunarLabs.Parser;
using LunarLabs.Parser.JSON;
using LunarLabs.WebServer.Core;
using LunarLabs.WebServer.HTTP;
using Phantasma.Core;
using Phantasma.Cryptography;

namespace Phantasma.API
{
    public class RPCServer : Runnable
    {
        public int Port { get; private set; }
        public string EndPoint { get; private set; }

        private Site _site;
        private HTTPServer _server;

        private NexusAPI _API;

        public RPCServer(NexusAPI API, string endPoint, int port, Logger logger = null)
        {
            if (logger == null)
            {
                logger = new NullLogger();
            }

            this.Port = port;
            this.EndPoint = endPoint;
            this._API = API;

            var settings = new ServerSettings() { environment = ServerEnvironment.Prod, port = port };

            _server = new HTTPServer(logger, settings);

            _site = new Site(_server, null);

            _site.Post("/" + EndPoint, (request) =>
              {
                  if (string.IsNullOrEmpty(request.postBody))
                  {
                      return GenerateRPCError("Invalid request", -32600);
                  }
                  else
                  {
                      DataNode root;
                      try
                      {
                          root = JSONReader.ReadFromString(request.postBody);
                      }
                      catch
                      {
                          return GenerateRPCError("Parsing error", -32700);
                      }

                      var version = root.GetString("jsonrpc");
                      if (version != "2" && version != "2.0")
                      {
                          return GenerateRPCError("Invalid jsonrpc version", -32602);
                      }

                      var method = root.GetString("method");
                      object result = null;

                      var paramNode = root.GetNode("params");

                      switch (method)
                      {
                          case "getaccount":
                              if (paramNode == null)
                              {
                                  return GenerateRPCError("Invalid params", -32602);
                              }

                              try
                              {
                                  var address = Address.FromText(paramNode.GetNodeByIndex(0).ToString());
                                  result = _API.GetAccount(address);
                              }
                              catch
                              {
                                  // ignore, it will be handled below
                              }

                              break;

                          case "getaddresstransactions":
                              if (paramNode == null)
                              {
                                  return GenerateRPCError("Invalid params", -32602);
                              }

                              try
                              {
                                  var address = Address.FromText(paramNode.GetNodeByIndex(0).ToString());
                                  var amountTx = int.Parse(paramNode.GetNodeByIndex(1).ToString());
                                  result = _API.GetAddressTransactions(address, amountTx);
                              }
                              catch
                              {
                                  // ignore, it will be handled below
                              }
                              break;

                          case "sendrawtransaction":
                              if (paramNode == null)
                              {
                                  return GenerateRPCError("Invalid params", -32602);
                              }
                              try //todo validation
                              {
                                  var chain = paramNode.GetNodeByIndex(0).ToString();
                                  var signedTx = paramNode.GetNodeByIndex(1).ToString();
                                  result = _API.SendRawTransaction(chain, signedTx);

                              }
                              catch
                              {
                                  // ignore, it will be handled below
                              }

                              break;

                          default:
                              return GenerateRPCError("Method not found", -32601);
                      }

                      if (result == null)
                      {
                          return GenerateRPCError("Missing result", -32603);
                      }

                      var id = root.GetString("id", "0");

                      string content;

                      if (result is DataNode)
                      {
                          content = JSONWriter.WriteToString((DataNode)result);
                      }
                      else
                      {
                          return GenerateRPCError("Not implemented", -32603);
                      }

                      return "{\"jsonrpc\": \"2.0\", \"result\": " + content + ", \"id\": \"" + id + "\"}";
                  }

              });
        }

        private string GenerateRPCError(string msg, int code = -32000, int id = 0)
        {
            return "{\"jsonrpc\": \"2.0\", \"error\": {\"code\": " + code + ", \"message\": \"" + msg + "\"}, \"id\": \"" + id + "\"}";
        }

        protected override void OnStop()
        {
            _server.Stop();
        }

        protected override bool Run()
        {
            _server.Run(_site);
            return true;
        }
    }
}
