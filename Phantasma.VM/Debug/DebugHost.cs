using Phantasma.Core;
using Phantasma.Storage;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Phantasma.VM.Debug
{
    public class DebugHost: Runnable
    {
        private Dictionary<string, Breakpoint> _breakpoints = new Dictionary<string, Breakpoint>();

        public bool Paused { get; private set; }

        private TcpListener _server;
        private NetworkStream _stream;

        private bool _stepping;
        private string _currentContext = "unknown";
        private uint _currentOffset = 0;

        public bool Connected { get; private set; }

        protected override void OnStart()
        {
            var localAddr = IPAddress.Parse("127.0.0.1");
            _server = new TcpListener(localAddr, Debugger.Port);
            _server.Start();
        }

        protected override bool Run()
        {
            try { 
                // Perform a blocking call to accept requests.
                // You could also use server.AcceptSocket() here.
                TcpClient client = _server.AcceptTcpClient();

                // Get a stream object for reading and writing
                _stream = client.GetStream();

                Connected = true;
                do
                {
                    Receive();

                } while (Connected);

                // Shutdown and end connection
                client.Close();
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
                return false;
            }

            return true;
        }

        protected override void OnStop()
        {
            this.Disconnect();
            _server.Stop();
        }

        public void Disconnect()
        {
            Connected = false;
               
        }

        private void ProcessCommand(DebugOpcode opcode, byte[] data)
        {
            switch (opcode)
            {
                case DebugOpcode.Log:
                    // do nothing, this only matters on the debugger client
                    break;

                case DebugOpcode.Add:
                    {
                        var breakpoint = Serialization.Unserialize<Breakpoint>(data);
                        AddBreakpoint(breakpoint);
                        break;
                    }

                case DebugOpcode.Remove:
                    {
                        var breakpoint = Serialization.Unserialize<Breakpoint>(data);
                        RemoveBreakpoint(breakpoint);
                        break;
                    }

                case DebugOpcode.Step:
                    {
                        if (Paused)
                        {
                            _stepping = true;
                            Paused = false;
                        }
                        break;
                    }

                case DebugOpcode.Resume:
                    if (Paused)
                    {
                        Paused = false;
                    }
                    break;

                case DebugOpcode.Status:
                    SendStatus();
                    break;

                default:
                    // TODO error handling / exception / disconnect
                    break;

            }
        }

        private void Receive()
        {
            if (!DebugUtils.ReceiveCommand(_stream, ProcessCommand))
            {
                Disconnect();
            }
        }

        private void Send(byte[] data)
        {
            _stream.Write(data, 0, data.Length);
        }

        public void OnStep(ExecutionFrame frame, Stack<VMObject> stack)
        {
            _currentContext = frame.Context.Name;
            _currentOffset = frame.Offset;

            if (_stepping)
            {
                Paused = true;
                _stepping = false;
            }
            else
            lock (_breakpoints)
            {
                foreach (var bp in _breakpoints.Values)
                {
                    if (bp.contextName == frame.Context.Name && bp.offset == frame.Offset)
                    {
                        Paused = true;
                        break;
                    }
                }
            }

            do
            {
                Thread.Sleep(500); // wait for debugger to receive commands
            } while (Paused);
        }

        public void OnLog(string msg)
        {
            var data = DebugUtils.EncodeMessage(DebugOpcode.Log, new LogCommand(msg));
            this.Send(data);
        }

        private void SendStatus()
        {
            var data = DebugUtils.EncodeMessage(DebugOpcode.Status, new StatusCommand(_currentContext, _currentOffset));
            this.Send(data);
        }

        public void AddBreakpoint(Breakpoint bp)
        {
            lock (_breakpoints)
            {
                _breakpoints[bp.Key] = bp;
            }
        }

        public void RemoveBreakpoint(Breakpoint bp)
        {
            lock (_breakpoints)
            {
                if (_breakpoints.ContainsKey(bp.Key))
                {
                    _breakpoints.Remove(bp.Key);
                }
            }
        }
    }
}
