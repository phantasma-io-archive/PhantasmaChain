using System;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.CodeGen.Core.Nodes
{
    public class IfNode : StatementNode
    {
        public ExpressionNode expr;
        public StatementNode trueBranch;
        public StatementNode falseBranch;

        public IfNode(BlockNode owner) : base(owner)
        {
        }

        public override IEnumerable<CompilerNode> Nodes
        {
            get
            {
                yield return expr;
                yield return trueBranch;
                if (falseBranch != null) yield return falseBranch;
                yield break;
            }
        }

        public override List<Instruction> Emit(Compiler compiler)
        {
            var temp = this.expr.Emit(compiler);

            var first = this.trueBranch.Emit(compiler);
            Instruction end = new Instruction() { source = this, target = compiler.AllocLabel(), op = Instruction.Opcode.Label };
            Instruction middle = null;

            if (falseBranch != null)
            {
                middle = new Instruction() { source = this, target = compiler.AllocLabel(), op = Instruction.Opcode.Label };
                var second = this.falseBranch.Emit(compiler);

                temp.Add(new Instruction() { source = this, target = compiler.AllocLabel(), op = Instruction.Opcode.JumpIfTrue, a = temp.Last(), b = middle });
                temp.AddRange(second);

                temp.Add(new Instruction() { source = this, target = compiler.AllocLabel(), op = Instruction.Opcode.Jump, b = end});
                temp.Add(middle);
                temp.AddRange(first);
            }
            else
            {
                temp.Add(new Instruction() { source = this, target = compiler.AllocLabel(), op = Instruction.Opcode.JumpIfFalse, a = temp.Last(), b = end });
                temp.AddRange(first);
            }

            temp.Add(end);

            return temp;
        }
    }
}