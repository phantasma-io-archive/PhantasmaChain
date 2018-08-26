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
        internal void RegisterInterop(RuntimeVM vm)
        {
            vm.RegisterMethod("Runtime.Log", Runtime_Log);

            vm.RegisterMethod("Address()", Constructor_Address);
            vm.RegisterMethod("Hash()", Constructor_Hash);
            vm.RegisterMethod("ABI()", Constructor_ABI);

            vm.RegisterMethod("Contract.Deploy", Contract_deploy);
            vm.RegisterMethod("Contract.Deploy", Chain_deploy);
        }

        private ExecutionState Constructor_Object<T>(VirtualMachine vm, Func<byte[], T> loader) 
        {
            var bytes = vm.stack.Pop().AsByteArray();

            try
            {
                T obj = loader(bytes);
                var temp = new VMObject();
                temp.SetValue(obj);
                vm.stack.Push(temp);
            }
            catch 
            {
                return ExecutionState.Fault;
            }

            return ExecutionState.Running;
        }

        private ExecutionState Constructor_Address(VirtualMachine vm)
        {
            return Constructor_Object<Address>(vm, bytes =>
            {
                Throw.If(bytes == null || bytes.Length != Address.PublicKeyLength, "invalid key");
                return new Address(bytes);
            });
        }

        private ExecutionState Constructor_Hash(VirtualMachine vm)
        {
            return Constructor_Object<Hash>(vm, bytes =>
            {
                Throw.If(bytes == null || bytes.Length != Hash.Length, "invalid hash");
                return new Hash(bytes);
            });
        }

        private ExecutionState Constructor_ABI(VirtualMachine vm)
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

        private ExecutionState Runtime_Log(VirtualMachine vm)
        {
            var text = vm.stack.Pop().AsString();
            this.Log.Write(Core.Log.LogEntryKind.Message, text);
            return ExecutionState.Running;
        }

        private ExecutionState Contract_deploy(VirtualMachine vm)
        {
            var script = vm.currentFrame.GetRegister(0).AsByteArray();
            var abi = vm.currentFrame.GetRegister(1).AsByteArray();

            var runtime = (RuntimeVM)vm;
            var tx = runtime.Transaction;

            var contract = new CustomContract(script, abi);

            Log.Message($"Deploying contract: {contract.Address.Text}");

            var obj = new VMObject();
            obj.SetValue(contract);
            vm.stack.Push(obj);

            return ExecutionState.Running;
        }

        private ExecutionState Chain_deploy(VirtualMachine vm)
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
