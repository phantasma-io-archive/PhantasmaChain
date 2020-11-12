using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.Storage.Utils;
using System.IO;

namespace Phantasma.Blockchain
{
    public class ChainTask: ITask
    {
        public readonly static ChainTask Null = null;

        public BigInteger ID { get; private set; }
        public bool state { get; private set; }
        public Address payer { get; private set; }
        public string contractName { get; private set; }
        public uint offset { get; private set; }
        public uint frequency { get; private set; }
        public TaskFrequencyMode mode { get; private set; }

        public ChainTask(BigInteger ID, Address payer, string contractName, uint offset, uint frequency, TaskFrequencyMode mode, bool state)
        {
            this.ID = ID;
            this.payer = payer;
            this.contractName = contractName;
            this.offset = offset;
            this.frequency = frequency;
            this.mode = mode;
            this.state = state;
        }

        public byte[] ToByteArray()
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    writer.WriteAddress(payer);
                    writer.WriteVarString(contractName);
                    writer.WriteVarInt(offset);
                    writer.WriteVarInt(frequency);
                    writer.Write((byte)mode);
                }

                return stream.ToArray();
            }
        }

        public static ChainTask FromBytes(BigInteger taskID, byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                using (var reader = new BinaryReader(stream))
                {
                    var payer = reader.ReadAddress();
                    var contractName = reader.ReadVarString();
                    var offset = (uint)reader.ReadVarInt();
                    var frequency = (uint) reader.ReadVarInt();
                    var mode = (TaskFrequencyMode)reader.ReadByte();

                    return new ChainTask(taskID, payer, contractName, offset, frequency, mode, true);
                }
            }
        }
    }
}
