using System.Collections.Generic;
using System.IO;
using System.Linq;
using Phantasma.Utils;

namespace PhantasmaChain.Contracts
{
    public enum ContractArgumentType
    {
        None,
        Object,
        Bytes,
        Number,
        String,
        Bool
    }

    public struct ContractMethod
    {
        public readonly string name;
        public readonly ContractArgumentType[] arguments;

        public ContractMethod(string name, IEnumerable<ContractArgumentType> args)
        {
            this.name = name;
            this.arguments = args.ToArray();
        }

        public static ContractMethod Unserialize(BinaryReader reader)
        {
            var name = reader.ReadShortString();
            var len = reader.ReadByte();
            var args = new ContractArgumentType[len];
            for (int i=0; i < len; i++)
            {
                args[i] = (ContractArgumentType)reader.ReadByte();
            }

            return new ContractMethod(name, args);
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.WriteShortString(name);
            writer.Write((byte)arguments.Length);
            foreach (var entry in arguments)
            {
                writer.Write((byte)entry);
            }
        }

        public byte[] ToArray()
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    Serialize(writer);
                    return stream.ToArray();
                }
            }
        }
    }

    public class ABI
    {
        public readonly string Name;

        private Dictionary<string, ContractMethod> _methods = new Dictionary<string, ContractMethod>();
        public IEnumerable<ContractMethod> Methods => _methods.Values;

        public ABI(string name, IEnumerable<ContractMethod> methods)
        {
            this.Name = name;
            foreach (var entry in methods)
            {
                _methods[entry.name] = entry;
            }
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
            if (thisMethod.arguments.Length != method.arguments.Length)
            {
                return false;
            }

            for (int i=0; i<method.arguments.Length; i++)
            {
                if (thisMethod.arguments[i] != method.arguments[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Checks if this ABI implements of other ABI (eg: other is a subset of this)
        /// </summary>
        public bool Implements(ABI other)
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

        public static ABI Unserialize(BinaryReader reader)
        {
            var name = reader.ReadShortString();
            var len = reader.ReadByte();
            var methods = new ContractMethod[len];
            for (int i = 0; i < len; i++)
            {
                methods[i] = ContractMethod.Unserialize(reader);
            }

            return new ABI(name, methods);
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.WriteShortString(Name);
            writer.Write((byte)_methods.Count);
            foreach (var method in _methods.Values)
            {
                method.Serialize(writer);
            }
        }

    }
}
