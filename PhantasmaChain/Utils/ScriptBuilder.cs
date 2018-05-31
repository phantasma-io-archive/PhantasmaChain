using Phantasma.VM;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Phantasma.Utils
{
    public class ScriptBuilder
    {
        private List<byte> data = new List<byte>();

        public void Emit(Opcode opcode)
        {
            data.Add((byte)opcode);
        }

        public void EmitPush(int reg)
        {
            Emit(Opcode.PUSH);
            data.Add((byte)reg);
        }

        public void EmitCall(string method)
        {
            var bytes = Encoding.ASCII.GetBytes(method);

            Emit(Opcode.CALL);
            data.Add((byte)bytes.Length);

            foreach (var entry in bytes)
            {
                data.Add(entry);
            }
        }

        public void Emit(int reg, byte[] bytes)
        {
            Emit(Opcode.LOAD);
            data.Add((byte)reg);

            data.Add((byte)bytes.Length);

            foreach (var entry in bytes)
            {
                data.Add(entry);
            }
        }

        public void Emit(int reg, string val)
        {
            var bytes = Encoding.UTF8.GetBytes(val);
            Emit(reg, bytes);
        }

        public void Emit(int reg, BigInteger val)
        {
            var bytes = val.ToByteArray();
            Emit(reg, bytes);
        }

        public void Emit(int reg, bool val)
        {
            var bytes = new byte[1] { (byte)(val ? 1 : 0) };
            Emit(reg, bytes);
        }

        public byte[] ToScript()
        {
            return data.ToArray();
        }
    }
}
