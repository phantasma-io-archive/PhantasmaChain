using System.Collections.Generic;
using System.Linq;

namespace Phantasma.CodeGen.Core.Nodes
{
    public class LiteralExpressionNode : ExpressionNode
    {
        public object value;
        public LiteralKind kind;

        public LiteralExpressionNode(CompilerNode owner) : base(owner)
        {
        }

        public override IEnumerable<CompilerNode> Nodes => Enumerable.Empty<CompilerNode>();

        public override string ToString()
        {
            return base.ToString() + "=>" + this.value;
        }

        public override List<Instruction> Emit(Compiler compiler)
        {
            var temp = new List<Instruction>();
            temp.Add(new Instruction() { source = this, target = compiler.AllocRegister(), literal = this, op = Instruction.Opcode.Assign });
            return temp;
        }

        public override TypeKind GetKind()
        {
            switch (kind)
            {
                case LiteralKind.Binary: return TypeKind.ByteArray;
                case LiteralKind.Boolean: return TypeKind.Boolean;
                case LiteralKind.Float: return TypeKind.Float;
                case LiteralKind.Integer: return TypeKind.Integer;
                case LiteralKind.String: return TypeKind.String;
                default: return TypeKind.Unknown;
            }
        }
    }
}