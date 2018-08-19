using System.Numerics;
using Phantasma.VM;
using Phantasma.VM.Types;

namespace Phantasma.Utils
{
    public static class VMUtils
    {
        public static object InvokeScript(byte[] script, object[] args)
        {
            var vm = new InvokeVM(script);

            for (int i=args.Length - 1; i>=0; i--)
            {
                vm.stack.Push(ToVMObject(args[i]));
            }
                       
            vm.Execute();

            return (vm.stack.Count > 0 ? vm.stack.Peek() : null);
        }

        public static VMObject ToVMObject(object obj)
        {
            if (obj is BigInteger)
            {
                return new VMObject().SetValue((BigInteger)obj);
            }

            if (obj is string)
            {
                return new VMObject().SetValue((string)obj);
            }

            if (obj is byte[])
            {
                return new VMObject().SetValue((byte[])obj, VMType.Bytes);
            }

            if (obj is Address)
            {
                return new VMObject().SetValue(((Address)obj).PublicKey, VMType.Address);
            }

            if (obj is int)
            {
                return new VMObject().SetValue((int)obj);
            }

            return null;
        }

        public static object FromVMObject(VMObject obj)
        {
            return null;
        }

    }
}
