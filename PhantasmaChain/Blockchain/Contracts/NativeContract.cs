using Phantasma.Utils;
using Phantasma.VM.Types;
using System.Text;

namespace Phantasma.Blockchain.Contracts
{
    public abstract class NativeContract : Contract
    {
        public override Address Address
        {
            get
            {
                var bytes = Encoding.ASCII.GetBytes(this.GetType().Name);
                var hash = CryptoUtils.Sha256(bytes);
                return new Address(hash);
            }
        }

        public override byte[] Script => null;

        private byte[] _ABI;
        public override byte[] ABI {
            get
            {
                return _ABI;
            }
        }

        public abstract NativeContractKind Kind { get; }

        public NativeContract(Chain chain) : base(chain)
        {
        }
    }
}
