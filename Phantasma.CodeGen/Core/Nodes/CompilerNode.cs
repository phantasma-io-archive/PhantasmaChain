using System;
using System.Collections;
using System.Collections.Generic;

namespace Phantasma.CodeGen.Core.Nodes
{
    public enum Visibility
    {
        Private,
        Protected,
        Internal,
        Public
    }

    public enum OperatorKind
    {
        Unknown,
        Addition,
        Subtraction,
        Multiplication,
        Division,
        Modulus,
        Increment,
        Decrement,
        Equals,
        Different,
        Great,
        Less,
        GreatOrEqual,
        LessOrEqual,
        Not,
        And,
        Or,
        Xor,
    }

    public abstract class CompilerNode
    {
        public readonly CompilerNode Owner;

        public CompilerNode(CompilerNode owner)
        {
            if (owner == null && !(this is ModuleNode))
            {
                throw new Exception("Owner cannot be null");
            }
            this.Owner = owner;
        }

        public void Visit(Action<CompilerNode, int> visitor, int level = 0)
        {
            visitor(this, level);
            foreach (var node in this.Nodes)
            {
                node.Visit(visitor, level + 1);
            }
        }

        public abstract IEnumerable<CompilerNode> Nodes { get; }

        public override string ToString()
        {
            return this.GetType().Name.Replace("Node", "");
        }

        public virtual DeclarationNode ResolveIdentifier(string identifier)
        {
            if (this.Owner != null)
            {
                return this.Owner.ResolveIdentifier(identifier);
            }
            else
            {
                throw new Exception("Identifier could not be resolved: " + identifier);
            }
        }

        protected virtual bool ValidateSemantics()
        {
            return true;
        }

        public bool Validate()
        {
            if (!ValidateSemantics())
            {
                return false;
            }

            foreach (var node in this.Nodes)
            {
                if (!node.Validate())
                {
                    return false;
                }
            }

            return true;
        }
    }
}