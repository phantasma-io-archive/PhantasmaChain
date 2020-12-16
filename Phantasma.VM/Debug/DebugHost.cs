using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Phantasma.VM.Debug
{
    public class DebugHost
    {
        private List<Breakpoint> breakpoints = new List<Breakpoint>();

        public bool Paused { get; private set; }

        private Queue<DebugCommand> _commands = new Queue<DebugCommand>();

        private TcpListener _server;
        private NetworkStream _stream;
        private StringBuilder _buffer;

        private bool _active;

        public DebugHost()
        {
            _active = true;
            var socketThread = new Thread(SocketThread);
            socketThread.Start();
        }

        private void SocketThread()
        {
            var localAddr = IPAddress.Parse("127.0.0.1");

            try
            {
                _server = new TcpListener(localAddr, Debugger.Port);
                _server.Start();

                // Enter the listening loop.
                while (_active)
                {
                    // Perform a blocking call to accept requests.
                    // You could also use server.AcceptSocket() here.
                    TcpClient client = _server.AcceptTcpClient();

                    // Get a stream object for reading and writing
                    _stream = client.GetStream();

                    var data = Receive();

                    // Process the data sent by the client.
                    data = data.ToUpper();

                    // Send back a response.
                    Send(data);

                    // Shutdown and end connection
                    client.Close();
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
            }
            finally
            {
                // Stop listening for new clients.
                _server.Stop();
            }
        }

        private string Receive()
        {
            var bytes = new Byte[256];

            string result = null;

            // Loop to receive all the data sent by the client
            do
            {
                var read = _stream.Read(bytes, 0, bytes.Length);
                if (read == 0)
                {
                    return null;
                }

                for (int i = 0; i < read; i++)
                {
                    var ch = (char)_buffer[i];

                    if (ch == '\n')
                    {
                        if (result == null)
                        {
                            result = _buffer.ToString();
                            _buffer.Clear();
                        }
                    }
                    else
                    {
                        _buffer.Append(ch);
                    }
                }
            } while (result == null);

            return result;
        }

        private void Send(string message)
        {
            byte[] msg = System.Text.Encoding.ASCII.GetBytes(message);

            // Send back a response.
            _stream.Write(msg, 0, msg.Length);
            Console.WriteLine("Sent: {0}", message);
        }

        public void OnStep(ExecutionFrame frame, Stack<VMObject> stack)
        {
            foreach (var bp in breakpoints)
            {
                if (bp.contextName == frame.Context.Name && bp.offset == frame.Offset)
                {
                    Paused = true;
                    break;
                }
            }

            do
            {
                lock (_commands)
                {
                    if (_commands.Count > 0)
                    {
                        var cmd = _commands.Dequeue();
                        ExecuteCommand(cmd);
                    }
                }
            } while (Paused);
        }

        public void OnLog(string msg)
        {

        }

        private void ExecuteCommand(DebugCommand cmd)
        {
            switch (cmd.Opcode)
            {

            }
            throw new NotImplementedException();
        }
    }
}
