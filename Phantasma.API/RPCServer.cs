using LunarLabs.WebServer.Core;
using LunarLabs.WebServer.HTTP;
using LunarLabs.WebServer.Plugins;
using Phantasma.Core;
using Phantasma.Domain;

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

            var settings = new ServerSettings() { Environment = ServerEnvironment.Prod, Port = port, MaxPostSizeInBytes = 1024 * 1024, Compression = false, Binding = ServerBinding.Any };

            _server = new HTTPServer(settings, logger);

            var rpc = new RPCPlugin(_server, endPoint);

            foreach (var entry in api.Methods)
            {
                var methodName = char.ToLower(entry.Name[0]) + entry.Name.Substring(1);
                var apiMethod = entry;
                rpc.RegisterHandler(methodName, (paramNode) =>
                {
                    var args = new object[apiMethod.Parameters.Count];
                    for (int i=0; i<args.Length; i++)
                    {
                        if (i < paramNode.ChildCount)
                        {
                            args[i] = paramNode.GetNodeByIndex(i).Value;
                        }
                        else
                        if (apiMethod.Parameters[i].HasDefaultValue)
                        {
                            args[i] = apiMethod.Parameters[i].DefaultValue;
                        }
                        else
                        {
                            throw new RPCException("missing argument: " + apiMethod.Parameters[i].Name);
                        }
                    }

                    try
                    {
                        var json = api.Execute(apiMethod.Name, args);
                        return json;
                    }
                    catch (APIException e)
                    {
                        throw new RPCException(e.Message);
                    }
                   
                });
            }
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

