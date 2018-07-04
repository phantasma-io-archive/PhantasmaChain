using Phantasma.VM;

namespace Phantasma.Core
{
    public sealed class DistributionContract : Contract
    {
        public static readonly byte[] DefaultScript = new byte[] { (byte)Opcode.RET };
        public static readonly byte[] DefaultABI = new byte[] { };

        private byte[] _publicKey;
        public override byte[] PublicKey => _publicKey;

        public override byte[] Script => DefaultScript;
        public override byte[] ABI => DefaultABI;

        public DistributionContract(Chain chain, byte[] publicKey) : base(chain)
        {
            this._publicKey = publicKey;
        }
    }
}
