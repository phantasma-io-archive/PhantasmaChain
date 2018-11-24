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

            _site.Get("/" + EndPoint, (request) =>
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
                          case "getAccount":
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

                          #region BLOCKS

                          case "getBlockNumber":
                              if (paramNode == null)
                              {
                                  return GenerateRPCError("Invalid params", -32602);
                              }

                              try
                              {
                                  var chain = paramNode.GetNodeByIndex(0).ToString();
                                  result = _API.GetBlockNumber(chain) ?? _API.GetBlockNumber(Address.FromText(chain));
                              }
                              catch
                              {
                                  // ignore, it will be handled below
                              }
                              break;

                          case "getBlockTransactionCountByHash":
                              if (paramNode == null)
                              {
                                  return GenerateRPCError("Invalid params", -32602);
                              }

                              try
                              {
                                  var blockHash = Hash.Parse(paramNode.GetNodeByIndex(0).ToString());
                                  result = _API.GetBlockTransactionCountByHash(blockHash);
                              }
                              catch
                              {
                                  // ignore, it will be handled below
                              }
                              break;

                          case "getBlockByHash":
                              if (paramNode == null)
                              {
                                  return GenerateRPCError("Invalid params", -32602);
                              }

                              try
                              {
                                  var blockHash = Hash.Parse(paramNode.GetNodeByIndex(0).ToString());
                                  result = _API.GetBlockByHash(blockHash);
                              }
                              catch
                              {
                                  // ignore, it will be handled below
                              }
                              break;

                          case "getBlockByNumber":
                              if (paramNode == null)
                              {
                                  return GenerateRPCError("Invalid params", -32602);
                              }

                              try
                              {
                                  var chain = paramNode.GetNodeByIndex(0).ToString();
                                  var height = ushort.Parse(paramNode.GetNodeByIndex(1).ToString());
                                  result = _API.GetBlockByHeight(chain, height) ?? _API.GetBlockByHeight(Address.FromText(chain), height);
                              }
                              catch
                              {
                                  // ignore, it will be handled below
                              }
                              break;

                          #endregion

                          case "getChains":
                              try
                              {
                                  result = _API.GetChains();
                              }
                              catch
                              {
                                  // ignore, it will be handled below
                              }

                              break;

                          #region Transactions
                          case "getTransactionByHash":
                              if (paramNode == null)
                              {
                                  return GenerateRPCError("Invalid params", -32602);
                              }

                              try
                              {
                                  var hash = Hash.Parse(paramNode.GetNodeByIndex(0).ToString());
                                  result = _API.GetTransaction(hash);
                              }
                              catch
                              {
                                  // ignore, it will be handled below
                              }
                              break;

                          case "getTransactionByBlockHashAndIndex":
                              if (paramNode == null)
                              {
                                  return GenerateRPCError("Invalid params", -32602);
                              }
                              try
                              {
                                  var blockHash = Hash.Parse(paramNode.GetNodeByIndex(0).ToString());
                                  int index = int.Parse(paramNode.GetNodeByIndex(0).ToString());
                                  result = _API.GetTransactionByBlockHashAndIndex(blockHash, index);
                              }
                              catch
                              {
                                  // ignore, it will be handled below
                              }
                              break;

                          case "getAddressTransactions":
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

                          #endregion

                          case "getTokens":
                              try
                              {
                                  result = _API.GetTokens();
                              }
                              catch
                              {
                                  // ignore, it will be handled below
                              }
                              break;
                          case "getConfirmations":
                              if (paramNode == null)
                              {
                                  return GenerateRPCError("Invalid params", -32602);
                              }

                              try
                              {
                                  var hash = Hash.Parse(paramNode.GetNodeByIndex(0).ToString());
                                  result = _API.GetConfirmations(hash);
                              }
                              catch
                              {
                                  // ignore, it will be handled below
                              }
                              break;

                          case "sendRawTransaction":
                              if (paramNode == null)
                              {
                                  return GenerateRPCError("Invalid params", -32602);
                              }
                              try //todo validation
                              {
                                  var signedTx = paramNode.GetNodeByIndex(0).ToString();
                                  result = _API.SendRawTransaction(signedTx);
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
            _server.Run();
            return true;
        }
    }
}
