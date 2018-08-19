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
        public readonly VMType[] arguments;

        public ContractMethod(string name, IEnumerable<VMType> args)
        {
            this.name = name;
            this.arguments = args.ToArray();
        }

        public static ContractMethod Unserialize(BinaryReader reader)
        {
            var name = reader.ReadShortString();
            var len = reader.ReadByte();
            var args = new VMType[len];
            for (int i = 0; i < len; i++)
            {
                args[i] = (VMType)reader.ReadByte();
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
}
