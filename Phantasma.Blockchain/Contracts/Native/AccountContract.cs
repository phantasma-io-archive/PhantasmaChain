using Phantasma.VM;
using Phantasma.VM.Contracts;
using Phantasma.Cryptography;

namespace Phantasma.Blockchain.Contracts.Native
{
    public sealed class AccountContract : SmartContract
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
