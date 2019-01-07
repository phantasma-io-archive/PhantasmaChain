using System.Collections.Generic;

namespace Phantasma.CodeGen.Core.Nodes
{
    public abstract class StatementNode : CompilerNode
    {
        public StatementNode(CompilerNode owner) : base(owner)
        {
        }

        public abstract List<Instruction> Emit(Compiler compiler);

        public MethodNode FindParentMethod()
        {
            var node = Owner;
            while (node != null)
            {
                if (node is MethodNode)
                {
                    return (MethodNode)node;
                }

                node = node.Owner;
            }

            return null;
        }
    }
}