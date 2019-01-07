using System.Collections.Generic;
using System.Linq;

namespace Phantasma.CodeGen.Core.Nodes
{
    public class ExitNode : StatementNode
    {
        public ExitNode(CompilerNode owner) : base(owner)
        {
        }

        public override List<Instruction> Emit(Compiler compiler)
        {
            var temp = new List<Instruction>();
            temp.Add(new Instruction() { source = this, target = null, a = null, op = Instruction.Opcode.Return });
            return temp;
        }

        public override IEnumerable<CompilerNode> Nodes => Enumerable.Empty<CompilerNode>();
    }
}