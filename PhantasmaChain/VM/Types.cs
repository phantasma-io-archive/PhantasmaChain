using System;

namespace Phantasma.VM
{
    public interface IInteropObject
    {

    }

    public class ExecutionContext: IInteropObject
    {
        public byte[] Script { get; private set; }

        public ExecutionContext(byte[] script)
        {
            this.Script = script;
        }
    }

    public class ExecutionFrame
    {
        public VMObject[] registers { get; private set; }

        public readonly uint Offset; // current instruction pointer **before** the frame was entered
        public readonly ExecutionContext Context;

        public ExecutionFrame(uint offset, ExecutionContext context)
        {
            this.Offset = offset;
            this.Context = context;

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
