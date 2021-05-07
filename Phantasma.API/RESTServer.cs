using LunarLabs.Parser;
using LunarLabs.WebServer.Core;
using LunarLabs.WebServer.HTTP;
using Phantasma.Core;
using Phantasma.Domain;
using System.Collections.Generic;

namespace Phantasma.API
{
    public class RESTServer : Runnable
    {
        public int Port { get; }
        public string EndPoint { get; }
        public readonly NexusAPI API;

        private readonly HTTPServer _server;

        public RESTServer(NexusAPI api, string endPoint, int port, LoggerCallback logger = null)
        {
            if (string.IsNullOrEmpty(endPoint))
            {
                endPoint = "/";
            }

            if (!endPoint.EndsWith("/"))
            {
                endPoint += "/";
            }

            Port = port;
            EndPoint = endPoint;
            API = api;

            var settings = new ServerSettings() { Environment = ServerEnvironment.Prod, Port = port, MaxPostSizeInBytes = 1024 * 1024, Binding = ServerBinding.Any };

            _server = new HTTPServer(settings, logger);

            var apiMap = DataNode.CreateObject();

            foreach (var entry in api.Methods)
            {
                var methodName = char.ToLower(entry.Name[0]) + entry.Name.Substring(1);
                var apiMethod = entry;

                var path = endPoint + methodName;
                var paths = new List<string>();

                var mapEntry = DataNode.CreateObject(methodName);
                apiMap.AddNode(mapEntry);

                var argMap = DataNode.CreateArray("args");
                mapEntry.AddNode(argMap);

                paths.Add(path);
                foreach (var arg in apiMethod.Parameters)
                {
                    var argEntry = DataNode.CreateObject();
                    argEntry.AddField("name", arg.Name);
                    argEntry.AddField("type", arg.Type.Name);
                    argEntry.AddField("required", arg.DefaultValue == null);
                    argEntry.AddField("description", arg.Description);
                    argMap.AddNode(argEntry);

                    path += "/{" + arg.Name + "}";
                    paths.Add(path);
                }

                foreach (var url in paths)
                {
                    _server.Get(url, (request) =>
                    {
                        var args = new object[apiMethod.Parameters.Count];

                        IAPIResult result;
                        try
                        {
                            for (int i = 0; i < args.Length; i++)
                            {
                                var name = apiMethod.Parameters[i].Name;
                                if (request.args.ContainsKey(name))
                                {
                                    args[i] = request.args[name];
                                }
                                else
                                if (apiMethod.Parameters[i].DefaultValue != null)
                                {
                                    args[i] = apiMethod.Parameters[i].DefaultValue;
                                }
                                else
                                {
                                    throw new APIException("missing argument: " + apiMethod.Parameters[i].Name);
                                }
                            }

                            var json = api.Execute(apiMethod.Name, args);
                            return json;
                        }
                        catch (APIException e)
                        {
                            result = new ErrorResult() { error = e.Message };
                            var temp = (ErrorResult)result;
                            var error = DataNode.CreateObject();
                            error.AddField("error", temp.error);
                            return error;
                        }
                    });
                }

                _server.Get(endPoint, (request) =>
                {
                    return apiMap;
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

