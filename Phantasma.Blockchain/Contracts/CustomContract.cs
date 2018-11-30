using Phantasma.Core;
using Phantasma.Cryptography;

namespace Phantasma.Blockchain.Contracts
{
    public sealed class CustomContract : SmartContract
    {
        public override ContractKind Kind => ContractKind.Custom;

        private string _name;
        public override string Name => _name;

        public byte[] Script { get; private set; }

        public CustomContract(byte[] script, byte[] ABI) : base()
        {
            Throw.IfNull(script, nameof(script));
            this.Script = script;

            _name = new Hash(CryptoExtensions.Sha256(script)).ToString(); // TODO do something better here
        }
    }
}
