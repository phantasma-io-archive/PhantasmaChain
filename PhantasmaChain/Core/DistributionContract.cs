using Phantasma.VM;

namespace Phantasma.Core
{
    public sealed class DistributionContract : Contract
    {
        public static readonly byte[] DefaultDistributionScript = new byte[] { (byte)Opcode.RET };
        public static readonly byte[] DefaultDistributionABI = new byte[] { };

        private byte[] _publicKey;
        public override byte[] PublicKey => _publicKey;

        public override byte[] Script => DefaultDistributionScript;
        public override byte[] ABI => DefaultDistributionABI;

        public DistributionContract(Chain chain, byte[] publicKey) : base(chain)
        {
            this._publicKey = publicKey;
        }
    }
}
