using System.Text;
using System.Numerics;
using Phantasma.Numerics;

namespace Phantasma.VM
{
    public struct Instruction
    {
        public uint Offset;
        public Opcode Opcode;
        public object[] Args;

        private static void AppendRegister(StringBuilder sb, object reg)
        {
            sb.Append($" r{(byte)reg}");
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Offset.ToString().PadLeft(3, '0'));
            sb.Append(": ");
            sb.Append(Opcode.ToString());

            switch (Opcode)
            {
                case Opcode.MOVE:
                case Opcode.COPY:
                case Opcode.SWAP:
                case Opcode.SIZE:
                case Opcode.SIGN:
                case Opcode.NOT:
                case Opcode.NEGATE:
                case Opcode.ABS:
                case Opcode.CTX:
                    {
                        AppendRegister(sb, Args[0]);
                        sb.Append(',');
                        AppendRegister(sb, Args[1]);
                        break;
                    }

                case Opcode.ADD:
                case Opcode.SUB:
                case Opcode.MUL:
                case Opcode.DIV:
                case Opcode.MOD:
                case Opcode.SHR:
                case Opcode.SHL:
                case Opcode.MIN:
                case Opcode.MAX:
                    {
                        AppendRegister(sb, Args[0]);
                        sb.Append(',');
                        AppendRegister(sb, Args[1]);
                        sb.Append(',');
                        AppendRegister(sb, Args[2]);
                        break;
                    }

                case Opcode.POP:
                case Opcode.PUSH:
                case Opcode.EXTCALL:
                case Opcode.DEC:
                case Opcode.INC:
                case Opcode.SWITCH:
                    {
                        AppendRegister(sb, Args[0]);
                        break;
                    }

                case Opcode.CALL:
                    {
                        sb.Append((byte)Args[0]);
                        sb.Append(',');
                        sb.Append(' ');
                        sb.Append((ushort)Args[1]);
                        break;
                    }

                // args: byte dst_reg, byte type, var length, var data_bytes
                case Opcode.LOAD:
                    {
                        AppendRegister(sb, Args[0]);
                        sb.Append(',');
                        sb.Append(' ');

                        var type = (VMType)Args[1];
                        var bytes = (byte[])Args[2];
                        switch (type)
                        {
                            case VMType.String:
                                sb.Append('"');
                                sb.Append(Encoding.UTF8.GetString(bytes));
                                sb.Append('"');
                                break;

                            case VMType.Number:
                                sb.Append(new BigInteger(bytes));
                                break;

                            default:
                                sb.Append(bytes.Encode());
                                break;
                        }

                        break;
                    }

            }

            return sb.ToString();
        }
    }
}
