using System;

namespace Phantasma.Blockchain.Contracts.Native
{
    public abstract class NativeContract : SmartContract
    {
        private string _name;
        public override string Name => _name;

        // TODO this is temporary, remove later
        public static string GetNameForContract<T>() where T : SmartContract
        {
            var type = typeof(T);
            return GetNameForContract(type);
        }

        private static string GetNameForContract(Type type)
        {
            return type.Name;
        }

        public NativeContract() : base()
        {
            _name = GetNameForContract(this.GetType());
        }
    }
}
