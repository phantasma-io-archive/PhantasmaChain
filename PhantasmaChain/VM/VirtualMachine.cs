using Phantasma.Core;
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

        private byte[] script;

        public readonly Stack<VMObject> valueStack = new Stack<VMObject>();
        public readonly Stack<uint> callStack = new Stack<uint>();

        private Stack<ExecutionFrame> frames = new Stack<ExecutionFrame>();
        public ExecutionFrame currentFrame { get; private set; }

        private List<ExecutionContext> _contextList = new List<ExecutionContext>();

        public ExecutionContext currentContext { get; private set; }

        public BigInteger gas { get; private set; }

        public VirtualMachine(byte[] script)
        {
            InstructionPointer = 0;
            State = ExecutionState.Running;

            this.currentContext = new ExecutionContext(script);

            this.gas = 0;
            this.script = script;
        }

        private void PushFrame()
        {
            var frame = new ExecutionFrame();
            frames.Push(frame);
            this.currentFrame = frame;
        }

        private void PopFrame()
        {
            if (frames.Count < 2)
            {
                throw new Exception("Not enough frames available");
            }

            frames.Pop();
            this.currentFrame = frames.Peek();
        }

        public abstract bool ExecuteInterop(string method);
        public abstract ExecutionContext LoadContext(byte[] key);

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

                    case Opcode.MOVE:
                        {
                            var src = Read8();
                            var dst = Read8();

                            Expect(src < MaxRegisterCount);
                            Expect(dst < MaxRegisterCount);

                            currentFrame.registers[dst] = currentFrame.registers[src];
                            break;
                        }

                    case Opcode.COPY:
                        {
                            var src = Read8();
                            var dst = Read8();

                            Expect(src < MaxRegisterCount);
                            Expect(dst < MaxRegisterCount);

                            currentFrame.registers[dst].Copy(currentFrame.registers[src]);
                            break;
                        }

                    case Opcode.LOAD:
                        {
                            var dst = Read8();
                            var type = (VMType)Read8();
                            var len = (int)ReadVar(0xFFF);

                            Expect(dst < MaxRegisterCount);

                            var bytes = ReadBytes(len);
                            currentFrame.registers[dst].SetValue(bytes, type);

                            break;
                        }

                    case Opcode.PUSH:
                        {
                            var src = Read8();
                            Expect(src < MaxRegisterCount);

                            valueStack.Push(currentFrame.registers[src]);
                            break;
                        }

                    case Opcode.POP:
                        {
                            var dst = Read8();

                            Expect(valueStack.Count > 0);
                            Expect(dst < MaxRegisterCount);

                            currentFrame.registers[dst] = valueStack.Pop();
                            break;
                        }

                    case Opcode.SWAP:
                        {
                            var src = Read8();
                            var dst = Read8();

                            Expect(src < MaxRegisterCount);
                            Expect(dst < MaxRegisterCount);

                            var temp = currentFrame.registers[src];
                            currentFrame.registers[src] = currentFrame.registers[dst];
                            currentFrame.registers[dst] = temp;

                            break;
                        }

                    case Opcode.CALL:
                        {
                            var ofs = Read16();
                            Expect(ofs < script.Length);

                            PushFrame();

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
                            var newPos = (short)Read16();

                            Expect(newPos >= 0);
                            Expect(newPos < script.Length);

                            if (opcode != Opcode.JMP)
                            {
                                var src = Read8();
                                Expect(src < MaxRegisterCount);

                                var status = currentFrame.registers[src].AsBool();

                                if (opcode == Opcode.JMPNOT)
                                {
                                    status = !status;
                                }

                                if (!status)
                                {
                                    break;
                                }
                            }

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

                                PopFrame();
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

                            var A = currentFrame.registers[src];
                            var B = currentFrame.registers[dst];

                            if (!A.IsEmpty)
                            {
                                if (B.IsEmpty)
                                {
                                    currentFrame.registers[dst].Copy(A);
                                }
                                else
                                {
                                    var srcA = A.AsByteArray();
                                    var srcB = B.AsByteArray();

                                    var result = new byte[srcA.Length + srcB.Length];
                                    Array.Copy(srcA, result, srcA.Length);
                                    Array.Copy(srcB, 0, result, srcA.Length, srcB.Length);
                                }
                            }
                            else
                            {
                                if (B.IsEmpty)
                                {
                                    currentFrame.registers[dst] = new VMObject();
                                }
                                else
                                {
                                    currentFrame.registers[dst].Copy(B);
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
                            var dst = Read8();
                            var len = (int)ReadVar(0xFFFF);

                            Expect(src < MaxRegisterCount);
                            Expect(dst < MaxRegisterCount);

                            var src_array = currentFrame.registers[src].AsByteArray();
                            Expect(len <= src_array.Length);

                            var result = new byte[len];
                            
                            Array.Copy(src_array, result, len);

                            currentFrame.registers[dst].SetValue(result, VMType.Bytes);
                            break;
                        }

                    case Opcode.RIGHT:
                        {
                            var src = Read8();
                            var dst = Read8();
                            var len = (int)ReadVar(0xFFFF);

                            Expect(src < MaxRegisterCount);
                            Expect(dst < MaxRegisterCount);

                            var src_array = currentFrame.registers[src].AsByteArray();
                            Expect(len <= src_array.Length);

                            var ofs = src_array.Length - len;

                            var result = new byte[len];
                            Array.Copy(src_array, ofs, result, 0, len);

                            currentFrame.registers[dst].SetValue(result, VMType.Bytes);
                            break;
                        }

                    case Opcode.SIZE:
                        {
                            var src = Read8();
                            var dst = Read8();

                            Expect(src < MaxRegisterCount);
                            Expect(dst < MaxRegisterCount);

                            var src_array = currentFrame.registers[src].AsByteArray();
                            currentFrame.registers[dst].SetValue(src_array.Length);
                            break;
                        }

                    case Opcode.NOT:
                        {
                            var src = Read8();
                            var dst = Read8();

                            Expect(src < MaxRegisterCount);
                            Expect(dst < MaxRegisterCount);

                            var val = currentFrame.registers[src].AsBool();

                            currentFrame.registers[dst].SetValue(!val);
                            break;
                        }

                    case Opcode.AND:
                    case Opcode.OR:
                    case Opcode.XOR:
                        {
                            var srcA = Read8();
                            var srcB = Read8();
                            var dst = Read8();

                            Expect(srcA < MaxRegisterCount);
                            Expect(srcB < MaxRegisterCount);
                            Expect(dst < MaxRegisterCount);

                            var a = currentFrame.registers[srcA].AsBool();
                            var b = currentFrame.registers[srcA].AsBool();

                            bool result;
                            switch (opcode)
                            {
                                case Opcode.AND: result = (a && b); break;
                                case Opcode.OR: result = (a || b); break;
                                case Opcode.XOR: result = (a ^ b); break;
                                default:
                                    {
                                        SetState(ExecutionState.Fault);
                                        return;
                                    }
                            }

                            currentFrame.registers[dst].SetValue(result);
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

                            var a = currentFrame.registers[srcA].AsNumber();
                            var b = currentFrame.registers[srcA].AsNumber();

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

                            currentFrame.registers[dst].SetValue(result);
                            break;
                        }

                    case Opcode.INC:
                        {
                            var dst = Read8();
                            Expect(dst < MaxRegisterCount);

                            var val = currentFrame.registers[dst].AsNumber();
                            currentFrame.registers[dst].SetValue(val + 1);

                            break;
                        }

                    case Opcode.DEC:
                        {
                            var dst = Read8();
                            Expect(dst < MaxRegisterCount);

                            var val = currentFrame.registers[dst].AsNumber();
                            currentFrame.registers[dst].SetValue(val - 1);

                            break;
                        }

                    case Opcode.SIGN:
                        {
                            var src = Read8();
                            var dst = Read8();

                            Expect(src < MaxRegisterCount);
                            Expect(dst < MaxRegisterCount);

                            var val = currentFrame.registers[src].AsNumber();

                            if (val == 0)
                            {
                                currentFrame.registers[dst].SetValue(0);
                            }
                            else
                            {
                                currentFrame.registers[dst].SetValue(val < 0 ? -1 : 1);
                            }

                            break;
                        }

                    case Opcode.NEGATE:
                        {
                            var src = Read8();
                            var dst = Read8();
                            Expect(src < MaxRegisterCount);
                            Expect(dst < MaxRegisterCount);

                            var val = currentFrame.registers[src].AsNumber();
                            currentFrame.registers[dst].SetValue(-val);

                            break;
                        }

                    case Opcode.ABS:
                        {
                            var src = Read8();
                            var dst = Read8();
                            Expect(src < MaxRegisterCount);
                            Expect(dst < MaxRegisterCount);

                            var val = currentFrame.registers[src].AsNumber();
                            currentFrame.registers[dst].SetValue(val<0?-val:val);

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
                            var srcA = Read8();
                            var srcB = Read8();
                            var dst = Read8();

                            Expect(srcA < MaxRegisterCount);
                            Expect(srcB < MaxRegisterCount);
                            Expect(dst < MaxRegisterCount);

                            var a = currentFrame.registers[srcA].AsNumber();
                            var b = currentFrame.registers[srcA].AsNumber();

                            BigInteger result;

                            switch (opcode)
                            {
                                case Opcode.ADD: result = a + b; break;
                                case Opcode.SUB: result = a - b; break;
                                case Opcode.MUL: result = a * b; break;
                                case Opcode.DIV: result = a / b; break;
                                case Opcode.MOD: result = a % b; break;
                                case Opcode.SHR: result = a >> (int)b; break;
                                case Opcode.SHL: result = a << (int)b; break;
                                case Opcode.MIN: result = a < b ? a : b; break;
                                case Opcode.MAX: result = a > b ? a : b; break;
                                default:
                                    {
                                        SetState(ExecutionState.Fault);
                                        return;
                                    }
                            }

                            currentFrame.registers[dst].SetValue(result);
                            break;
                        }

                    case Opcode.PUT:
                        {
                            var src = Read8();
                            var dst = Read8();
                            var key = Read8();

                            Expect(src < MaxRegisterCount);
                            Expect(dst < MaxRegisterCount);
                            Expect(key < MaxRegisterCount);

                            currentFrame.registers[dst].SetKey(currentFrame.registers[key].AsString(), currentFrame.registers[src]);

                            break;
                        }

                    case Opcode.GET:
                        {
                            var src = Read8();
                            var dst = Read8();
                            var key = Read8();

                            Expect(src < MaxRegisterCount);
                            Expect(dst < MaxRegisterCount);
                            Expect(key < MaxRegisterCount);

                            currentFrame.registers[dst] = currentFrame.registers[src].GetKey(currentFrame.registers[key].AsString());

                            break;
                        }

                    case Opcode.CTX:
                        {
                            var dst = Read8();
                            var key = ReadBytes(KeyPair.PublicKeyLength);

                            Expect(dst < MaxRegisterCount);

                            var context = LoadContext(key);

                            if (context == null)
                            {
                                SetState(ExecutionState.Fault);
                            }

                            currentFrame.registers[dst].SetValue(context);

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
