using System;
using System.Text;
using Phantasma.Numerics;
using Phantasma.Storage;
using Phantasma.Core.Log;
using System.Net.Sockets;

namespace Phantasma.VM.Debug
{
    public enum DebugOpcode
    {
        Invalid,
        Log,
        Status,
        Add,
        Remove,
        Step,
        Resume,
    }

    public struct DebugCommand
    {
        public readonly DebugOpcode Opcode;
        public readonly byte[] Data;

        public DebugCommand(DebugOpcode opcode, byte[] data)
        {
            Opcode = opcode;
            Data = data;
        }
    }

    public struct LogCommand
    {
        public readonly string Message;

        public LogCommand(string message)
        {
            Message = message;
        }
    }

    public struct StatusCommand
    {
        public readonly string Context;
        public uint Offset;

        public StatusCommand(string context, uint offset)
        {
            Context = context;
            Offset = offset;
        }
    }

    public static class DebugUtils
    {
        public static byte[] EncodeMessage<T>(DebugOpcode opcode, T data)
        {
            var bytes = Serialization.Serialize(data);
            return EncodeMessage(opcode, bytes);
        }

        public static byte[] EncodeMessage(DebugOpcode opcode)
        {
            return EncodeMessage(opcode, new byte[0]);
        }

        public static byte[] EncodeMessage(DebugOpcode opcode, byte[] data)
        {
            var hex = Base16.Encode(data);
            var message = $"[{opcode}|{hex}]";

            byte[] msg = Encoding.ASCII.GetBytes(message);
            return msg;
        }

        public static bool ReceiveCommand(NetworkStream stream, Action<DebugOpcode, byte[]> callback)
        {
            var bytes = new Byte[256];

            string result = null;

            var _buffer = new StringBuilder();
    
            // Loop to receive all the data sent by the client
            do
            {
                var read = stream.Read(bytes, 0, bytes.Length);
                if (read == 0)
                {
                    return false;
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

            if (string.IsNullOrEmpty(result))
            {
                return false;
            }

            bool validStr = result.StartsWith("[") && result.EndsWith("]");

            if (!validStr)
            {
                return false;
            }

            result = result.Substring(1, result.Length - 2);
            var temp = result.Split(',');

            DebugOpcode opcode;

            if (!Enum.TryParse<DebugOpcode>(temp[0], out opcode))
            {
                return false;
            }

            byte[] data = temp.Length == 1 || string.IsNullOrEmpty(temp[1]) ? new byte[0] : Base16.Decode(temp[1]);

            callback(opcode, data);
            return true;
        }
    }
}
