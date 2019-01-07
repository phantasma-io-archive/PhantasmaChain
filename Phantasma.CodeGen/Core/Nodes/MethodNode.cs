using System;
using System.Collections.Generic;

namespace Phantasma.CodeGen.Core.Nodes
{
    public class MethodNode : CompilerNode
    {
        public string name;
        public bool isStatic;
        public bool isAbstract;
        public bool isVirtual;
        public Visibility visibility;

        public TypeNode returnType;

        public List<ParameterNode> parameters = new List<ParameterNode>();

        public StatementNode body;

        public MethodNode(ClassNode owner) : base(owner)
        {
            owner.methods.Add(this);
        }

        public override DeclarationNode ResolveIdentifier(string identifier)
        {
            foreach (var arg in parameters)
            {
                if (arg.decl.identifier == identifier)
                {
                    return arg.decl;
                }
            }

            return base.ResolveIdentifier(identifier);
        }

        public override IEnumerable<CompilerNode> Nodes
        {
            get
            {
                foreach (var arg in parameters)
                {
                    yield return arg;
                }
                yield return returnType;
                yield return body;
                yield break;
            }
        }

        public override string ToString()
        {
            return base.ToString() + "=>" + this.name;
        }

        public List<Instruction> Emit(Compiler compiler)
        {
            var result = new List<Instruction>();

            result.Add(new Instruction() { source = this, target = this.name, op = Instruction.Opcode.Label });

            foreach (var arg in parameters)
            {
                var reg = compiler.AllocRegister();
                compiler.varMap[arg.decl.identifier] = reg;
                result.Add(new Instruction() { source = this, target = reg, op = Instruction.Opcode.Pop });
            }

            var temp = body.Emit(compiler);
            result.AddRange(temp);
            return result;
        }

        protected override bool ValidateSemantics()
        {
            return !string.IsNullOrEmpty(name);
        }
    }
}