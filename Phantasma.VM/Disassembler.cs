using Phantasma.Core;
using Phantasma.Cryptography;
using System.Collections.Generic;

namespace Phantasma.VM
{
    public class Disassembler
    {
        private uint _instructionPointer;
        private byte[] _script;

        private List<Instruction> _instructions;
        public IEnumerable<Instruction> Instructions => _instructions;

        public Disassembler(byte[] script)
        {
            this._script = script;
            this._instructionPointer = 0;
            this._instructions = GetInstructions();
        }

        private List<Instruction> GetInstructions()
        {
            var result = new List<Instruction>();

            while (_instructionPointer < _script.Length)
            {
                var temp = new Instruction();
                temp.Opcode = (Opcode)Read8();

                switch (temp.Opcode)
                {
                    // args: byte src_reg, byte dest_reg
                    case Opcode.MOVE:
                    case Opcode.COPY:
                    case Opcode.SWAP:
                    case Opcode.SIZE:
                    case Opcode.SIGN:
                    case Opcode.NOT:
                    case Opcode.NEGATE:
                    case Opcode.ABS:
                        {
                            var src = Read8();
                            var dst = Read8();

                            temp.Args = new object[] { src, dst };
                            break;
                        }

                    // args: byte dst_reg, byte type, var length, var data_bytes
                    case Opcode.LOAD:
                        {
                            var dst = Read8();
                            var type = (VMType)Read8();
                            var len = (int)ReadVar(0xFFF);

                            var bytes = ReadBytes(len);

                            temp.Args = new object[] { dst, type, bytes };

                            break;
                        }

                    // args: byte src_reg
                    case Opcode.POP:
                    case Opcode.PUSH:
                    case Opcode.EXTCALL:
                        {
                            var src = Read8();
                            temp.Args = new object[] { src };
                            break;
                        }

                    // args: ushort offset, byte regCount
                    case Opcode.CALL:
                        {
                            var count = Read8();
                            var ofs = Read16();
                            temp.Args = new object[] { count, ofs };
                            break;
                        }

                    // args: ushort offset, byte src_reg
                    // NOTE: JMP only has offset arg, not the rest
                    case Opcode.JMP:
                    case Opcode.JMPIF:
                    case Opcode.JMPNOT:
                        {
                            if (temp.Opcode == Opcode.JMP)
                            {
                                var newPos = (short)Read16();
                                temp.Args = new object[] { newPos };
                            }
                            else
                            {
                                var src = Read8();
                                var newPos = (short)Read16();
                                temp.Args = new object[] { src, newPos };
                            }
                            break;
                        }

                    // args: var length, var bytes
                    case Opcode.THROW:
                        {
                            var len = (int)ReadVar(1024);

                            byte[] bytes;
                            if (len > 0)
                            {
                                bytes = ReadBytes(len);
                            }
                            else
                            {
                                bytes = new byte[0];
                            }

                            temp.Args = new object[] { len, bytes };
                            break;
                        }


                    // args: byte src_a_reg, byte src_b_reg, byte dest_reg
                    case Opcode.AND:
                    case Opcode.OR:
                    case Opcode.XOR:
                    case Opcode.CAT:
                    case Opcode.EQUAL:
                    case Opcode.LT:
                    case Opcode.GT:
                    case Opcode.LTE:
                    case Opcode.GTE:
                        {
                            var srcA = Read8();
                            var srcB = Read8();
                            var dst = Read8();

                            temp.Args = new object[] { srcA, srcB, dst };
                            break;
                        }

                    // args: byte src_reg, byte dest_reg, var length
                    case Opcode.LEFT:
                    case Opcode.RIGHT:
                        {
                            var src = Read8();
                            var dst = Read8();
                            var len = (int)ReadVar(0xFFFF);

                            temp.Args = new object[] { src, dst, len};
                            break;
                        }

                    // args: byte reg
                    case Opcode.INC:
                    case Opcode.DEC:
                    case Opcode.THIS:
                    case Opcode.SWITCH:
                        {
                            var dst = Read8();
                            temp.Args = new object[] { dst };
                            break;
                        }

                    // args: byte src_a_reg, byte src_b_reg, byte dest_reg
                    case Opcode.ADD:
                    case Opcode.SUB:
                    case Opcode.MUL:
                    case Opcode.DIV:
                    case Opcode.MOD:
                    case Opcode.SHR:
                    case Opcode.SHL:
                    case Opcode.MIN:
                    case Opcode.MAX:
                    case Opcode.PUT:
                    case Opcode.GET:
                        {
                            var srcA = Read8();
                            var srcB = Read8();
                            var dst = Read8();
                            temp.Args = new object[] { srcA, srcB, dst };
                            break;
                        }

                    // args: byte dest_reg, var key
                    case Opcode.CTX:
                        {
                            var dst = Read8();
                            var key = ReadBytes(Address.PublicKeyLength);
                            temp.Args = new object[] { dst, key };
                            break;
                        }

                    default:
                        {
                            temp.Args = new object[0];
                            break;
                        }
                }

                result.Add(temp);
            }

            return result;
        }

        #region IO 
        private byte Read8()
        {
            Throw.If(_instructionPointer >= this._script.Length, "Outside of range");

            var result = this._script[_instructionPointer];
            _instructionPointer++;
            return result;
        }

        private ushort Read16()
        {
            var a = Read8();
            var b = Read8();
            return (ushort)(a + (b >> 8));
        }

        private uint Read32()
        {
            var a = Read8();
            var b = Read8();
            var c = Read8();
            var d = Read8();
            return (uint)(a + (b << 8) + (c << 16) + (d << 24));
        }

        private ulong Read64()
        {
            var a = Read8();
            var b = Read8();
            var c = Read8();
            var d = Read8();
            var e = Read8();
            var f = Read8();
            var g = Read8();
            var h = Read8();
            return (ulong)(a + (b << 8) + (c << 16) + (d << 24) + (e << 32) + (f << 40) + (g << 48) + (g << 56));
        }

        private ulong ReadVar(ulong max)
        {
            byte n = Read8();

            ulong val;

            switch (n)
            {
                case 0xFD: val = Read16(); break;
                case 0xFE: val = Read32(); break;
                case 0xFF: val = Read64(); break;
                default: val = n; break;
            }

            Throw.If(val > max, "Input exceed max");

            return val;
        }

        private byte[] ReadBytes(int length)
        {
            Throw.If(_instructionPointer + length >= this._script.Length, "Outside of range");

            var result = new byte[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = this._script[_instructionPointer];
                _instructionPointer++;
            }

            return result;
        }
        #endregion

    }


}
