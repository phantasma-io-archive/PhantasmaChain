using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Phantasma.IO;
using Phantasma.Numerics;

namespace Phantasma.VM.Utils
{
    public class ScriptBuilder
    {
        private MemoryStream stream;
        private BinaryWriter writer;

        private Dictionary<int, string> _jumpLocations = new Dictionary<int, string>();
        private Dictionary<string, int> _labelLocations = new Dictionary<string, int>();

        public ScriptBuilder()
        {
            this.stream = new MemoryStream();
            this.writer = new BinaryWriter(stream);
        }

        public void Emit(Opcode opcode, byte[] bytes = null)
        {
            //var ofs = (int)stream.Position;
            writer.Write((byte)opcode);

            if (bytes != null)
            {
                writer.Write(bytes);
            }
        }

        public void EmitPush(byte reg)
        {
            Emit(Opcode.PUSH);
            writer.Write((byte)reg);
        }

        public void EmitPop(byte reg)
        {
            Emit(Opcode.POP);
            writer.Write((byte)reg);
        }

        public void EmitExtCall(string method, byte reg = 0)
        {
            EmitLoad(reg, method);
            Emit(Opcode.EXTCALL);
            writer.Write((byte)reg);
        }

        public void EmitLoad(byte reg, byte[] bytes, VMType type = VMType.Bytes)
        {
            Emit(Opcode.LOAD);
            writer.Write((byte)reg);
            writer.Write((byte)type);

            writer.WriteVarInt(bytes.Length);
            writer.Write(bytes);
        }

        public void EmitLoad(byte reg, string val)
        {
            var bytes = Encoding.UTF8.GetBytes(val);
            EmitLoad(reg, bytes, VMType.String);
        }

        public void EmitLoad(byte reg, BigInteger val)
        {
            var bytes = val.ToByteArray(includeSignInArray: true);
            EmitLoad(reg, bytes, VMType.Number);
        }

        public void EmitLoad(byte reg, bool val)
        {
            var bytes = new byte[1] { (byte)(val ? 1 : 0) };
            EmitLoad(reg, bytes, VMType.Bool);
        }

        public void EmitLoad(byte reg, Enum val)
        {
            var temp = Convert.ToUInt32(val);
            var bytes = BitConverter.GetBytes(temp);
            EmitLoad(reg, bytes, VMType.Enum);
        }

        public void EmitMove(byte src_reg, byte dst_reg)
        {
            Emit(Opcode.MOVE);
            writer.Write((byte)src_reg);
            writer.Write((byte)dst_reg);
        }

        public void EmitCopy(byte src_reg, byte dst_reg)
        {
            Emit(Opcode.COPY);
            writer.Write((byte)src_reg);
            writer.Write((byte)dst_reg);
        }

        public void EmitLabel(string label)
        {
            Emit(Opcode.NOP);
            _labelLocations[label] = (int)stream.Position;
        }

        public void EmitJump(Opcode opcode, string label, byte reg = 0)
        {
            switch (opcode)
            {
                case Opcode.JMP:
                case Opcode.JMPIF:
                case Opcode.JMPNOT:
                    Emit(opcode);
                    break;

                default:
                    throw new Exception("Invalid jump opcode: " + opcode);
            }

            if (opcode != Opcode.JMP)
            {
                writer.Write(reg);
            }

            var ofs = (int)stream.Position;
            writer.Write((ushort)0);
            _jumpLocations[ofs] = label;
        }

        public void EmitCall(string label, byte regCount)
        {
            if (regCount<1 || regCount > VirtualMachine.MaxRegisterCount)
            {
                throw new ArgumentException("Invalid number of registers");
            }

            var ofs = (int)stream.Position;
            ofs += 2;
            Emit(Opcode.CALL);
            writer.Write((byte)regCount);
            writer.Write((ushort)0);

            _jumpLocations[ofs] = label;
        }

        public void EmitConditionalJump(Opcode opcode, byte src_reg, string label)
        {
            if (opcode != Opcode.JMPIF && opcode != Opcode.JMPNOT)
            {
                throw new ArgumentException("Opcode is not a conditional jump");
            }

            var ofs = (int)stream.Position;
            ofs += 2;

            Emit(opcode);
            writer.Write((byte)src_reg);
            writer.Write((ushort)0);
            _jumpLocations[ofs] = label;
        }

        public byte[] ToScript()
        {
            var script = stream.ToArray();

            // resolve jump offsets
            foreach (var entry in _jumpLocations)
            {
                var label = entry.Value;
                var labelOffset = (ushort)_labelLocations[label];
                var bytes = BitConverter.GetBytes(labelOffset);
                var targetOffset = entry.Key;

                for (int i = 0; i < 2; i++)
                {
                    script[targetOffset + i] = bytes[i];
                }
            }

            return script;
        }

        public void EmitVarBytes(long value)
        {
            writer.WriteVarInt(value);
        }

        public void EmitRaw(byte[] bytes)
        {
            writer.Write(bytes);
        }
    }
}
