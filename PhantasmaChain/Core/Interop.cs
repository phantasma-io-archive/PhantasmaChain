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

            var runtime = (RuntimeVM)vm;
            var tx = runtime.Transaction;

            var pubKey = script.ScriptToPublicKey();

            this.Log($"Deploying contract: {pubKey.PublicKeyToAddress()}");

            if (NativeToken == null)
            {
                NativeToken = new Token(pubKey);
            }

            var obj = new VMObject();
            obj.SetValue(pubKey, VMType.Address);
            vm.stack.Push(obj);
        }
    }
}
