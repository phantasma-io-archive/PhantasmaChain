using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Phantasma.VM
{
    public enum ExecutionState
    {
        Running,
        Break,
        Fault,
        Halt
    }

    public abstract class VirtualMachine
    {
        public const int MaxRegisterCount = 32;

        public uint InstructionPointer { get; private set; }
        public ExecutionState State { get; private set; }

        private MachineValue[] registers = new MachineValue[MaxRegisterCount];

        private byte[] script;

        private Stack<MachineValue> valueStack = new Stack<MachineValue>();
        private Stack<uint> callStack = new Stack<uint>();

        public BigInteger gas { get; private set; }

        public VirtualMachine(byte[] script)
        {
            InstructionPointer = 0;
            State = ExecutionState.Running;

            this.gas = 0;
            this.script = script;
        }

        public abstract bool ExecuteInterop(string method);

        public MachineValue GetRegister(int index)
        {
            if (index<0 || index >= MaxRegisterCount)
            {
                throw new ArgumentException("Invalid index");
            }

            return registers[index];
        }

        public void Execute()
        {
            while (State == ExecutionState.Running)
            {
                this.Step();
            }
        }

        private void SetState(ExecutionState state)
        {
            this.State = state;
        }

        private byte Read8()
        {
            if (InstructionPointer >= script.Length)
            {
                throw new Exception("Outside of range");
            }

            var result = script[InstructionPointer];
            InstructionPointer++;
            return result;
        }

        private ushort Read16()
        {
            var a = Read8();
            var b = Read8();
            return (ushort)(a + b << 8);
        }

        private uint Read32()
        {
            var a = Read8();
            var b = Read8();
            var c = Read8();
            var d = Read8();
            return (ushort)(a + (b << 8) + (c << 16) + (d << 24));
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
            return (ushort)(a + (b << 8) + (c << 16) + (d << 24) + (e << 32) + (f << 40) + (g << 48) + (g << 56));
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

            if (val > max)
            {
                throw new Exception("Input exceed max");
            }

            return val;
        }

        private byte[] ReadBytes(int length)
        {
            if (InstructionPointer + length >= script.Length)
            {
                throw new Exception("Outside of range");
            }

            var result = new byte[length];
            for (int i= 0; i<length; i++)
            {
                result[i] = script[InstructionPointer];
                InstructionPointer++;
            }

            return result;
        }

        private void Expect(bool assertion)
        {
            if (!assertion)
            {
                throw new Exception("Assertion failed");
            }
        }

        public void Step()
        {
            try
            {
                var opcode = (Opcode)Read8();

                switch (opcode)
                {
                    case Opcode.NOP:
                        {
                            break;
                        }

                    case Opcode.COPY:
                        {
                            var src = Read8();
                            var dst = Read8();

                            Expect(src < MaxRegisterCount);
                            Expect(dst < MaxRegisterCount);

                            registers[dst] = registers[src];
                            break;
                        }

                    case Opcode.LOAD:
                        {
                            var dst = Read8();
                            var len = (int)ReadVar(0xFFF);

                            Expect(dst < MaxRegisterCount);

                            registers[dst].Data = ReadBytes(len);
                            break;
                        }

                    case Opcode.PUSH:
                        {
                            var src = Read8();
                            Expect(src < MaxRegisterCount);

                            valueStack.Push(registers[src]);
                            break;
                        }

                    case Opcode.POP:
                        {
                            var dst = Read8();

                            Expect(valueStack.Count > 0);
                            Expect(dst < MaxRegisterCount);

                            registers[dst] = valueStack.Pop();
                            break;
                        }

                    case Opcode.SWAP:
                        {
                            var src = Read8();
                            var dst = Read8();

                            Expect(src < MaxRegisterCount);
                            Expect(dst < MaxRegisterCount);

                            var temp = registers[src];
                            registers[src] = registers[dst];
                            registers[dst] = temp;

                            break;
                        }

                    case Opcode.CALL:
                        {
                            var ofs = Read16();
                            Expect(ofs < script.Length);

                            callStack.Push(InstructionPointer);
                            InstructionPointer = ofs;
                            break;
                        }

                    case Opcode.EXTCALL:
                        {
                            var len = Read8();
                            var bytes = ReadBytes(len);

                            var method = Encoding.ASCII.GetString(bytes);

                            if (!ExecuteInterop(method))
                            {
                                SetState(ExecutionState.Fault);
                                return;
                            }

                            break;
                        }

                    case Opcode.JMP:
                    case Opcode.JMPIF:
                    case Opcode.JMPNOT:
                        {
                            var ofs = (short)Read16(); 

                            if (opcode != Opcode.JMP)
                            {
                                var src = Read8();
                                Expect(src < MaxRegisterCount);

                                var status = registers[src].AsBool();

                                if (opcode == Opcode.JMPNOT)
                                {
                                    status = !status;
                                }

                                if (!status)
                                {
                                    break;
                                }
                            }

                            var newPos = (int)InstructionPointer + ofs;

                            Expect(newPos >= 0);
                            Expect(newPos < script.Length);

                            InstructionPointer = (uint)newPos;
                            break;
                        }

                    case Opcode.RET:
                        {
                            if (callStack.Count > 0)
                            {
                                var ofs = callStack.Pop();

                                Expect(ofs < script.Length);

                                InstructionPointer = ofs;
                            }
                            else
                            {
                                SetState(ExecutionState.Halt);
                            }
                            return;
                        }

                    case Opcode.CAT:
                        {
                            var src = Read8();
                            var dst = Read8();

                            Expect(src < MaxRegisterCount);
                            Expect(dst < MaxRegisterCount);

                            var A = registers[src];
                            var B = registers[dst];

                            if (!A.IsEmpty)
                            {
                                if (B.IsEmpty)
                                {
                                    registers[dst] = A;
                                }
                                else
                                {
                                    var result = new byte[A.Data.Length + B.Data.Length];
                                    Array.Copy(A.Data, result, A.Data.Length);
                                    Array.Copy(B.Data, 0, result, A.Data.Length, B.Data.Length);
                                }
                            }

                            break;
                        }

                    case Opcode.SUBSTR:
                        {
                            throw new NotImplementedException();
                        }

                    case Opcode.LEFT:
                        {
                            var src = Read8();
                            var len = (int)ReadVar(0xFFFF);

                            Expect(src < MaxRegisterCount);
                            Expect(len <= registers[src].Length);

                            var result = new byte[len];
                            Array.Copy(registers[src].Data, result, len);

                            registers[src].Data = result;
                            break;
                        }

                    case Opcode.RIGHT:
                        {
                            var src = Read8();
                            var len = (int)ReadVar(0xFFFF);

                            Expect(src < MaxRegisterCount);
                            Expect(len <= registers[src].Length);

                            var ofs = registers[src].Length - len;

                            var result = new byte[len];
                            Array.Copy(registers[src].Data, ofs, result, 0, len);

                            registers[src].Data = result;
                            break;
                        }

                    case Opcode.SIZE:
                        {
                            var src = Read8();
                            var dst = Read8();

                            Expect(src < MaxRegisterCount);
                            Expect(dst < MaxRegisterCount);

                            registers[dst].SetValue(registers[src].Length);
                            break;
                        }

                    case Opcode.INVERT:
                    case Opcode.AND:
                    case Opcode.OR:
                    case Opcode.XOR:
                        {
                            throw new NotImplementedException();
                        }

                    case Opcode.INC:
                        {
                            var dst = Read8();
                            Expect(dst < MaxRegisterCount);

                            var val = registers[dst].AsNumber();
                            registers[dst].SetValue(val + 1);

                            break;
                        }

                    case Opcode.DEC:
                        {
                            var dst = Read8();
                            Expect(dst < MaxRegisterCount);

                            var val = registers[dst].AsNumber();
                            registers[dst].SetValue(val - 1);

                            break;
                        }

                    case Opcode.SIGN:
                        {
                            var src = Read8();
                            var dst = Read8();

                            Expect(src < MaxRegisterCount);
                            Expect(dst < MaxRegisterCount);

                            var val = registers[src].AsNumber();

                            if (val == 0)
                            {
                                registers[dst].SetValue(0);
                            }
                            else
                            {
                                registers[dst].SetValue(val < 0 ? -1 : 1);
                            }

                            break;
                        }

                    case Opcode.NEGATE:
                        {
                            var dst = Read8();
                            Expect(dst < MaxRegisterCount);

                            var val = registers[dst].AsNumber();
                            registers[dst].SetValue(-val);

                            break;
                        }

                    case Opcode.ABS:
                        {
                            var dst = Read8();
                            Expect(dst < MaxRegisterCount);

                            var val = registers[dst].AsNumber();
                            registers[dst].SetValue(val<0?-val:val);

                            break;
                        }

                    case Opcode.ADD:
                        {
                            var src = Read8();
                            var dst = Read8();

                            Expect(src < MaxRegisterCount);
                            Expect(dst < MaxRegisterCount);

                            var a = registers[src].AsNumber();
                            var b = registers[dst].AsNumber();

                            registers[dst].SetValue(a + b);

                            break;
                        }

                    case Opcode.SUB:
                        {
                            var dst = Read8();
                            var src = Read8();

                            Expect(src < MaxRegisterCount);
                            Expect(dst < MaxRegisterCount);

                            var a = registers[src].AsNumber();
                            var b = registers[dst].AsNumber();

                            registers[dst].SetValue(b - a);

                            break;
                        }


                    case Opcode.MUL:
                        {
                            var src = Read8();
                            var dst = Read8();

                            Expect(src < MaxRegisterCount);
                            Expect(dst < MaxRegisterCount);

                            var a = registers[src].AsNumber();
                            var b = registers[dst].AsNumber();

                            registers[dst].SetValue(a * b);

                            break;
                        }

                    case Opcode.DIV:
                        {
                            var src = Read8();
                            var dst = Read8();

                            Expect(src < MaxRegisterCount);
                            Expect(dst < MaxRegisterCount);

                            var a = registers[src].AsNumber();
                            var b = registers[dst].AsNumber();

                            registers[dst].SetValue(a / b);

                            break;
                        }

                    case Opcode.MOD:
                        {
                            var src = Read8();
                            var dst = Read8();

                            Expect(src < MaxRegisterCount);
                            Expect(dst < MaxRegisterCount);

                            var a = registers[src].AsNumber();
                            var b = registers[dst].AsNumber();

                            registers[dst].SetValue(a % b);

                            break;
                        }

                    case Opcode.SHR:
                        {
                            var dst = Read8();
                            var bits = Read8();

                            Expect(dst < MaxRegisterCount);

                            var val = registers[dst].AsNumber();
                            val >>= bits;

                            registers[dst].SetValue(val);

                            break;
                        }

                    case Opcode.SHL:
                        {
                            var dst = Read8();
                            var bits = Read8();

                            Expect(dst < MaxRegisterCount);

                            var val = registers[dst].AsNumber();
                            val <<= bits;

                            registers[dst].SetValue(val);

                            break;
                        }

                    case Opcode.EQUAL:
                    case Opcode.LT:
                    case Opcode.GT:
                        {
                            var srcA = Read8();
                            var srcB = Read8();
                            var dst = Read8();

                            Expect(srcA < MaxRegisterCount);
                            Expect(srcB < MaxRegisterCount);
                            Expect(dst < MaxRegisterCount);

                            var a = registers[srcA].AsNumber();
                            var b = registers[srcA].AsNumber();

                            bool result;
                            switch (opcode)
                            {
                                case Opcode.EQUAL: result = (a == b); break;
                                case Opcode.LT: result = (a < b); break;
                                case Opcode.GT: result = (a > b); break;
                                case Opcode.LTE: result = (a <= b); break;
                                case Opcode.GTE: result = (a >= b); break;
                                default:
                                    {
                                        SetState(ExecutionState.Fault);
                                        return;
                                    }
                            }

                            registers[dst].SetValue(result);
                            break;
                        }

                    case Opcode.MIN:
                        {
                            var srcA = Read8();
                            var srcB = Read8();
                            var dst = Read8();

                            Expect(srcA < MaxRegisterCount);
                            Expect(srcB < MaxRegisterCount);
                            Expect(dst < MaxRegisterCount);

                            var a = registers[srcA].AsNumber();
                            var b = registers[srcA].AsNumber();

                            registers[dst].SetValue(a < b ? a : b);
                            break;
                        }

                    case Opcode.MAX:
                        {
                            var srcA = Read8();
                            var srcB = Read8();
                            var dst = Read8();

                            Expect(srcA < MaxRegisterCount);
                            Expect(srcB < MaxRegisterCount);
                            Expect(dst < MaxRegisterCount);

                            var a = registers[srcA].AsNumber();
                            var b = registers[srcA].AsNumber();

                            registers[dst].SetValue(a > b ? a : b);
                            break;
                        }
   
                default:
                        {
                            SetState(ExecutionState.Fault);
                            return;
                        }
                }


            }
            catch
            {
                SetState(ExecutionState.Fault);
            }
        }
    }
}
