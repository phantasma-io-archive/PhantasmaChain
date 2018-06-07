using Phantasma.VM;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Phantasma.Utils
{
    public class ScriptBuilder
    {
        private List<byte> data = new List<byte>();

        public void Patch(int offset, byte val)
        {
            data[offset] = val;
        }

        public void Patch(int offset, byte[] bytes)
        {
            for (int i = 0; i < bytes.Length; i++)
            {
                Patch(offset + i, bytes[i]);
            }
        }

        public void Patch(int offset, ushort val)
        {
            var bytes = BitConverter.GetBytes(val);
            Patch(offset, bytes);
        }

        public int Emit(Opcode opcode)
        {
            var ofs = data.Count;
            data.Add((byte)opcode);
            return ofs;
        }

        public void EmitPush(int reg)
        {
            Emit(Opcode.PUSH);
            data.Add((byte)reg);
        }

        public void EmitCall(string method)
        {
            var bytes = Encoding.ASCII.GetBytes(method);

            Emit(Opcode.EXTCALL);
            data.Add((byte)bytes.Length);

            foreach (var entry in bytes)
            {
                data.Add(entry);
            }
        }

        public void Emit(int reg, byte[] bytes, VMType type = VMType.Bytes)
        {
            Emit(Opcode.LOAD);
            data.Add((byte)reg);
            data.Add((byte)type);

            data.Add((byte)bytes.Length);

            foreach (var entry in bytes)
            {
                data.Add(entry);
            }
        }

        public void Emit(int reg, string val)
        {
            var bytes = Encoding.UTF8.GetBytes(val);
            Emit(reg, bytes, VMType.String);
        }

        public void Emit(int reg, BigInteger val)
        {
            var bytes = val.ToByteArray();
            Emit(reg, bytes, VMType.Number);
        }

        public void Emit(int reg, bool val)
        {
            var bytes = new byte[1] { (byte)(val ? 1 : 0) };
            Emit(reg, bytes, VMType.Bool);
        }

        public byte[] ToScript()
        {
            return data.ToArray();
        }
    }
}
