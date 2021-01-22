using System.IO;
using System.Numerics;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Storage.Utils;

namespace Phantasma.Blockchain
{
    public class ChainTask: ITask
    {
        public readonly static ChainTask Null = null;

        public BigInteger ID { get; private set; }
        public bool State { get; private set; }
        public Address Owner { get; private set; }
        public string ContextName { get; private set; }
        public string Method { get; private set; }
        public uint Frequency { get; private set; }
        public uint Delay { get; private set; }
        public TaskFrequencyMode Mode { get; private set; }
        public BigInteger GasLimit { get; private set; }
        public BigInteger Height { get; private set; }

        public ChainTask(BigInteger ID, Address payer, string contractName, string method, uint frequency, uint delay, TaskFrequencyMode mode, BigInteger gasLimit, BigInteger height, bool state)
        {
            this.ID = ID;
            this.Owner = payer;
            this.ContextName = contractName;
            this.Method = method;
            this.Frequency = frequency;
            this.Delay = delay;
            this.Mode = mode;
            this.GasLimit = gasLimit;
            this.Height = height;
            this.State = state;
        }

        public byte[] ToByteArray()
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    writer.WriteAddress(Owner);
                    writer.WriteVarString(ContextName);
                    writer.WriteVarString(Method);
                    writer.WriteVarInt(Frequency);
                    writer.WriteVarInt(Delay);
                    writer.Write((byte)Mode);
                    writer.WriteBigInteger(GasLimit);
                    writer.WriteBigInteger(Height);
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
                    var method = reader.ReadVarString();
                    var frequency = (uint)reader.ReadVarInt();
                    var delay = (uint)reader.ReadVarInt();
                    var mode = (TaskFrequencyMode)reader.ReadByte();
                    var gasLimit = reader.ReadBigInteger();
                    var height = reader.ReadBigInteger();

                    return new ChainTask(taskID, payer, contractName, method, frequency, delay, mode, gasLimit, height, true);
                }
            }
        }
    }
}
