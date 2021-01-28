using System;
using System.Text;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Linq;
using Phantasma.Core;
using Phantasma.Storage;

namespace Phantasma.VM.Debug
{
    public class Debugger : Runnable
    {
        public const int Port = 7671;

        private TcpClient _client;
        private NetworkStream _stream;

        public Action<string> OnLog = null;

        public Debugger(string host = "localhost", int port = Port)
        {
            _client = new TcpClient(host, port);
        }

        protected override void OnStart()
        {
            _stream = _client.GetStream();
        }

        protected override void OnStop()
        {
            if (_stream != null)
            {
                // Close everything.
                _stream.Close();
                _client.Close();

                _stream = null;
                _client = null;
            }
        }

        protected override bool Run()
        {
            return DebugUtils.ReceiveCommand(_stream, ProcessCommand);
        }

        private void ProcessCommand(DebugOpcode opcode, byte[] data)
        {
            switch (opcode)
            {
                case DebugOpcode.Add:
                case DebugOpcode.Remove:
                case DebugOpcode.Step:
                case DebugOpcode.Resume:
                    // do nothing, this only matters on the debugger client
                    break;

                case DebugOpcode.Log:
                    {
                        var log = Serialization.Unserialize<LogCommand>(data);
                        this.OnLog?.Invoke(log.Message);
                        break;
                    }

                case DebugOpcode.Status:
                    {
                        var status = Serialization.Unserialize<StatusCommand>(data);
                        // TODO
                        break;
                    }

                default:
                    // TODO error handling / exception / disconnect
                    break;

            }
        }

        private void Send(byte[] data)
        {
            _stream.Write(data, 0, data.Length);
        }

        public void AddBreakpoint(Breakpoint bp)
        {
            var data = DebugUtils.EncodeMessage<Breakpoint>(DebugOpcode.Add, bp);
            Send(data);
        }

        public void RemoveBreakpoint(Breakpoint bp)
        {
            var data = DebugUtils.EncodeMessage<Breakpoint>(DebugOpcode.Remove, bp);
            Send(data);
        }

        public void Step()
        {
            var data = DebugUtils.EncodeMessage(DebugOpcode.Step);
            Send(data);
        }

        public void Resume()
        {
            var data = DebugUtils.EncodeMessage(DebugOpcode.Resume);
            Send(data);
        }
    }
}
