using System.Collections.Generic;
using System.Linq;

namespace Phantasma.CodeGen.Core.Nodes
{

    public enum TypeKind
    {
        Unknown,
        Void,
        String,
        Integer,
        Float,
        Boolean,
        ByteArray,
        Struct,
    }

    public class TypeNode: CompilerNode
    {
        public TypeKind Kind;

        public TypeNode(CompilerNode owner, TypeKind kind) : base(owner)
        {
            this.Kind = kind;
        }

        public override IEnumerable<CompilerNode> Nodes => Enumerable.Empty<CompilerNode>();
    }
}
