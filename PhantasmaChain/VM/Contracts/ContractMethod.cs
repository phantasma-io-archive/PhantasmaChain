using Phantasma.Utils;
using Phantasma.VM;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Phantasma.VM.Contracts
{
    public struct ContractMethod
    {
        public readonly string name;
        public readonly VMType returnType;
        public readonly VMType[] parameters;

        public ContractMethod(string name, VMType returnType, IEnumerable<VMType> parameters)
        {
            this.name = name;
            this.returnType = returnType;
            this.parameters = parameters.ToArray();
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
    }
}
