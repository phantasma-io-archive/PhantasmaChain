using System;
using Phantasma.Blockchain.Contracts;
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
            vm.RegisterMethod("Chain.Deploy", Chain_deploy);
            vm.RegisterMethod("Account.Rename", Account_Rename);
        }

        private ExecutionState Runtime_Log(VirtualMachine vm)
        {
            var text = vm.stack.Pop().AsString();
            this.Log.Write(Utils.Log.LogEntryKind.Message, text);
            return ExecutionState.Running;
        }

        private ExecutionState Chain_deploy(VirtualMachine vm)
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

        private ExecutionState Account_Rename(VirtualMachine vm)
        {
            // TODO Verify permissions
            byte[] pubKey = new byte[0];
            if (!HasContract(pubKey))
            {
                return ExecutionState.Fault;
            }

            var account = this._contracts[pubKey];

            var name = vm.currentFrame.GetRegister(0).AsString();

            throw new NotImplementedException();
            /*
            // if same name, cancel
            if (account.Name == name)
            {
                return ExecutionState.Fault;
            }

            // check if someone else is using this name already
            if (this._contractLookup.Contains(name))
            {
                return ExecutionState.Fault;
            }

            //TODO
            //account.Rename(name);

            return ExecutionState.Running;
            */
        }
    }
}
