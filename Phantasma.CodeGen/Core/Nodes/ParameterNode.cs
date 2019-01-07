using System;
using System.Collections.Generic;

namespace Phantasma.CodeGen.Core.Nodes
{
    public class ParameterNode : CompilerNode
    {
        public DeclarationNode decl;

        public ParameterNode(MethodNode owner) : base(owner)
        {
            owner.parameters.Add(this);
        }

        public override string ToString()
        {
            return base.ToString() + "=>" + this.decl.ToString();
        }

        public override IEnumerable<CompilerNode> Nodes
        {
            get
            {
                yield return decl;
                yield break;
            }
        }
    }
}