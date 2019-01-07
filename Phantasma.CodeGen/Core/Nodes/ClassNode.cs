using System;
using System.Collections.Generic;

namespace Phantasma.CodeGen.Core.Nodes
{
    public class ClassNode : CompilerNode
    {
        public string name;
        public string parent;
        public bool isAbstract;
        public bool isStatic;
        public Visibility visibility;

        public List<MethodNode> methods = new List<MethodNode>();

        public ClassNode(ModuleNode owner) : base(owner)
        {
            owner.classes.Add(this);
        }

        public override IEnumerable<CompilerNode> Nodes => methods;

        public override string ToString()
        {
            return base.ToString() + "=>" + this.name;
        }
    }
}