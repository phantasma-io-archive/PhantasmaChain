using Phantasma.Blockchain;
using Phantasma.Core;
using Phantasma.Core.Utils;
using System.IO;
using System.Linq;

namespace Phantasma.VM.Contracts
{
    public struct ContractMethod
    {
        public readonly string name;
        public readonly VMType returnType;
        public readonly VMType[] parameters;

        public ContractMethod(string name, VMType returnType, params VMType[] parameters)
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
            var name = reader.ReadShortString();
            var returnType = (VMType)reader.ReadByte();
            var len = reader.ReadByte();
            var parameters = new VMType[len];
            for (int i = 0; i < len; i++)
            {
                parameters[i] = (VMType)reader.ReadByte();
            }

            return new ContractMethod(name, returnType, parameters);
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.WriteShortString(name);
            writer.Write((byte)returnType);
            writer.Write((byte)parameters.Length);
            foreach (var entry in parameters)
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

        public T Invoke<T>(SmartContract contract, params object[] args)
        {
            return (T)Invoke(contract, args);
        }

        public object Invoke(SmartContract contract, params object[] args)
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
