using Phantasma.Cryptography;
using Phantasma.Numerics;

namespace Phantasma.Domain
{
    public enum TaskFrequencyMode
    {
        None,
        Time,
        Blocks,
    }

    public interface ITask
    {
        BigInteger ID { get; }
        bool state { get; }
        Address payer { get; }
        string contractName { get; }
        uint offset { get; }
        uint frequency { get; }
        TaskFrequencyMode mode { get; }
    }

}

