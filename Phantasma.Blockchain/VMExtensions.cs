using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Storage;
using Phantasma.VM;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

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
                vm.Expect(BigInteger.IsParsable(temp.AsString()), $"expected number for {ArgumentName}");
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

            vm.Expect(temp.Type == VMType.String, $"expected string for {ArgumentName}");

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
            if (temp.Type == VMType.String)
            {
                var text = temp.AsString();
                //TODO_FIX_TX
                //if (Address.IsValidAddress(text) && vm.Chain.Height > 65932)
                if (Address.IsValidAddress(text) && vm.ProtocolVersion >= 2)
                {
                    return Address.FromText(text);
                }
                return vm.Nexus.LookUpName(vm.Storage, text);
            }
            else
            if (temp.Type == VMType.Bytes)
            {
                var bytes = temp.AsByteArray();
                var addr = Serialization.Unserialize<Address>(bytes);
                return addr;
            }
            else
            {
                var addr = temp.AsInterop<Address>();
                return addr;
            }
        }

    }
}
