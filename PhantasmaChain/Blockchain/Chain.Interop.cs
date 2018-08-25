using System;
using Phantasma.Blockchain.Contracts;
using Phantasma.Cryptography;
using Phantasma.Utils;
using Phantasma.VM;
using Phantasma.VM.Types;

namespace Phantasma.Blockchain
{
    public partial class Chain
    {
        internal void RegisterInterop(RuntimeVM vm)
        {
            vm.RegisterMethod("Runtime.Log", Runtime_Log);

            vm.RegisterMethod("Address()", Constructor_Address);
            vm.RegisterMethod("Hash()", Constructor_Hash);

            vm.RegisterMethod("Contract.Deploy", Contract_deploy);
            vm.RegisterMethod("Contract.Deploy", Chain_deploy);
        }

        private ExecutionState Constructor_Address(VirtualMachine vm)
        {
            var bytes = vm.stack.Pop().AsByteArray();
            if (bytes == null || bytes.Length != Address.PublicKeyLength)
            {
                return ExecutionState.Fault;
            }

            var address = new Address(bytes);
            var temp = new VMObject();
            temp.SetValue(address);
            vm.stack.Push(temp);

            return ExecutionState.Running;
        }

        private ExecutionState Constructor_Hash(VirtualMachine vm)
        {
            var bytes = vm.stack.Pop().AsByteArray();
            if (bytes == null || bytes.Length != Hash.Length)
            {
                return ExecutionState.Fault;
            }

            var hash = new Hash(bytes);
            var temp = new VMObject();
            temp.SetValue(hash);
            vm.stack.Push(temp);

            return ExecutionState.Running;
        }

        private ExecutionState Runtime_Log(VirtualMachine vm)
        {
            var text = vm.stack.Pop().AsString();
            this.Log.Write(Utils.Log.LogEntryKind.Message, text);
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
