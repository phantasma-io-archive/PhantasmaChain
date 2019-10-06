using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.Storage.Context;

namespace Phantasma.Contracts.Native
{
    public struct ChainValue
    {
        public string Name;
        public BigInteger Current;
        public BigInteger Minimum;
        public BigInteger Maximum;
        public BigInteger Deviation;
    }

    public sealed class GovernanceContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Governance;

        internal StorageMap _valueMap;

        public BigInteger FeeMultiplier = 1;

        public GovernanceContract() : base()
        {
        }

        public bool HasName(string name)
        {
            return HasValue(name);
        }

        #region VALUES
        public bool HasValue(string name)
        {
            return _valueMap.ContainsKey<string>(name);
        }

        public void CreateValue(string name, BigInteger initial, BigInteger minimum, BigInteger maximum)
        {
            Runtime.Expect(!HasName(name), "name already exists");
            Runtime.Expect(Runtime.IsWitness(Runtime.Nexus.GenesisAddress), "genesis must be witness");

            Runtime.Expect(minimum < maximum, "invalid minimum");
            Runtime.Expect(minimum <= initial, "initial should be equal or greater than minimum");
            Runtime.Expect(maximum >= initial, "initial should be equal or lesser than maximum");

            if (name == ValidatorContract.ValidatorCountTag)
            {
                Runtime.Expect(initial == 1, "initial number of validators must always be one");
            }

            var value = new ChainValue()
            {
                Name = name,
                Current = initial,
                Minimum = minimum,
                Maximum = maximum,
            };

            _valueMap.Set<string, ChainValue>(name, value);
            Runtime.Notify(EventKind.ValueCreate, Runtime.Nexus.GenesisAddress, new ChainValueEventData() { Name = name, Value = initial });
        }

        public BigInteger GetValue(string name)
        {
            Runtime.Expect(HasValue(name), "invalid value name");
            var temp = _valueMap.Get<string, ChainValue>(name);
            return temp.Current;
        }

        public void SetValue(string name, BigInteger value)
        {
            Runtime.Expect(HasValue(name), "invalid value name");

            var temp = _valueMap.Get<string, ChainValue>(name);
            Runtime.Expect(value != temp.Current, "value already set");
            Runtime.Expect(value >= temp.Minimum, "less than minimum value");
            Runtime.Expect(value <= temp.Maximum, "greater than maximum value");

            var pollName = ConsensusContract.SystemPoll + name;
            var hasConsensus = Runtime.CallContext("consensus", "HasConsensus", pollName, value).AsBool();
            Runtime.Expect(hasConsensus, "consensus not reached");

            temp.Current = value;

            _valueMap.Set<string, ChainValue>(name, temp);

            Runtime.Notify(EventKind.ValueUpdate, Runtime.Nexus.GenesisAddress, new ChainValueEventData() { Name = name, Value = value});
        }
        #endregion
    }
}
