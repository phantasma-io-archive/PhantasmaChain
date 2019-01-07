using System;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.CodeGen.Core.Nodes
{
    public class ReturnNode : StatementNode
    {
        public ExpressionNode expr;

        public ReturnNode(CompilerNode owner) : base(owner)
        {
        }

        public override List<Instruction> Emit(Compiler compiler)
        {
            var temp = expr.Emit(compiler);
            var last = temp.Last();
            temp.Add(new Instruction() { source = this, target = last.target, a = last, op = Instruction.Opcode.Push });
            temp.Add(new Instruction() { source = this, target = null, a = null, op = Instruction.Opcode.Return });
            return temp;
        }

        public override IEnumerable<CompilerNode> Nodes
        {
            get
            {
                yield return expr;
                yield break;
            }
        }

        protected override bool ValidateSemantics()
        {
            var method = FindParentMethod();

            if (method == null)
            {
                return false;
            }

            return method.returnType.Kind == expr.GetKind();
        }
    }
}