using Phantasma.Numerics;

namespace Phantasma.VM.Utils
{
    public static class VMUtils
    {
        public static object InvokeScript(byte[] script, object[] args)
        {
            var vm = new InvokeVM(script);

            for (int i=args.Length - 1; i>=0; i--)
            {
                vm.Stack.Push(ToVMObject(args[i]));
            }
                       
            vm.Execute();

            return (vm.Stack.Count > 0 ? vm.Stack.Peek() : null);
        }

        public static VMObject ToVMObject(object obj)
        {
            if (obj == null)
            {
                return new VMObject();
            }

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

            if (obj is int)
            {
                return new VMObject().SetValue((int)obj);
            }

            return new VMObject().SetValue(obj);
        }

        public static object FromVMObject(VMObject obj)
        {
            return null;
        }

    }
}
