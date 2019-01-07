using System;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.CodeGen.Core.Nodes
{
    public class AssignmentNode : StatementNode
    {
        public string identifier;
        public ExpressionNode expr;

        public AssignmentNode(CompilerNode owner) : base(owner)
        {
        }

        public override IEnumerable<CompilerNode> Nodes
        {
            get
            {
                yield return expr;
                yield break;
            }
        }

        public override string ToString()
        {
            return base.ToString() + "=>" + this.identifier;
        }

        public override List<Instruction> Emit(Compiler compiler)
        {
            var temp = expr.Emit(compiler);
            temp.Add(new Instruction() { source = this, target = this.identifier, a = temp.Last(), op = Instruction.Opcode.Assign});
            return temp;
        }
    }
}