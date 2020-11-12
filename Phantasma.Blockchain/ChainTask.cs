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
        public bool State { get; private set; }
        public Address Owner { get; private set; }
        public string ContextName { get; private set; }
        public string Method { get; private set; }
        public uint Frequency { get; private set; }
        public TaskFrequencyMode Mode { get; private set; }
        public BigInteger GasLimit { get; private set; }

        public ChainTask(BigInteger ID, Address payer, string contractName, string method, uint frequency, TaskFrequencyMode mode, BigInteger gasLimit, bool state)
        {
            this.ID = ID;
            this.Owner = payer;
            this.ContextName = contractName;
            this.Method = method;
            this.Frequency = frequency;
            this.Mode = mode;
            this.GasLimit = gasLimit;
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
                    writer.Write((byte)Mode);
                    writer.WriteBigInteger(GasLimit);
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
                    var frequency = (uint) reader.ReadVarInt();
                    var mode = (TaskFrequencyMode)reader.ReadByte();
                    var gasLimit = reader.ReadBigInteger();

                    return new ChainTask(taskID, payer, contractName, method, frequency, mode, gasLimit, true);
                }
            }
        }
    }
}
