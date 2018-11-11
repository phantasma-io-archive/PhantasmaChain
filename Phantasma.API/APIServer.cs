using LunarLabs.WebServer.Core;
using LunarLabs.WebServer.HTTP;

namespace Phantasma.API
{
    public class APIServer
    {
        public int Port { get; private set; }

        private Site _site;
        private HTTPServer _server;

        public APIServer(int port, Logger logger = null)
        {
            this.Port = port;

            var settings = new ServerSettings() { environment = ServerEnvironment.Prod, port = port };

            _server = new HTTPServer(logger, settings);

            _site = new Site(_server, null);
        }

        public void Start()
        {
            _server.Run(_site);
        }
    }
}
