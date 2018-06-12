using System;

namespace Phantasma.VM
{
    public class InvokeVM : VirtualMachine
    {
        public InvokeVM(byte[] script) : base(script)
        {

        }

        public override bool ExecuteInterop(string method)
        {
            throw new NotImplementedException();
        }

        public override ExecutionContext LoadContext(byte[] key)
        {
            throw new NotImplementedException();
        }
    }

}
