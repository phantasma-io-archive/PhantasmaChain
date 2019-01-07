using System;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.CodeGen.Core.Nodes
{
    public class BlockNode : StatementNode
    {
        public List<DeclarationNode> declarations = new List<DeclarationNode>();
        public List<StatementNode> statements = new List<StatementNode>();

        public BlockNode(CompilerNode owner) : base(owner)
        {
        }

        public override DeclarationNode ResolveIdentifier(string identifier)
        {
            foreach (var decl in declarations)
            {
                if (decl.identifier == identifier)
                {
                    return decl;
                }
            }

            return base.ResolveIdentifier(identifier);
        }

        public override List<Instruction> Emit(Compiler compiler)
        {
            var list = new List<Instruction>();

            foreach (var st in statements)
            {
                var temp = st.Emit(compiler);
                list.AddRange(temp);
            }
            return list;
        }

        public override IEnumerable<CompilerNode> Nodes => declarations.Concat<CompilerNode>(statements);
    }
}