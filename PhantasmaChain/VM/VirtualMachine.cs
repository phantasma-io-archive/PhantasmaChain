using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Phantasma.Core;
using Phantasma.Utils;

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

        public readonly Stack<VMObject> stack = new Stack<VMObject>();
        public readonly Stack<ExecutionFrame> frames = new Stack<ExecutionFrame>();
        public ExecutionFrame currentFrame { get; private set; }

        private Dictionary<byte[], ExecutionContext> _contextList = new Dictionary<byte[], ExecutionContext>(new ByteArrayComparer());

        public readonly byte[] entryScript;

        public ExecutionContext currentContext { get; private set; }
        public byte[] entryPublicKey { get; private set; }

        public BigInteger gas { get; private set; }

        public VirtualMachine(byte[] script)
        {
            InstructionPointer = 0;
            State = ExecutionState.Running;

            this.currentContext = new ExecutionContext(script);
            this.entryPublicKey = script.ScriptToPublicKey();
            _contextList[this.entryPublicKey] = this.currentContext;

            PushFrame(currentContext);

            this.gas = 0;
            this.entryScript = script;
        }

        private void PushFrame(ExecutionContext context)
        {
            var frame = new ExecutionFrame(InstructionPointer, context);
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
            this.InstructionPointer = currentFrame.Offset;
            this.currentContext = currentFrame.Context;

            Expect(InstructionPointer < currentContext.Script.Length);
        }

        public abstract ExecutionState ExecuteInterop(string method);
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
            if (InstructionPointer >= currentContext.Script.Length)
            {
                throw new Exception("Outside of range");
            }

            var result = currentContext.Script[InstructionPointer];
            InstructionPointer++;
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
            if (InstructionPointer + length >= currentContext.Script.Length)
            {
                throw new Exception("Outside of range");
            }

            var result = new byte[length];
            for (int i= 0; i<length; i++)
            {
                result[i] = currentContext.Script[InstructionPointer];
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

                    // args: byte src_reg, byte dest_reg
                    case Opcode.MOVE:
                        {
                            var src = Read8();
                            var dst = Read8();

                            Expect(src < MaxRegisterCount);
                            Expect(dst < MaxRegisterCount);

                            currentFrame.registers[dst] = currentFrame.registers[src];
                            break;
                        }

                    // args: byte src_reg, byte dest_reg
                    case Opcode.COPY:
                        {
                            var src = Read8();
                            var dst = Read8();

                            Expect(src < MaxRegisterCount);
                            Expect(dst < MaxRegisterCount);

                            currentFrame.registers[dst].Copy(currentFrame.registers[src]);
                            break;
                        }

                    // args: byte dst_reg, byte type, var length, var data_bytes
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

                    // args: byte src_reg
                    case Opcode.PUSH:
                        {
                            var src = Read8();
                            Expect(src < MaxRegisterCount);

                            stack.Push(currentFrame.registers[src]);
                            break;
                        }

                    // args: byte dest_reg
                    case Opcode.POP:
                        {
                            var dst = Read8();

                            Expect(stack.Count > 0);
                            Expect(dst < MaxRegisterCount);

                            currentFrame.registers[dst] = stack.Pop();
                            break;
                        }

                    // args: byte src_reg, byte dest_reg
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

                    // args: ushort offset
                    case Opcode.CALL:
                        {
                            var ofs = Read16();
                            Expect(ofs < currentContext.Script.Length);

                            PushFrame(currentContext);

                            InstructionPointer = ofs;
                            break;
                        }

                    // args: byte length, var method_name
                    case Opcode.EXTCALL:
                        {
                            var len = Read8();
                            var bytes = ReadBytes(len);

                            var method = Encoding.ASCII.GetString(bytes);

                            var state = ExecuteInterop(method);
                            if (state != ExecutionState.Running)
                            {
                                return;
                            }

                            break;
                        }

                    // args: ushort offset, byte src_reg
                    // NOTE: JMP only has offset arg, not the rest
                    case Opcode.JMP:
                    case Opcode.JMPIF:
                    case Opcode.JMPNOT:
                        {
                            var newPos = (short)Read16();

                            Expect(newPos >= 0);
                            Expect(newPos < currentContext.Script.Length);

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

                    // args: none
                    case Opcode.RET:
                        {
                            if (frames.Count > 1)
                            {                                
                                PopFrame();
                            }
                            else
                            {
                                SetState(ExecutionState.Halt);
                            }
                            return;
                        }

                    // args: byte src_a_reg, byte src_b_reg, byte dest_reg
                    case Opcode.CAT:
                        {
                            var srcA = Read8();
                            var srcB = Read8();
                            var dst = Read8();

                            Expect(srcA < MaxRegisterCount);
                            Expect(srcB < MaxRegisterCount);
                            Expect(dst < MaxRegisterCount);

                            var A = currentFrame.registers[srcA];
                            var B = currentFrame.registers[srcB];

                            if (!A.IsEmpty)
                            {
                                if (B.IsEmpty)
                                {
                                    currentFrame.registers[dst].Copy(A);
                                }
                                else
                                {
                                    var bytesA = A.AsByteArray();
                                    var bytesB = B.AsByteArray();

                                    var result = new byte[bytesA.Length + bytesB.Length];
                                    Array.Copy(bytesA, result, bytesA.Length);
                                    Array.Copy(bytesB, 0, result, bytesA.Length, bytesB.Length);

                                    currentFrame.registers[dst].SetValue(result, VMType.Bytes);
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

                    // args: byte src_reg, byte dest_reg, var length
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

                    // args: byte src_reg, byte dest_reg, byte length
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

                    // args: byte src_reg, byte dest_reg
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

                    // args: byte src_reg, byte dest_reg
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

                    // args: byte src_a_reg, byte src_b_reg, byte dest_reg
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
                            var b = currentFrame.registers[srcB].AsBool();

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

                    // args: byte src_a_reg, byte src_b_reg, byte dest_reg
                    case Opcode.EQUAL:
                        {
                            var srcA = Read8();
                            var srcB = Read8();
                            var dst = Read8();

                            Expect(srcA < MaxRegisterCount);
                            Expect(srcB < MaxRegisterCount);
                            Expect(dst < MaxRegisterCount);

                            var a = currentFrame.registers[srcA];
                            var b = currentFrame.registers[srcB];

                            var result = a.Equals(b);
                            currentFrame.registers[dst].SetValue(result);

                            break;
                        }

                    // args: byte src_a_reg, byte src_b_reg, byte dest_reg
                    case Opcode.LT:
                    case Opcode.GT:
                    case Opcode.LTE:
                    case Opcode.GTE:
                        {
                            var srcA = Read8();
                            var srcB = Read8();
                            var dst = Read8();

                            Expect(srcA < MaxRegisterCount);
                            Expect(srcB < MaxRegisterCount);
                            Expect(dst < MaxRegisterCount);

                            var a = currentFrame.registers[srcA].AsNumber();
                            var b = currentFrame.registers[srcB].AsNumber();

                            bool result;
                            switch (opcode)
                            {
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

                    // args: byte reg
                    case Opcode.INC:
                        {
                            var dst = Read8();
                            Expect(dst < MaxRegisterCount);

                            var val = currentFrame.registers[dst].AsNumber();
                            currentFrame.registers[dst].SetValue(val + 1);

                            break;
                        }

                    // args: byte reg
                    case Opcode.DEC:
                        {
                            var dst = Read8();
                            Expect(dst < MaxRegisterCount);

                            var val = currentFrame.registers[dst].AsNumber();
                            currentFrame.registers[dst].SetValue(val - 1);

                            break;
                        }

                    // args: byte src_reg, byte dest_reg
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

                    // args: byte src_reg, byte dest_reg
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

                    // args: byte src_reg, byte dest_reg
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
                        {
                            var srcA = Read8();
                            var srcB = Read8();
                            var dst = Read8();

                            Expect(srcA < MaxRegisterCount);
                            Expect(srcB < MaxRegisterCount);
                            Expect(dst < MaxRegisterCount);

                            var a = currentFrame.registers[srcA].AsNumber();
                            var b = currentFrame.registers[srcB].AsNumber();

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

                    // args: byte src_reg, byte dest_reg, byte key
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

                    // args: byte src_reg, byte dest_reg, byte key
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

                    // args: byte dest_reg, var key
                    case Opcode.CTX:
                        {
                            var dst = Read8();
                            var key = ReadBytes(KeyPair.PublicKeyLength);

                            Expect(dst < MaxRegisterCount);

                            ExecutionContext context;

                            if (_contextList.ContainsKey(key))
                            {
                                context = _contextList[key];
                            }
                            else
                            {
                                context = LoadContext(key);
                            }

                            if (context == null)
                            {
                                SetState(ExecutionState.Fault);
                            }

                            currentFrame.registers[dst].SetValue(context);

                            break;
                        }

                    // args: var key
                    case Opcode.SWITCH:
                        {
                            var key = ReadBytes(KeyPair.PublicKeyLength);

                            if (!_contextList.ContainsKey(key))
                            {
                                SetState(ExecutionState.Fault);
                            }

                            PushFrame(_contextList[key]);
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
