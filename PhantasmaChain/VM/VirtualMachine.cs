using Phantasma.Contracts.Types;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

        private Stack<MachineValue> stack = new Stack<MachineValue>();

        public BigInteger gas { get; private set; }

        public VirtualMachine(byte[] script)
        {
            InstructionPointer = 0;
            State = ExecutionState.Running;

            this.gas = 0;
            this.script = script;
        }

        public abstract bool ExecuteInterop(string method);

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

        private byte ReadByte()
        {
            if (InstructionPointer >= script.Length)
            {
                throw new Exception("Outside of range");
            }

            var result = script[InstructionPointer];
            InstructionPointer++;
            return result;
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

                var opcode = (Opcode)ReadByte();

                switch (opcode)
                {
                    case Opcode.NOP:
                        {
                            break;
                        }

                    case Opcode.COPY:
                        {
                            var src = ReadByte();
                            var dst = ReadByte();

                            Expect(src < MaxRegisterCount);
                            Expect(dst < MaxRegisterCount);

                            registers[dst] = registers[src];
                            break;
                        }


                    case Opcode.LOAD:
                        {
                            var dst = ReadByte();
                            var len = ReadByte(); // TODO: Must be var int, not byte

                            Expect(dst < MaxRegisterCount);

                            registers[dst].Data = ReadBytes(len);
                            break;
                        }

                    case Opcode.PUSH:
                        {
                            var src = ReadByte();
                            Expect(src < MaxRegisterCount);

                            stack.Push(registers[src]);
                            break;
                        }

                    case Opcode.POP:
                        {
                            var dst = ReadByte();

                            Expect(stack.Count > 0);
                            Expect(dst < MaxRegisterCount);

                            registers[dst] = stack.Pop();
                            break;
                        }

                    case Opcode.SWAP:
                        {
                            var src = ReadByte();
                            var dst = ReadByte();

                            Expect(src < MaxRegisterCount);
                            Expect(dst < MaxRegisterCount);

                            var temp = registers[src];
                            registers[src] = registers[dst];
                            registers[dst] = temp;

                            break;
                        }

                    case Opcode.CALL:
                        {
                            var len = ReadByte(); // TODO read varint
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
                            var ofs = (sbyte)ReadByte(); // TODO: Must be 16bits instead of 8

                            if (opcode != Opcode.JMP)
                            {
                                var src = ReadByte();
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
                            SetState(ExecutionState.Halt);
                            return;
                        }

                    case Opcode.CAT:
                        {
                            var src = ReadByte();
                            var dst = ReadByte();

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
                            var src = ReadByte();
                            var len = ReadByte(); // TODO: var int

                            Expect(src < MaxRegisterCount);
                            Expect(len <= registers[src].Length);

                            var result = new byte[len];
                            Array.Copy(registers[src].Data, result, len);

                            registers[src].Data = result;
                            break;
                        }

                    case Opcode.RIGHT:
                        {
                            var src = ReadByte();
                            var len = ReadByte(); // TODO: var int

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
                            var src = ReadByte();
                            var dst = ReadByte();

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
                            var dst = ReadByte();
                            Expect(dst < MaxRegisterCount);

                            var val = registers[dst].AsNumber();
                            registers[dst].SetValue(val + 1);

                            break;
                        }

                    case Opcode.DEC:
                        {
                            var dst = ReadByte();
                            Expect(dst < MaxRegisterCount);

                            var val = registers[dst].AsNumber();
                            registers[dst].SetValue(val - 1);

                            break;
                        }

                    case Opcode.SIGN:
                        {
                            var src = ReadByte();
                            var dst = ReadByte();

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
                            var dst = ReadByte();
                            Expect(dst < MaxRegisterCount);

                            var val = registers[dst].AsNumber();
                            registers[dst].SetValue(-val);

                            break;
                        }

                    case Opcode.ABS:
                        {
                            var dst = ReadByte();
                            Expect(dst < MaxRegisterCount);

                            var val = registers[dst].AsNumber();
                            registers[dst].SetValue(val<0?-val:val);

                            break;
                        }

                    case Opcode.ADD:
                        {
                            var src = ReadByte();
                            var dst = ReadByte();

                            Expect(src < MaxRegisterCount);
                            Expect(dst < MaxRegisterCount);

                            var a = registers[src].AsNumber();
                            var b = registers[dst].AsNumber();

                            registers[dst].SetValue(a + b);

                            break;
                        }

                    case Opcode.SUB:
                        {
                            var dst = ReadByte();
                            var src = ReadByte();

                            Expect(src < MaxRegisterCount);
                            Expect(dst < MaxRegisterCount);

                            var a = registers[src].AsNumber();
                            var b = registers[dst].AsNumber();

                            registers[dst].SetValue(b - a);

                            break;
                        }


                    case Opcode.MUL:
                        {
                            var src = ReadByte();
                            var dst = ReadByte();

                            Expect(src < MaxRegisterCount);
                            Expect(dst < MaxRegisterCount);

                            var a = registers[src].AsNumber();
                            var b = registers[dst].AsNumber();

                            registers[dst].SetValue(a * b);

                            break;
                        }

                    case Opcode.DIV:
                        {
                            var src = ReadByte();
                            var dst = ReadByte();

                            Expect(src < MaxRegisterCount);
                            Expect(dst < MaxRegisterCount);

                            var a = registers[src].AsNumber();
                            var b = registers[dst].AsNumber();

                            registers[dst].SetValue(a / b);

                            break;
                        }

                    case Opcode.MOD:
                        {
                            var src = ReadByte();
                            var dst = ReadByte();

                            Expect(src < MaxRegisterCount);
                            Expect(dst < MaxRegisterCount);

                            var a = registers[src].AsNumber();
                            var b = registers[dst].AsNumber();

                            registers[dst].SetValue(a % b);

                            break;
                        }

                    case Opcode.SHR:
                        {
                            var dst = ReadByte();
                            var bits = ReadByte();

                            Expect(dst < MaxRegisterCount);

                            var val = registers[dst].AsNumber();
                            val >>= bits;

                            registers[dst].SetValue(val);

                            break;
                        }

                    case Opcode.SHL:
                        {
                            var dst = ReadByte();
                            var bits = ReadByte();

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
                            var srcA = ReadByte();
                            var srcB = ReadByte();
                            var dst = ReadByte();

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
                            var srcA = ReadByte();
                            var srcB = ReadByte();
                            var dst = ReadByte();

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
                            var srcA = ReadByte();
                            var srcB = ReadByte();
                            var dst = ReadByte();

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
