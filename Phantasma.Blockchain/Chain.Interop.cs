using System;
using System.IO;
using Phantasma.Blockchain.Contracts;
using Phantasma.Cryptography;
using Phantasma.Core;
using Phantasma.VM;
using Phantasma.VM.Contracts;
using Phantasma.Blockchain.Contracts.Native;

namespace Phantasma.Blockchain
{
    public partial class Chain
    {
        internal static void RegisterInterop(RuntimeVM vm)
        {
            vm.RegisterMethod("Runtime.Log", Runtime_Log);

            vm.RegisterMethod("Address()", Constructor_Address);
            vm.RegisterMethod("Hash()", Constructor_Hash);
            vm.RegisterMethod("ABI()", Constructor_ABI);

            vm.RegisterMethod("Contract.Deploy", Contract_deploy);
            vm.RegisterMethod("Contract.Deploy", Chain_deploy);
        }

        private static ExecutionState Constructor_Object<T>(VirtualMachine vm, Func<byte[], T> loader) 
        {
            var bytes = vm.Stack.Pop().AsByteArray();

            try
            {
                T obj = loader(bytes);
                var temp = new VMObject();
                temp.SetValue(obj);
                vm.Stack.Push(temp);
            }
            catch 
            {
                return ExecutionState.Fault;
            }

            return ExecutionState.Running;
        }

        private static ExecutionState Constructor_Address(VirtualMachine vm)
        {
            return Constructor_Object<Address>(vm, bytes =>
            {
                Throw.If(bytes == null || bytes.Length != Address.PublicKeyLength, "invalid key");
                return new Address(bytes);
            });
        }

        private static ExecutionState Constructor_Hash(VirtualMachine vm)
        {
            return Constructor_Object<Hash>(vm, bytes =>
            {
                Throw.If(bytes == null || bytes.Length != Hash.Length, "invalid hash");
                return new Hash(bytes);
            });
        }

        private static ExecutionState Constructor_ABI(VirtualMachine vm)
        {
            return Constructor_Object<ContractInterface>(vm, bytes =>
            {
                Throw.If(bytes == null, "invalid abi");

                using (var stream = new MemoryStream(bytes))
                {
                    using (var reader = new BinaryReader(stream))
                    {
                        return ContractInterface.Unserialize(reader);
                    }
                }
            });
        }

        private static ExecutionState Runtime_Log(VirtualMachine vm)
        {
            var text = vm.Stack.Pop().AsString();
            //this.Log.Write(Core.Log.LogEntryKind.Message, text);
            Console.WriteLine(text); // TODO fixme
            return ExecutionState.Running;
        }

        private static ExecutionState Contract_deploy(VirtualMachine vm)
        {
            var script = vm.currentFrame.GetRegister(0).AsByteArray();
            var abi = vm.currentFrame.GetRegister(1).AsByteArray();

            var runtime = (RuntimeVM)vm;
            var tx = runtime.Transaction;

            var contract = new CustomContract(script, abi);

            //Log.Message($"Deploying contract: Address??");

            var obj = new VMObject();
            obj.SetValue(contract);
            vm.Stack.Push(obj);

            return ExecutionState.Running;
        }

        private static ExecutionState Chain_deploy(VirtualMachine vm)
        {
            var script = vm.currentFrame.GetRegister(0).AsByteArray();

            var runtime = (RuntimeVM)vm;
            if (!runtime.Chain.IsRoot)
            {
                return ExecutionState.Fault;
            }

            //Log.Message($"Deploying chain: {contract.Address.Text}");

            //TODO finish me
            return ExecutionState.Fault;
        }

    }
}
