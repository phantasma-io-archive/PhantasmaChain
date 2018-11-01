using Phantasma.VM.Contracts;
using Phantasma.Core;

namespace Phantasma.Blockchain.Contracts
{
    public sealed class CustomContract : SmartContract
    {
        public override ContractKind Kind => ContractKind.Custom;

        //public override Address Address => Address.FromScript(this.Script);

        public byte[] Script { get; private set; }

        public CustomContract(byte[] script, byte[] ABI) : base()
        {
            Throw.IfNull(script, nameof(script));
            this.Script = script;
        }
    }
}
