using Phantasma.Utils;
using Phantasma.VM.Types;

namespace Phantasma.Blockchain.Contracts
{
    public sealed class CustomContract : Contract
    {
        private Address _address;
        public override Address Address => _address;

        private byte[] _script;
        public override byte[] Script => _script;

        private byte[] _ABI;
        public override byte[] ABI => _ABI;

        public CustomContract(Chain chain, byte[] script, byte[] ABI) : base(chain)
        {
            this._script = script;
            this._ABI = ABI;
            this._address = script.ScriptToPublicKey();
        }
    }
}
