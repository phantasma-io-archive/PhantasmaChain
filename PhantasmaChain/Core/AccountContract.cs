using Phantasma.VM;

namespace Phantasma.Core
{
    public class AccountContract : Contract
    {
        public static readonly byte[] DefaultAccountScript = new byte[] { (byte)Opcode.RET };
        public static readonly byte[] DefaultAccountABI = new byte[] { };

        private byte[] _publicKey;
        public override byte[] PublicKey => _publicKey;

        public override byte[] Script => DefaultAccountScript;
        public override byte[] ABI => DefaultAccountABI;

        public AccountContract(Chain chain, byte[] publicKey) : base(chain)
        {
            this._publicKey = publicKey;
        }
    }
}
