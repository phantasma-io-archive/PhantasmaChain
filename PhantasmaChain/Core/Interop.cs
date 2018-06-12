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
        }

        private void Chain_deploy(VirtualMachine vm)
        {
            var script = vm.currentFrame.GetRegister(0).AsByteArray();
            var abi = vm.currentFrame.GetRegister(1).AsByteArray();

            var runtime = (RuntimeVM)vm;
            var tx = runtime.Transaction;

            var contract = new CustomContract(this, script, abi);

            Log.Message($"Deploying contract: {contract.PublicKey.PublicKeyToAddress()}");

            if (NativeTokenPubKey == null)
            {
                NativeTokenPubKey = contract.PublicKey;
            }

            var obj = new VMObject();
            obj.SetValue(contract);
            vm.stack.Push(obj);
        }
    }
}
