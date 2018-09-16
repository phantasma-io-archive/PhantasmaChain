using System;

namespace Phantasma.VM
{
    public class ExecutionFrame
    {
        public readonly VMObject[] Registers;

        public readonly uint Offset; // current instruction pointer **before** the frame was entered
        public readonly ExecutionContext Context;
        public readonly VirtualMachine VM;

        public ExecutionFrame(VirtualMachine VM, uint offset, ExecutionContext context, int registerCount)
        {
            this.VM = VM;
            this.Offset = offset;
            this.Context = context;

            Registers = new VMObject[registerCount];

            for (int i = 0; i < registerCount; i++)
            {
                Registers[i] = new VMObject();
            }
        }

        public VMObject GetRegister(int index)
        {
            if (index < 0 || index >= Registers.Length)
            {
                throw new ArgumentException("Invalid index");
            }

            return Registers[index];
        }
    }
}
