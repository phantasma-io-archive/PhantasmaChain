using Phantasma.CodeGen.Core.Nodes;
using System.Collections.Generic;

namespace Phantasma.CodeGen.Core
{
    public class Compiler
    {
        private int registerIndex;
        private int labelIndex;

        public Dictionary<string, string> varMap = new Dictionary<string, string>();

        public string AllocRegister()
        {
            var temp = "t"+registerIndex.ToString();
            registerIndex++;
            return temp;
        }


        public string AllocLabel()
        {
            var temp = "p"+labelIndex.ToString();
            labelIndex++;
            return temp;
        }

        private void ProcessBlock(List<Instruction> instructions, BlockNode block)
        {
            foreach (var st in block.statements)
            {
                var list = st.Emit(this);

                foreach (var item in list)
                {
                    instructions.Add(item);
                }
            }
        }

        public List<Instruction> Execute(ModuleNode node)
        {
            var instructions = new List<Instruction>();

            if (node.body != null)
            {
                var temp = node.body.Emit(this);
                instructions.AddRange(temp);
            }

            labelIndex = 0;
            registerIndex = 0;
            foreach (var entry in node.classes)
            {
                foreach (var method in entry.methods)
                {
                    var temp = method.Emit(this);                    
                    instructions.AddRange(temp);
                }
            }

            return instructions;
        }
    }
}
