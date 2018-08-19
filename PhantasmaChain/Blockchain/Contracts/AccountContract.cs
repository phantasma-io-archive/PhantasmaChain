using Phantasma.VM;
using Phantasma.VM.Contracts;
using Phantasma.VM.Types;

namespace Phantasma.Blockchain.Contracts
{
    public sealed class AccountContract : Contract
    {
        public static readonly byte[] DefaultAccountScript = new byte[] { (byte)Opcode.RET };

        private Address _address;
        public override Address Address => _address;

        public override byte[] Script => DefaultAccountScript;
        public override ContractInterface ABI => null;

        public AccountContract(byte[] publicKey) : base()
        {
            this._address = new Address(publicKey);
        }
    }
}
