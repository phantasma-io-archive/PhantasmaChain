using Phantasma.Contracts;
using Phantasma.Utils;
using Phantasma.VM;

namespace Phantasma.Core
{
    public partial class Chain
    {
        internal void RegisterInterop(RuntimeVM vm)
        {
            vm.RegisterMethod("Chain.Deploy", Chain_deploy);
            vm.RegisterMethod("Account.Rename", Account_Rename);
        }

        private ExecutionState Chain_deploy(VirtualMachine vm)
        {
            var script = vm.currentFrame.GetRegister(0).AsByteArray();
            var abi = vm.currentFrame.GetRegister(1).AsByteArray();

            var runtime = (RuntimeVM)vm;
            var tx = runtime.Transaction;

            var contract = new CustomContract(this, script, abi);

            Log.Message($"Deploying contract: {contract.PublicKey.PublicKeyToAddress(Cryptography.AddressType.Contract)}");

            if (NativeTokenPubKey == null)
            {
                NativeTokenPubKey = contract.PublicKey;
            }

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
        }
    }
}
