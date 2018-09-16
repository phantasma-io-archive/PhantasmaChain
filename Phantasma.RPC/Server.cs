using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using LunarLabs.Parser;
using LunarLabs.Parser.JSON;
using Phantasma.Core;

namespace Phantasma.Network.RPC
{
    public abstract class RPCServer : Runnable
    {
        public readonly int Port;
        private TcpListener _server;

        public RPCServer(int port)
        {
            this.Port = port;
            _server = new TcpListener(IPAddress.Any, port);
        }

        protected override void OnStart()
        {
            base.OnStart();

            // Start listening for client requests.
            _server.Start();
        }

        protected override void OnStop()
        {
            base.OnStop();

            // Stop listening for new clients.
            _server.Stop();
        }

        protected override bool Run()
        {
            try
            {
                Console.Write("Waiting for a connection... ");
                TcpClient client = _server.AcceptTcpClient();

                // Get a stream object for reading and writing
                var stream = client.GetStream();

                string input;

                using (var reader = new StreamReader(stream))
                {
                    input = reader.ReadLine();

                    Console.WriteLine(input);

                    string json;

                    DataNode root;

                    try
                    {
                        root = JSONReader.ReadFromString(input);
                    }
                    catch
                    {
                        root = null;
                    }

                    if (root != null)
                    {
                        lock (this)
                        {
                            json = ExecuteRequest(root);
                        }
                    }
                    else
                    {
                        json = "{\"response\" : {\"error\" : \"json parsing error\"}}";
                    }

                    Console.WriteLine(json);

                    var output = Encoding.UTF8.GetBytes(json);
                    stream.Write(output, 0, output.Length);
                    stream.Flush();
                    stream.Close();
                    client.Close();
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
            }

            return true;
        }

        internal abstract string ExecuteRequest(DataNode root);
    }
}
