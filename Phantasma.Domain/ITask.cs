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
        bool State { get; }
        Address Owner { get; }
        string ContextName { get; }
        uint Offset { get; }
        uint Frequency { get; }
        TaskFrequencyMode Mode { get; }
    }

}

