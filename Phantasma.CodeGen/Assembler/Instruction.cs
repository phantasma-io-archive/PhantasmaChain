using System.Linq;
using Phantasma.Numerics;
using Phantasma.VM.Utils;
using Phantasma.VM;
using Phantasma.Cryptography;

namespace Phantasma.CodeGen.Assembler
{
    internal class Instruction : Semanteme
    {
        private const string ERR_INCORRECT_NUMBER = "incorrect number of arguments";
        private const string ERR_INVALID_ARGUMENT = "invalid argument";
        private const string ERR_INVALID_CONTRACT = "invalid contract";
        private const string ERR_UNKNOWN_CONTRACT = "unknown contract";
        private const string ERR_SYNTAX_ERROR = "syntax error";

        public string[] Arguments;

        public Opcode Opcode;

        public override string ToString()
        {
            return this.Opcode.ToString();
        }

        public override void Process(ScriptBuilder sb)
        {
            switch (Opcode)
            {
                //1 reg
                case Opcode.PUSH:
                case Opcode.POP:
                case Opcode.INC:
                case Opcode.DEC:
                    Process1Reg(sb);
                    break;

                case Opcode.LOAD:
                    ProcessLoad(sb);
                    break;

                //2 reg
                case Opcode.SWAP:
                case Opcode.SIZE:
                case Opcode.NOT:
                case Opcode.SIGN:
                case Opcode.NEGATE:
                case Opcode.ABS:
                case Opcode.COPY:
                case Opcode.MOVE:
                    Process2Reg(sb);
                    break;

                //3 reg
                case Opcode.AND:
                case Opcode.OR:
                case Opcode.XOR:
                case Opcode.CAT:
                case Opcode.EQUAL:
                case Opcode.LT:
                case Opcode.GT:
                case Opcode.LTE:
                case Opcode.GTE:
                case Opcode.ADD:
                case Opcode.SUB:
                case Opcode.MUL:
                case Opcode.DIV:
                case Opcode.MOD:
                case Opcode.SHL:
                case Opcode.SHR:
                case Opcode.MIN:
                case Opcode.MAX:
                case Opcode.PUT:
                case Opcode.GET:
                    Process3Reg(sb);
                    break;

                case Opcode.EXTCALL:
                    ProcessExtCall(sb);
                    break;

                case Opcode.SUBSTR:
                case Opcode.LEFT:
                case Opcode.RIGHT:
                    ProcessRightLeft(sb);
                    break;

                case Opcode.CTX:
                    ProcessCtx(sb);
                    break;

                case Opcode.SWITCH:
                    ProcessSwitch(sb);
                    break;

                case Opcode.RET:
                    ProcessReturn(sb);
                    break;

                case Opcode.THROW:
                    ProcessThrow(sb);
                    break;

                case Opcode.JMPIF:
                case Opcode.JMPNOT:
                    ProcessJumpIf(sb);
                    break;

                case Opcode.JMP:
                    ProcessJump(sb);
                    break;

                case Opcode.CALL:
                    ProcessCall(sb);
                    break;

                default:
                    throw new CompilerException(LineNumber, ERR_SYNTAX_ERROR);
            }
        }

        private void ProcessSwitch(ScriptBuilder sb)
        {
            if (Arguments.Length != 1)
            {
                throw new CompilerException(LineNumber, ERR_INCORRECT_NUMBER);
            }

            byte dest_reg;
            if (Arguments[0].IsRegister())
            {
                dest_reg = Arguments[0].AsRegister();
            }
            else
            {
                throw new CompilerException(LineNumber, ERR_INVALID_ARGUMENT);
            }

            sb.Emit(this.Opcode, new byte[] { dest_reg });
        }

        private void ProcessCtx(ScriptBuilder sb)
        {
            if (Arguments.Length != 2) throw new CompilerException(LineNumber, ERR_INCORRECT_NUMBER);

            if (Arguments[0].IsRegister())
            {
                var dest_reg = Arguments[0].AsRegister();

                byte[] bytes = null;
                if (Arguments[1].IsBytes())
                {
                    bytes = Base16.Decode(Arguments[1]);
                }
                else
                if (Arguments[1].IsString())
                {
                    var name = Arguments[1].AsString();
                    // TODO FIXME!!!
                    /*NativeContractKind kind;
                    
                    if (System.Enum.TryParse<NativeContractKind>(name, true, out kind))
                    {
                        var contract = Chain.GetNativeContract(kind);
                        if (contract == null)
                        {
                            throw new CompilerException(LineNumber, ERR_INVALID_CONTRACT);
                        }
                        else
                        {
                            bytes = contract.Address.PublicKey;
                        }
                    }
                    else*/
                    {
                        throw new CompilerException(LineNumber, ERR_UNKNOWN_CONTRACT);
                    }
                }
                else
                {
                    throw new CompilerException(LineNumber, ERR_INVALID_ARGUMENT);
                }

                if (bytes.Length != Address.PublicKeyLength)
                {
                    throw new CompilerException(LineNumber, ERR_INVALID_ARGUMENT);
                }

                sb.Emit(this.Opcode, new byte[] { dest_reg }.Concat(bytes).ToArray());
            }
            else
            {
                throw new CompilerException(LineNumber, ERR_INVALID_ARGUMENT);
            }
        }

        private void ProcessRightLeft(ScriptBuilder sb)
        {
            if (Arguments.Length != 3) throw new CompilerException(LineNumber, ERR_INCORRECT_NUMBER);
            if (Arguments[0].IsRegister() && Arguments[1].IsRegister() && Arguments[2].IsNumber())
            {
                var src_reg = Arguments[0].AsRegister();
                var dest_reg = Arguments[1].AsRegister();
                var length = (int)Arguments[2].AsNumber();

                sb.Emit(this.Opcode, new byte[]
                {
                        src_reg,
                        dest_reg
                });

                sb.EmitVarBytes(length);
            }
            else
            {
                throw new CompilerException(LineNumber, ERR_INVALID_ARGUMENT);
            }
        }

        private void ProcessJumpIf(ScriptBuilder sb)
        {
            if (Arguments.Length != 2) throw new CompilerException(LineNumber, ERR_INCORRECT_NUMBER);
            if (Arguments[0].IsRegister() && Arguments[1].IsLabel())
            {
                var reg = Arguments[0].AsRegister();
                var label = Arguments[1].AsLabel();
                sb.EmitConditionalJump(this.Opcode, reg, label);
            }
            else
            {
                throw new CompilerException(LineNumber, ERR_INVALID_ARGUMENT);
            }
        }

        private void ProcessJump(ScriptBuilder sb)
        {
            if (Arguments.Length != 1)
            {
                throw new CompilerException(LineNumber, ERR_INCORRECT_NUMBER);
            }

            if (!Arguments[0].IsLabel())
            {
                throw new CompilerException(LineNumber, ERR_INVALID_ARGUMENT);
            }
            else
            {
                sb.EmitJump(Opcode.JMP, Arguments[0]);
            }
        }

        private void ProcessCall(ScriptBuilder sb)
        {
            if (Arguments.Length < 1 || Arguments.Length > 2)
            {
                throw new CompilerException(LineNumber, ERR_INCORRECT_NUMBER);
            }

            if (!Arguments[0].IsLabel())
            {
                throw new CompilerException(LineNumber, ERR_INVALID_ARGUMENT);
            }

            byte regCount = 0;

            if (Arguments.Length < 2)
            {
                regCount = VirtualMachine.DefaultRegisterCount;
            }
            else
            if (Arguments[1].IsNumber())
            {
                regCount = (byte)Arguments[1].AsNumber();
            }
            else
            {
                throw new CompilerException(LineNumber, ERR_INVALID_ARGUMENT);
            }

            sb.EmitCall(Arguments[0].AsLabel(), regCount);
        }

        private void ProcessExtCall(ScriptBuilder sb)
        {
            if (Arguments.Length != 1)
            {
                throw new CompilerException(LineNumber, ERR_INCORRECT_NUMBER);
            }

            if (Arguments[0].IsString())
            {
                var extCall = Arguments[0].AsString();

                if (string.IsNullOrEmpty(extCall))
                {
                    throw new CompilerException(LineNumber, ERR_INVALID_ARGUMENT);
                }

                sb.EmitExtCall(extCall);
            }
            else
            if (Arguments[0].IsRegister())
            {
                var reg = Arguments[0].AsRegister();
                sb.Emit(Opcode.EXTCALL, new byte[] { reg });
            }
            else
            {
                throw new CompilerException(LineNumber, ERR_INVALID_ARGUMENT);
            }
        }

        private void Process1Reg(ScriptBuilder sb)
        {
            if (Arguments.Length != 1)
            {
                throw new CompilerException(LineNumber, ERR_INCORRECT_NUMBER);
            }

            if (Arguments[0].IsRegister())
            {
                var reg = Arguments[0].AsRegister();
                sb.Emit(this.Opcode, new[]
                {
                    reg
                });
            }
            else
            {
                throw new CompilerException(LineNumber, ERR_INVALID_ARGUMENT);
            }
        }

        private void Process2Reg(ScriptBuilder sb)
        {
            if (Arguments.Length != 2)
            {
                throw new CompilerException(LineNumber, ERR_INCORRECT_NUMBER);
            }

            if (Arguments[0].IsRegister() && Arguments[1].IsRegister())
            {
                var src = Arguments[0].AsRegister();
                var dest = Arguments[1].AsRegister();
                sb.Emit(this.Opcode, new[] { src, dest });
            }
            else
            {
                throw new CompilerException(LineNumber, ERR_INVALID_ARGUMENT);
            }
        }

        private void Process3Reg(ScriptBuilder sb)
        {
            if (Arguments.Length <= 1 || Arguments.Length > 3) {
                throw new CompilerException(LineNumber, ERR_INCORRECT_NUMBER);
            }

            if (Arguments.Length == 2 && Arguments[0].IsRegister() && Arguments[1].IsRegister())
            {
                var src_a_reg = Arguments[0].AsRegister();
                var src_b_reg = Arguments[1].AsRegister();

                sb.Emit(this.Opcode, new[]
                {
                    src_a_reg,
                    src_b_reg,
                    src_a_reg
                });
            }
            else
            if (Arguments[0].IsRegister() && Arguments[1].IsRegister() && Arguments[2].IsRegister())
            {
                var src_a_reg = Arguments[0].AsRegister();
                var src_b_reg = Arguments[1].AsRegister();
                var src_c_reg = Arguments[2].AsRegister();

                sb.Emit(this.Opcode, new[]
                {
                    src_a_reg,
                    src_b_reg,
                    src_c_reg
                });
            }
            else
            {
                throw new CompilerException(LineNumber, ERR_INVALID_ARGUMENT);
            }
        }

        private void ProcessLoad(ScriptBuilder sb)
        {
            if (Arguments.Length != 2)
            {
                throw new CompilerException(LineNumber, ERR_INCORRECT_NUMBER);
            }

            if (Arguments[0].IsRegister())
            {
                var reg = Arguments[0].AsRegister();

                if (Arguments[1].IsNumber())
                {
                    sb.EmitLoad(reg, Arguments[1].AsNumber());
                }
                else
                if (Arguments[1].IsBytes())
                {
                    sb.EmitLoad(reg, Arguments[1].IsBytes());
                }
                else
                if (Arguments[1].IsBool())
                {
                    sb.EmitLoad(reg, Arguments[1].AsBool());
                }
                else
                if (Arguments[1].IsString())
                {
                    sb.EmitLoad(reg, Arguments[1].AsString());
                }
                else
                {
                    throw new CompilerException(LineNumber, ERR_INVALID_ARGUMENT);
                }
            }
            else
            {
                throw new CompilerException(LineNumber, ERR_INVALID_ARGUMENT);
            }
        }

        private void ProcessReturn(ScriptBuilder sb)
        {
            if (Arguments.Length == 0)
            {
                sb.Emit(this.Opcode);
            }
            else
            if (Arguments.Length == 1)
            {
                if (Arguments[0].IsRegister())
                {
                    var reg = Arguments[0].AsRegister();
                    sb.EmitPush(reg);
                    sb.Emit(this.Opcode);
                }
                else
                {
                    throw new CompilerException(LineNumber, ERR_INVALID_ARGUMENT);
                }
            }
            else
            {
                throw new CompilerException(LineNumber, ERR_INCORRECT_NUMBER);
            }
        }

        private void ProcessThrow(ScriptBuilder sb)
        {
            if (Arguments.Length == 0)
            {
                sb.Emit(this.Opcode);
                sb.EmitVarBytes(0);
            }
            else
            if (Arguments.Length == 1)
            {
                if (Arguments[0].IsBytes())
                {
                    var bytes = Arguments[0].AsBytes();
                    sb.Emit(this.Opcode);
                    sb.EmitVarBytes(bytes.Length);
                    sb.EmitRaw(bytes);
                }
                else
                {
                    throw new CompilerException(LineNumber, ERR_INVALID_ARGUMENT);
                }
            }
            else
            {
                throw new CompilerException(LineNumber, ERR_INCORRECT_NUMBER);
            }
        }

    }
}