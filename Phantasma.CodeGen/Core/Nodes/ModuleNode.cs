using System.Collections.Generic;

namespace Phantasma.CodeGen.Core.Nodes
{
    public class ModuleNode : CompilerNode
    {
        public List<ImportNode> imports = new List<ImportNode>();
        public List<ClassNode> classes = new List<ClassNode>();

        public StatementNode body;

        public ModuleNode() : base(null)
        {
        }

        public override IEnumerable<CompilerNode> Nodes
        {
            get
            {
                foreach (var import in imports)
                {
                    yield return import;
                }

                foreach (var @class in classes)
                {
                    yield return @class;
                }

                if (body != null)
                {
                    yield return body;
                }

                yield break;
            }
        }

        protected override bool ValidateSemantics()
        {
            return body != null;
        }
    }
}