using Phantasma.Utils;

namespace Phantasma.Core
{
    public sealed class CustomContract : Contract
    {
        private byte[] _publicKey;
        public override byte[] PublicKey => _publicKey;

        private byte[] _script;
        public override byte[] Script => _script;

        private byte[] _ABI;
        public override byte[] ABI => _ABI;

        public CustomContract(Chain chain, byte[] script, byte[] ABI) : base(chain)
        {
            this._script = script;
            this._ABI = ABI;
            this._publicKey = script.ScriptToPublicKey();
        }
    }
}
