using System;

namespace Phantasma.VM
{
    public class ExecutionFrame
    {
        public VMObject[] registers { get; private set; }

        public ExecutionFrame()
        {
            registers = new VMObject[VirtualMachine.MaxRegisterCount];

            for (int i = 0; i < VirtualMachine.MaxRegisterCount; i++)
            {
                registers[i] = new VMObject();
            }
        }

        public VMObject GetRegister(int index)
        {
            if (index < 0 || index >= VirtualMachine.MaxRegisterCount)
            {
                throw new ArgumentException("Invalid index");
            }

            return registers[index];
        }
    }
}
