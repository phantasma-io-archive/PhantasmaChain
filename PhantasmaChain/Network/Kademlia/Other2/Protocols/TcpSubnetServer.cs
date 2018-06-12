using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Phantasma.Kademlia.Common;
using Phantasma.Utils;

namespace Phantasma.Kademlia.Protocols
{
    public class TcpSubnetServer
    {
        protected Dictionary<int, INode> subnets;
        protected HttpListener listener;
        protected string url;
        protected int port;
        protected bool running;

        protected Dictionary<string, Type> routePackets = new Dictionary<string, Type>
        {
            {"//Ping", typeof(PingRequest) },
            {"//Store", typeof(StoreRequest) },
            {"//FindNode", typeof(FindNodeRequest) },
            {"//FindValue", typeof(FindValueRequest) },
        };

        /// <summary>
        /// Instantiate the server, listening on the specified url and port.
        /// </summary>
        /// <param name="url">Of the form http://127.0.0.1 or https or domain name.  No trailing forward slash.</param>
        /// <param name="port">The port number.</param>
        public TcpSubnetServer(string url, int port)
        {
            this.url = url;
            this.port = port;
            subnets = new Dictionary<int, INode>();
        }

        public void RegisterProtocol(int subnet, INode node)
        {
            subnets[subnet] = node;
        }

        public void Start()
        {
            listener = new HttpListener();
            listener.Prefixes.Add(url + ":" + port + "/");
            listener.Start();
            running = true;
            Task.Run(() => WaitForConnection());
        }

        public void Stop()
        {
            running = false;
            listener.Stop();
        }

        protected virtual void WaitForConnection()
        {
            while (running)
            {
                // Wait for a connection.  Return to caller while we wait.
                HttpListenerContext context = listener.GetContext();
                ProcessRequest(context);
            }
        }

        protected virtual async void ProcessRequest(HttpListenerContext context)
        {
            string data = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();

            if (context.Request.HttpMethod == "POST")
            {
                Type requestType;
                string path = context.Request.RawUrl;

                if (routePackets.TryGetValue(path, out requestType))
                {
                    CommonRequest commonRequest = JsonConvert.DeserializeObject<CommonRequest>(data);
                    int subnet = ((BaseSubnetRequest)JsonConvert.DeserializeObject(data, requestType)).Subnet;
                    INode node;

                    if (subnets.TryGetValue(subnet, out node))
                    {
                        // Remove "//"
                        // Prefix our call with "Server" so that the method name is unambiguous.
                        string methodName = "Server" + path.Substring(2);      

#if DEBUG       // For unit testing
                        if (!((TcpSubnetProtocol)node.OurContact.Protocol).Responds)
                        {
                            // Exceeds 500ms timeout.
                            System.Threading.Thread.Sleep(1000);
                            context.Response.Close();
                            return;         // bail now.
                        }
#endif

                        try
                        {
                            MethodInfo mi = node.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
                            object response = await Task.Run(() => mi.Invoke(node, new object[] { commonRequest }));
                            SendResponse(context, response);
                        }
                        catch (Exception ex)
                        {
                            SendErrorResponse(context, new ErrorResponse() { ErrorMessage = ex.Message });
                        }
                    }
                    else
                    {
                        SendErrorResponse(context, new ErrorResponse() { ErrorMessage = "Subnet node not found." });
                    }
                }
                else
                {
                    SendErrorResponse(context, new ErrorResponse() { ErrorMessage = "Method not recognized." });
                }
            }

            context.Response.Close();
        }

        protected void SendResponse(HttpListenerContext context, object resp)
        {
            context.Response.StatusCode = 200;
            SendResponseInternal(context, resp);
        }

        protected void SendErrorResponse(HttpListenerContext context, ErrorResponse resp)
        {
            context.Response.StatusCode = 400;
            SendResponseInternal(context, resp);
        }

        private void SendResponseInternal(HttpListenerContext context, object resp)
        {
            context.Response.ContentType = "text/text";
            context.Response.ContentEncoding = Encoding.UTF8;
            byte[] byteData = JsonConvert.SerializeObject(resp).to_Utf8();

            context.Response.OutputStream.Write(byteData, 0, byteData.Length);
        }
    }
}