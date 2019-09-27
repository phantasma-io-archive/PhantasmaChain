using Phantasma.Domain;

namespace Phantasma.Contracts
{
    public abstract class NativeContract : SmartContract
    {
        public override string Name => Kind.GetName();

        public abstract NativeContractKind Kind { get; }
    }
}
