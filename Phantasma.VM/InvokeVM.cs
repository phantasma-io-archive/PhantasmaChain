using Phantasma.Cryptography;
using Phantasma.Numerics;
using System;

namespace Phantasma.VM
{
    public class InvokeVM : VirtualMachine
    {
        public InvokeVM(byte[] script) : base(script)
        {

        }

        public override ExecutionState ExecuteInterop(string method)
        {
            throw new NotImplementedException();
        }

        public override ExecutionContext LoadContext(string contextName)
        {
            throw new NotImplementedException();
        }
    }

}
