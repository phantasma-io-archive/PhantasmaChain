using System;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.CodeGen.Core.Nodes
{
    public class StackAssignmentNode : StatementNode
    {
        public string identifier;
        public DeclarationNode declaration;

        public StackAssignmentNode(CompilerNode owner) : base(owner)
        {
        }

        public override IEnumerable<CompilerNode> Nodes
        {
            get
            {
                if (declaration != null)
                {
                    yield return declaration;
                }

                yield break;
            }
        }

        public override string ToString()
        {
            return base.ToString() + "=>" + this.identifier;
        }

        public override List<Instruction> Emit(Compiler compiler)
        {
            var reg = compiler.AllocRegister();
            compiler.varMap[declaration.identifier] = reg;

            var temp = new List<Instruction>();
            temp.Add(new Instruction() { source = this, target = reg, op = Instruction.Opcode.Pop });
            return temp;
        }
    }
}