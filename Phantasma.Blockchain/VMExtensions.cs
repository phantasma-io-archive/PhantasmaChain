using System;
using System.Numerics;
using System.Diagnostics;
using Phantasma.Core;
using Phantasma.Cryptography;
using Phantasma.Storage;
using Phantasma.VM;

namespace Phantasma.Blockchain
{
    public static class VMExtensions
    {
        public static void ExpectStackSize(this VirtualMachine vm, int minSize)
        {
            if (vm.Stack.Count < minSize)
            {
                var callingFrame = new StackFrame(1);
                var method = callingFrame.GetMethod();

                throw new VMException(vm, $"not enough arguments in stack, expected {minSize} @ {method}");
            }
        }

        public static T PopEnum<T>(this VirtualMachine vm, string ArgumentName) where T : struct, IConvertible
        {
            if (!typeof(T).IsEnum)
            {
                throw new ArgumentException("PopEnum: T must be an enumerated type");
            }

            var temp = vm.Stack.Pop();

            if (temp.Type != VMType.Number)
            {
                vm.Expect(temp.Type == VMType.Enum, $"expected enum for {ArgumentName}");
            }

            return temp.AsEnum<T>();
        }

        public static BigInteger PopNumber(this VirtualMachine vm, string ArgumentName)
        {
            var temp = vm.Stack.Pop();

            if (temp.Type == VMType.String)
            {
                vm.Expect(temp.AsString().IsParsable(), $"expected number for {ArgumentName}");
            }
            else
            {
                vm.Expect(temp.Type == VMType.Number, $"expected number for {ArgumentName}");
            }

            return temp.AsNumber();
        }

        public static string PopString(this VirtualMachine vm, string ArgumentName)
        {
            var temp = vm.Stack.Pop();

            vm.Expect(temp.Type == VMType.String, $"expected string for {ArgumentName} got {temp.Type}");

            return temp.AsString();
        }

        public static byte[] PopBytes(this VirtualMachine vm, string ArgumentName)
        {
            var temp = vm.Stack.Pop();

            vm.Expect(temp.Type == VMType.Bytes, $"expected bytes for {ArgumentName}");

            return temp.AsByteArray();
        }

        public static Address PopAddress(this RuntimeVM vm)
        {
            var temp = vm.Stack.Pop();

            switch (temp.Type)
            {
                case VMType.String:
                    {
                        var text = temp.AsString();
                        if (Address.IsValidAddress(text) && vm.ProtocolVersion >= 2)
                        {
                            return Address.FromText(text);
                        }
                        return vm.Chain.LookUpName(vm.Storage, text);
                    }

                case VMType.Bytes:
                    {
                        var bytes = temp.AsByteArray();

                        if (bytes == null || bytes.Length == 0)
                        {
                            return Address.Null;
                        }

                        var addressData = bytes;
                        if (addressData.Length == Address.LengthInBytes + 1)
                        {
                            // HACK this is to work around sometimes addresses being passed around in Serializable format...
                            var addr = Serialization.Unserialize<Address>(bytes);
                            return addr;
                        }

                        Throw.If(addressData.Length != Address.LengthInBytes, "cannot build Address from invalid data");
                        return Address.FromBytes(addressData);
                    }

                default:
                    {
                        var addr = temp.AsInterop<Address>();
                        return addr;
                    }
            }

        }

    }
}
