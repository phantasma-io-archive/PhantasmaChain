using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Phantasma.Core;
using Phantasma.Core.Types;
using Phantasma.Storage;
using Phantasma.Numerics;
using Phantasma.Storage.Utils;

namespace Phantasma.VM.Utils
{
    public class ScriptBuilder
    {
        private MemoryStream stream;
        private BinaryWriter writer;

        private Dictionary<int, string> _jumpLocations = new Dictionary<int, string>();
        private Dictionary<string, int> _labelLocations = new Dictionary<string, int>();

        public int CurrentSize => (int)writer.BaseStream.Position;

        public ScriptBuilder()
        {
            this.stream = new MemoryStream();
            this.writer = new BinaryWriter(stream);
        }

        public ScriptBuilder Emit(Opcode opcode, byte[] bytes = null)
        {
            //var ofs = (int)stream.Position;
            writer.Write((byte)opcode);

            if (bytes != null)
            {
                writer.Write(bytes);
            }

            return this;
        }

        public ScriptBuilder EmitPush(byte reg)
        {
            Emit(Opcode.PUSH);
            writer.Write((byte)reg);
            return this;
        }

        public ScriptBuilder EmitPop(byte reg)
        {
            Emit(Opcode.POP);
            writer.Write((byte)reg);
            return this;
        }

        public ScriptBuilder EmitExtCall(string method, byte reg = 0)
        {
            EmitLoad(reg, method);
            Emit(Opcode.EXTCALL);
            writer.Write((byte)reg);
            return this;
        }

        public ScriptBuilder EmitLoad(byte reg, byte[] bytes, VMType type = VMType.Bytes)
        {
            Throw.If(bytes.Length > 0xFFFF, "tried to load too much data");

            Emit(Opcode.LOAD);
            writer.Write((byte)reg);
            writer.Write((byte)type);

            writer.WriteVarInt(bytes.Length);
            writer.Write(bytes);
            return this;
        }

        public ScriptBuilder EmitLoad(byte reg, string val)
        {
            var bytes = Encoding.UTF8.GetBytes(val);
            EmitLoad(reg, bytes, VMType.String);
            return this;
        }

        public ScriptBuilder EmitLoad(byte reg, BigInteger val)
        {
            var bytes = val.ToSignedByteArray();
            EmitLoad(reg, bytes, VMType.Number);
            return this;
        }

        public ScriptBuilder EmitLoad(byte reg, bool val)
        {
            var bytes = new byte[1] { (byte)(val ? 1 : 0) };
            EmitLoad(reg, bytes, VMType.Bool);
            return this;
        }

        public ScriptBuilder EmitLoad(byte reg, Enum val)
        {
            var temp = Convert.ToUInt32(val);
            var bytes = BitConverter.GetBytes(temp);
            EmitLoad(reg, bytes, VMType.Enum);
            return this;
        }

        public ScriptBuilder EmitLoad(byte reg, Timestamp val)
        {
            var bytes = BitConverter.GetBytes(val.Value);
            EmitLoad(reg, bytes, VMType.Timestamp);
            return this;
        }

        public ScriptBuilder EmitLoad(byte reg, ISerializable val)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    val.SerializeData(writer);
                }

                var bytes = stream.ToArray();
                EmitLoad(reg, bytes, VMType.Bytes);
                return this;
            }
        }

        public ScriptBuilder EmitMove(byte src_reg, byte dst_reg)
        {
            Emit(Opcode.MOVE);
            writer.Write((byte)src_reg);
            writer.Write((byte)dst_reg);
            return this;
        }

        public ScriptBuilder EmitCopy(byte src_reg, byte dst_reg)
        {
            Emit(Opcode.COPY);
            writer.Write((byte)src_reg);
            writer.Write((byte)dst_reg);
            return this;
        }

        public ScriptBuilder EmitLabel(string label)
        {
            Emit(Opcode.NOP);
            _labelLocations[label] = (int)stream.Position;
            return this;
        }

        public ScriptBuilder EmitJump(Opcode opcode, string label, byte reg = 0)
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
            return this;
        }

        public ScriptBuilder EmitCall(string label, byte regCount)
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
            return this;
        }

        public ScriptBuilder EmitConditionalJump(Opcode opcode, byte src_reg, string label)
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
            return this;
        }

        public ScriptBuilder EmitVarBytes(long value)
        {
            writer.WriteVarInt(value);
            return this;
        }

        public ScriptBuilder EmitRaw(byte[] bytes)
        {
            writer.Write(bytes);
            return this;
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
    }
}
