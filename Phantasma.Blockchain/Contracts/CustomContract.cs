using Phantasma.VM.Contracts;
using Phantasma.Cryptography;

namespace Phantasma.Blockchain.Contracts
{
    public sealed class CustomContract : SmartContract
    {
        //public override Address Address => Address.FromScript(this.Script);

        private byte[] _script;
        public override byte[] Script => _script;

        private byte[] _ABI;
        public override ContractInterface ABI => null;

        public CustomContract(byte[] script, byte[] ABI) : base()
        {
            this._script = script;
            this._ABI = ABI;
        }
    }
}
