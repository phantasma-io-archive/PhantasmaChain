using System;
using System.Text;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.VM.Debug
{
    public enum DebugOpcode
    {
        Invalid,
        Log,
        Add,
        Delete,
        Step,
        Resume,
    }

    public struct DebugCommand
    {
        public readonly DebugOpcode Opcode;
        public readonly string[] Args;

        public DebugCommand(DebugOpcode opcode, IEnumerable<string> args)
        {
            Opcode = opcode;
            Args = args.ToArray();
        }

        public static DebugCommand FromString(string str)
        {
            var split = str.Split(' ');
            DebugOpcode opcode;
            if (Enum.TryParse<DebugOpcode>(split[0], out opcode))
            {
                return new DebugCommand(opcode, split.Skip(1));
            }

            return new DebugCommand(DebugOpcode.Invalid, Enumerable.Empty<string>());
        }
    }

    public class Debugger : IDisposable
    {
        public const int Port = 7671;

        private TcpClient _client;
        private NetworkStream _stream;

        public Debugger(string host = "localhost", int port = Port)
        {
            _client = new TcpClient(host, port);
            // Get a client stream for reading and writing.
            _stream = _client.GetStream();
        }

        private void Send(string message)
        {
            // Translate the passed message into ASCII and store it as a Byte array.
            var data = Encoding.ASCII.GetBytes(message);

            // Send the message to the connected TcpServer.
            _stream.Write(data, 0, data.Length);
        }

        private string Receive()
        {
            var data = new byte[256];

            var bytesRead = _stream.Read(data, 0, data.Length);
            var responseData = Encoding.ASCII.GetString(data, 0, bytesRead);

            return responseData;
        }

        public void Dispose()
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

        public bool AddBreakpoint(string contextName, uint offset)
        {
            throw new NotImplementedException();
        }

        public bool RemoveBreakpoint(string contextName, uint offset)
        {
            throw new NotImplementedException();
        }

        public void Step()
        {
            throw new NotImplementedException();
        }

        public void Resume()
        {
            throw new NotImplementedException();
        }
    }
}
