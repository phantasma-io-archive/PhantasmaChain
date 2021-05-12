using Phantasma.Storage;
using Phantasma.Storage.Utils;
using Phantasma.VM;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Phantasma.Domain
{
    public interface IContract
    {
        string Name { get; }
        ContractInterface ABI { get; }
    }

    public enum NativeContractKind
    {
        Gas,
        Block,
        Nexus,
        Stake,
        Swap,
        Account,
        Consensus,
        Governance,
        Storage,
        Validator,
        Interop,
        Exchange,
        Privacy,
        Relay,
        Ranking,
        Market,
        Friends,
        Mail,
        Sale,
    }

    public sealed class ContractInterface: ISerializable
    {
        public static readonly ContractInterface Empty = new ContractInterface(Enumerable.Empty<ContractMethod>(), Enumerable.Empty<ContractEvent>());

        private Dictionary<string, ContractMethod> _methods = new Dictionary<string, ContractMethod>(StringComparer.OrdinalIgnoreCase);
        public IEnumerable<ContractMethod> Methods => _methods.Values;
        public int MethodCount => _methods.Count;

        private ContractEvent[] _events;
        public IEnumerable<ContractEvent> Events => _events;
        public int EventCount => _events.Length;

        public ContractMethod this[string name]
        {
            get
            {
                return FindMethod(name);
            }
        }

        public ContractInterface(IEnumerable<ContractMethod> methods, IEnumerable<ContractEvent> events)
        {
            foreach (var entry in methods)
            {
                _methods[entry.name] = entry;
            }

            this._events = events.ToArray();
        }

        public ContractInterface()
        {
            this._events = new ContractEvent[0];
        }

        public bool HasMethod(string name)
        {
            return _methods.ContainsKey(name);
        }

        public ContractMethod FindMethod(string name)
        {
            if (_methods.ContainsKey(name))
            {
                return _methods[name];
            }

            return null;
        }

        public ContractEvent FindEvent(byte value)
        {
            foreach (var evt in _events)
            {
                if (evt.value == value)
                {
                    return evt;
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if this ABI implements a specific event
        /// </summary>
        public bool Implements(ContractEvent evt)
        {
            foreach (var entry in this.Events)
            {
                if (entry.name == evt.name && entry.value == evt.value && entry.returnType == evt.returnType)
                {
                    return true;
                }
            }

            return false;
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

            for (int i = 0; i < method.parameters.Length; i++)
            {
                if (thisMethod.parameters[i].type != method.parameters[i].type)
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

            foreach (var evt in other.Events)
            {
                if (!this.Implements(evt))
                {
                    return false;
                }
            }

            return true;
        }

        public void UnserializeData(BinaryReader reader)
        {
            var len = reader.ReadByte();
            _methods.Clear();
            for (int i = 0; i < len; i++)
            {
                var method  = ContractMethod.Unserialize(reader);
                _methods[method.name] = method;
            }

            len = reader.ReadByte();
            this._events = new ContractEvent[len];
            for (int i = 0; i < len; i++)
            {
                _events[i] = ContractEvent.Unserialize(reader);
            }
        }

        public static ContractInterface Unserialize(BinaryReader reader)
        {
            var result = new ContractInterface();
            result.UnserializeData(reader);
            return result;
        }

        public static ContractInterface FromBytes(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                using (var reader = new BinaryReader(stream))
                {
                    return Unserialize(reader);
                }
            }
        }

        public void SerializeData(BinaryWriter writer)
        {
            writer.Write((byte)_methods.Count);
            foreach (var method in _methods.Values)
            {
                method.Serialize(writer);
            }

            writer.Write((byte)_events.Length);
            foreach (var evt in _events)
            {
                evt.Serialize(writer);
            }
        }

        public byte[] ToByteArray()
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    SerializeData(writer);
                }

                return stream.ToArray();
            }
        }
    }

    public struct ContractParameter
    {
        public readonly string name;
        public readonly VMType type;

        public ContractParameter(string name, VMType vmtype)
        {
            this.name = name;
            this.type = vmtype;
        }
    }

    public class ContractEvent
    {
        public readonly byte value;
        public readonly string name;
        public readonly VMType returnType;
        public readonly byte[] description;

        public ContractEvent(byte value, string name, VMType returnType, byte[] description)
        {
            this.value = value;
            this.name = name;
            this.returnType = returnType;
            this.description = description;
        }

        public override string ToString()
        {
            return $"{name} : {returnType} => {value}";
        }

        public static ContractEvent Unserialize(BinaryReader reader)
        {
            var value = reader.ReadByte();
            var name = reader.ReadVarString();
            var returnType = (VMType)reader.ReadByte();
            var description = reader.ReadByteArray();

            return new ContractEvent(value, name, returnType, description);
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)value);
            writer.WriteVarString(name);
            writer.Write((byte)returnType);
            writer.WriteByteArray(description);

        }
    }

    public class ContractMethod
    {
        public readonly string name;
        public readonly VMType returnType;
        public readonly ContractParameter[] parameters;
        public int offset;

        public ContractMethod(string name, VMType returnType, Dictionary<string, int> labels, params ContractParameter[] parameters) 
        {
            if (!labels.ContainsKey(name))
            {
                throw new Exception("Missing offset in label map for method " + name);
            }

            var offset = labels[name];

            this.name = name;
            this.offset = offset;
            this.returnType = returnType;
            this.parameters = parameters;
        }

        public ContractMethod(string name, VMType returnType, int offset, params ContractParameter[] parameters)
        {
            this.name = name;
            this.offset = offset;
            this.returnType = returnType;
            this.parameters = parameters;
        }

        public bool IsProperty()
        {
            if (name.Length >= 4 && name.StartsWith("get") && char.IsUpper(name[3]))
            {
                return true;
            }

            if (name.Length >= 3 && name.StartsWith("is") && char.IsUpper(name[2]))
            {
                return true;
            }

            return false;
        }

        public bool IsTrigger()
        {
            if (name.Length >= 3 && name.StartsWith("on") && char.IsUpper(name[2]))
            {
                return true;
            }

            return false;
        }

        public override string ToString()
        {
            return $"{name} : {returnType}";
        }

        public static ContractMethod FromBytes(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                using (var reader = new BinaryReader(stream))
                {
                    return Unserialize(reader);
                }
            }
        }

        public static ContractMethod Unserialize(BinaryReader reader)
        {
            var name = reader.ReadVarString();
            var returnType = (VMType)reader.ReadByte();
            var offset = reader.ReadInt32();
            var len = reader.ReadByte();
            var parameters = new ContractParameter[len];
            for (int i = 0; i < len; i++)
            {
                var pName = reader.ReadVarString();
                var pVMType = (VMType)reader.ReadByte();
                parameters[i] = new ContractParameter(pName, pVMType);
            }

            return new ContractMethod(name, returnType, offset, parameters);
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.WriteVarString(name);
            writer.Write((byte)returnType);
            writer.Write((int)offset);
            writer.Write((byte)parameters.Length);
            foreach (var entry in parameters)
            {
                writer.WriteVarString(entry.name);
                writer.Write((byte)entry.type);
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

        /*
        public T Invoke<T>(IContract contract, params object[] args)
        {
            return (T)Invoke(contract, args);
        }

        public object Invoke(IContract contract, params object[] args)
        {
            Throw.IfNull(contract, "null contract");
            Throw.IfNull(args, "null args");
            Throw.If(args.Length != this.parameters.Length, "invalid arg count");

            var type = contract.GetType();
            var method = type.GetMethod(this.name);
            Throw.IfNull(method, "ABI mismatch");

            return method.Invoke(contract, args);
        }*/
    }

}
