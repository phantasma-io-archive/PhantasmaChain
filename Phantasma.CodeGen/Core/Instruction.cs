using Phantasma.CodeGen.Core.Nodes;

namespace Phantasma.CodeGen.Core
{
    public class Instruction
    {
        public enum Opcode
        {
            Label,
            Return,
            Assign,
            Pop,
            Push,
            Add,
            Sub,
            Mul,
            Div,
            Mod,
            Shr,
            Shl,
            Negate,
            Equals,
            LessThan,
            GreaterThan,
            LessOrEqualThan,
            GreaterOrEqualThan,
            Or,
            And,
            Not,
            Inc,
            Dec,
            Jump,
            JumpIfTrue,
            JumpIfFalse,
            Call,
        }

        public CompilerNode source;
        public string target;
        public LiteralExpressionNode literal;
        public string varName; // HACK: Fix me later
        public Instruction a;
        public Instruction b;
        public Opcode op;

        public override string ToString()
        {
            if (op == Opcode.Label)
            {
                return $"@{target}:";
            }

            if (op == Opcode.Call)
            {
                return $"call @{target}";
            }

            if (op == Opcode.Jump)
            {
                return $"goto {b}";
            }

            if (op == Opcode.JumpIfFalse)
            {
                return $"if !{a.target} goto {b}";
            }

            if (op == Opcode.JumpIfTrue)
            {
                return $"if {a.target} goto {b}";
            }

            if (op == Opcode.Pop)
            {
                return $"pop {target}";
            }

            if (op == Opcode.Push)
            {
                return $"push {target}";
            }

            if (op == Opcode.Return)
            {
                return $"ret";
            }

            if (op == Opcode.Assign && literal != null)
            {
                if (literal.kind == LiteralKind.String)
                {
                    return target + $" := \"{literal.value}\"";
                }
                return target + $" := {literal.value}";
            }

            if (op == Opcode.Assign && varName != null)
            {
                return target + $" := {varName}";
            }

            string symbol;
            switch (op)
            {
                case Opcode.Add: symbol = "+"; break;
                case Opcode.Sub: symbol = "-"; break;
                case Opcode.Mul: symbol = "*"; break;
                case Opcode.Div: symbol = "/"; break;
                case Opcode.Mod: symbol = "%"; break;
                case Opcode.Not: symbol = "!"; break;
                case Opcode.Inc: symbol = "++"; break;
                case Opcode.Dec: symbol = "--"; break;
                case Opcode.Equals: symbol = "=="; break;
                case Opcode.LessOrEqualThan: symbol = "<="; break;
                case Opcode.GreaterOrEqualThan: symbol = ">="; break;
                case Opcode.LessThan: symbol = "<"; break;
                case Opcode.GreaterThan: symbol = ">"; break;
                default: symbol = null; break;
            }

            var result = target;
            if (b != null)
            {
                result += $" := {a.target} {symbol} {b.target}";
            }
            else
            if (a != null)
            {
                if (op == Opcode.Assign)
                {
                    result += $" := {a.target}";
                }
                else
                if (symbol != null)
                {
                    if (symbol == "-")
                    {
                        op = Opcode.Negate;
                    }

                    result += $" := {symbol}{a.target}";
                }
                else
                {
                    result += $" := {op}{a.target}";
                }
            }
            else
            {
                result += $" := {symbol}()";
            }

            return result;
        }
    }
}
