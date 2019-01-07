using System;
using System.Collections.Generic;

namespace Phantasma.CodeGen.Core.Nodes
{
    public class WhileNode : StatementNode
    {
        public ExpressionNode expr;
        public StatementNode body;

        public WhileNode(BlockNode owner) : base(owner)
        {
        }

        public override IEnumerable<CompilerNode> Nodes
        {
            get
            {
                yield return expr;
                yield return body;
            }
        }

        public override List<Instruction> Emit(Compiler compiler)
        {
            throw new NotImplementedException();
        }
    }
}