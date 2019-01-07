using System.Collections.Generic;
using System.Linq;

namespace Phantasma.CodeGen.Core.Nodes
{
    public class CallNode: StatementNode
    {
        public MethodNode method;

        public CallNode(CompilerNode owner) : base(owner)
        {
        }

        public override IEnumerable<CompilerNode> Nodes => Enumerable.Empty<CompilerNode>();

        public override List<Instruction> Emit(Compiler compiler)
        {
            var temp = new List<Instruction>();
            temp.Add(new Instruction() { source = this, target = method.name, op = Instruction.Opcode.Call });
            return temp;
        }
    }
}
