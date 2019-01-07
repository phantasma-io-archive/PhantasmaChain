using System.Collections.Generic;
using System.Linq;

namespace Phantasma.CodeGen.Core.Nodes
{
    public class ImportNode : CompilerNode
    {
        public string reference;

        public ImportNode(ModuleNode owner) : base(owner)
        {
            owner.imports.Add(this);
        }

        public override IEnumerable<CompilerNode> Nodes => Enumerable.Empty<CompilerNode>();

        public override string ToString()
        {
            return base.ToString() + "=>" + this.reference;
        }
    }
}