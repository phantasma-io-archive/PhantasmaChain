using System.IO;
using System.Linq;
using Phantasma.Core;
using Phantasma.Storage;
using Phantasma.Storage.Utils;

namespace Phantasma.VM.Contracts
{
    public struct ContractParameter
    {
        public readonly string name;
        public readonly VMType type;

        public ContractParameter(string name, VMType type)
        {
            this.name = name;
            this.type = type;
        }
    }

    public class ContractMethod
    {
        public readonly string name;
        public readonly VMType returnType;
        public readonly ContractParameter[] parameters;

        public ContractMethod(string name, VMType returnType, params ContractParameter[] parameters)
        {
            this.name = name;
            this.returnType = returnType;
            this.parameters = parameters.ToArray();
        }

        public override string ToString()
        {
            return $"{name} => {returnType}";
        }

        public static ContractMethod Unserialize(BinaryReader reader)
        {
            var name = reader.ReadVarString();
            var returnType = (VMType)reader.ReadByte();
            var len = reader.ReadByte();
            var parameters = new ContractParameter[len];
            for (int i = 0; i < len; i++)
            {
                var pName = reader.ReadVarString();
                var pType = (VMType)reader.ReadByte();
                parameters[i] = new ContractParameter(pName, pType);
            }

            return new ContractMethod(name, returnType, parameters);
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.WriteVarString(name);
            writer.Write((byte)returnType);
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
        }
    }
}
