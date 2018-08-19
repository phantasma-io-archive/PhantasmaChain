using Phantasma.Utils;
using Phantasma.VM.Contracts;
using Phantasma.VM.Types;

namespace Phantasma.Blockchain.Contracts
{
    public sealed class CustomContract : Contract
    {
        public override Address Address => Address.FromScript(this.Script);

        private byte[] _script;
        public override byte[] Script => _script;

        private byte[] _ABI;
        public override ContractInterface ABI => null;

        public CustomContract(Chain chain, byte[] script, byte[] ABI) : base(chain)
        {
            this._script = script;
            this._ABI = ABI;
        }
    }
}
