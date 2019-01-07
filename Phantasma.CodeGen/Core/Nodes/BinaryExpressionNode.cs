using System;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.CodeGen.Core.Nodes
{
    public class BinaryExpressionNode : ExpressionNode
    {
        public OperatorKind @operator;
        public ExpressionNode left;
        public ExpressionNode right;

        public BinaryExpressionNode(CompilerNode owner) : base(owner)
        {

        }

        public override string ToString()
        {
            return base.ToString() + "=>" + this.@operator;
        }

        public override IEnumerable<CompilerNode> Nodes
        {
            get
            {
                yield return left;
                yield return right;
                yield break;
            }
        }

        public override List<Instruction> Emit(Compiler compiler)
        {
            var left = this.left.Emit(compiler);
            var right = this.right.Emit(compiler);

            Instruction.Opcode opcode;
            switch (this.@operator)
            {
                case OperatorKind.Addition: opcode = Instruction.Opcode.Add; break;
                case OperatorKind.Subtraction: opcode = Instruction.Opcode.Sub; break;
                case OperatorKind.Multiplication: opcode = Instruction.Opcode.Mul; break;
                case OperatorKind.Division: opcode = Instruction.Opcode.Div; break;
                case OperatorKind.Modulus: opcode = Instruction.Opcode.Mod; break;
                case OperatorKind.Equals: opcode = Instruction.Opcode.Equals; break;
                default: throw new ArgumentException("Invalid opcode: " + @operator);
            }

            var temp = new List<Instruction>();
            temp.AddRange(left);
            temp.AddRange(right);
            temp.Add(new Instruction() { source = this, target = compiler.AllocRegister(), a = left.Last(), op = opcode, b = right.Last() });

            return temp;
        }

        public override TypeKind GetKind()
        {
            var leftKind = left.GetKind();
            var rightKind = right.GetKind();

            if (leftKind == rightKind)
            {
                return leftKind;
            }

            return TypeKind.Unknown;
        }

        protected override bool ValidateSemantics()
        {
            if (@operator == OperatorKind.Unknown)
            {
                return false;
            }

            return this.GetKind() != TypeKind.Unknown;
        }
    }
}