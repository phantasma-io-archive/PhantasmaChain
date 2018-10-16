using Phantasma.Numerics;
using System.Text;

namespace Phantasma.VM
{
    public struct Instruction
    {
        public Opcode Opcode;
        public object[] args;

        private static void AppendRegister(StringBuilder sb, object reg)
        {
            sb.Append($" r{(byte)reg}");
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
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
                    {
                        AppendRegister(sb, args[0]);
                        sb.Append(',');
                        AppendRegister(sb, args[1]);
                        break;
                    }

                case Opcode.POP:
                case Opcode.PUSH:
                case Opcode.EXTCALL:
                    {
                        AppendRegister(sb, args[0]);
                        break;
                    }

                case Opcode.CALL:
                    {
                        sb.Append((byte)args[0]);
                        sb.Append(',');
                        sb.Append(' ');
                        sb.Append((ushort)args[1]);
                        break;
                    }

                // args: byte dst_reg, byte type, var length, var data_bytes
                case Opcode.LOAD:
                    {
                        AppendRegister(sb, args[0]);
                        sb.Append(',');
                        sb.Append(' ');

                        var type = (VMType)args[1];
                        var bytes = (byte[])args[2];
                        switch (type)
                        {
                            case VMType.String:
                                sb.Append('"');
                                sb.Append(Encoding.UTF8.GetString(bytes));
                                sb.Append('"');
                                break;

                            default:
                                sb.Append(Base16.Encode(bytes));
                                break;
                        }

                        break;
                    }

            }

            return sb.ToString();
        }
    }
}
