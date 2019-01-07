using System.Collections.Generic;
using System.Linq;

namespace Phantasma.CodeGen.Core.Nodes
{
    public class VariableExpressionNode : ExpressionNode
    {
        public string identifier;

        public DeclarationNode declaration; // can resolved later

        public VariableExpressionNode(CompilerNode owner) : base(owner)
        {
        }

        public override IEnumerable<CompilerNode> Nodes => Enumerable.Empty<CompilerNode>();

        public override string ToString()
        {
            return base.ToString() + "=>" + this.identifier;
        }

        public override List<Instruction> Emit(Compiler compiler)
        {
            if (this.declaration == null)
            {
                this.declaration = ResolveIdentifier(this.identifier);
            }

            var varLocation = compiler.varMap[this.declaration.identifier];

            var temp = new List<Instruction>();
            temp.Add(new Instruction() { source = this, target = compiler.AllocRegister(), varName = varLocation, op = Instruction.Opcode.Assign});
            return temp;
        }

        public override TypeKind GetKind()
        {
            return TypeKind.Unknown; // TODO should return Kind of variable
        }
    }
}