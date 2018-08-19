using System.Collections.Generic;
using System.IO;
using Phantasma.Utils;

namespace Phantasma.VM.Contracts
{
    public sealed class ContractInterface
    {
        private Dictionary<string, ContractMethod> _methods = new Dictionary<string, ContractMethod>();
        public IEnumerable<ContractMethod> Methods => _methods.Values;

        public ContractInterface(IEnumerable<ContractMethod> methods)
        {
            foreach (var entry in methods)
            {
                _methods[entry.name] = entry;
            }
        }

        public ContractMethod FindMethod(string name)
        {
            Throw.If(!_methods.ContainsKey(name), "method not found");
            return _methods[name];
        }

        /// <summary>
        /// Checks if this ABI implements a specific method
        /// </summary>
        public bool Implements(ContractMethod method)
        {
            if (!_methods.ContainsKey(method.name))
            {
                return false;
            }

            var thisMethod = _methods[method.name];
            if (thisMethod.parameters.Length != method.parameters.Length)
            {
                return false;
            }

            for (int i=0; i<method.parameters.Length; i++)
            {
                if (thisMethod.parameters[i] != method.parameters[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Checks if this ABI implements of other ABI (eg: other is a subset of this)
        /// </summary>
        public bool Implements(ContractInterface other)
        {
            foreach (var method in other.Methods)
            {
                if (!this.Implements(method))
                {
                    return false;
                }
            }

            return true;
        }

        public static ContractInterface Unserialize(BinaryReader reader)
        {
            var len = reader.ReadByte();
            var methods = new ContractMethod[len];
            for (int i = 0; i < len; i++)
            {
                methods[i] = ContractMethod.Unserialize(reader);
            }

            return new ContractInterface(methods);
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)_methods.Count);
            foreach (var method in _methods.Values)
            {
                method.Serialize(writer);
            }
        }

    }
}
