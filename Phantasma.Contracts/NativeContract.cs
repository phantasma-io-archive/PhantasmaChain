using Phantasma.Domain;

namespace Phantasma.Contracts
{
    public static class ContractPatch
    {
        public static readonly uint UnstakePatch = 1578238531;
    }

    public abstract class NativeContract : SmartContract
    {
        public override string Name => Kind.GetName();

        public abstract NativeContractKind Kind { get; }
    }
}
