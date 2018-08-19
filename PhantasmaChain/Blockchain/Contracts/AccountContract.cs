using Phantasma.VM;
using Phantasma.VM.Types;

namespace Phantasma.Blockchain.Contracts
{
    public sealed class AccountContract : Contract
    {
        public static readonly byte[] DefaultAccountScript = new byte[] { (byte)Opcode.RET };
        public static readonly byte[] DefaultAccountABI = new byte[] { };

        private Address _address;
        public override Address Address => _address;

        public override byte[] Script => DefaultAccountScript;
        public override byte[] ABI => DefaultAccountABI;

        public AccountContract(Chain chain, byte[] publicKey) : base(chain)
        {
            this._address = new Address(publicKey);
        }
    }
}
