using Phantasma.VM;
using Phantasma.VM.Types;

namespace Phantasma.Blockchain.Contracts
{
    public sealed class DistributionContract : Contract
    {
        public static readonly byte[] DefaultScript = new byte[] { (byte)Opcode.RET };
        public static readonly byte[] DefaultABI = new byte[] { };

        private Address _address;
        public override Address Address => _address;

        public override byte[] Script => DefaultScript;
        public override byte[] ABI => DefaultABI;

        public DistributionContract(Chain chain, byte[] publicKey) : base(chain)
        {
            this._address = new Address(publicKey);
        }
    }
}
