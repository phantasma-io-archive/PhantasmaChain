using System;

namespace Phantasma.Contracts.Types
{
    public interface Surprise<T>
    {
        T Value { get; }
        Timestamp Timestamp { get; }
        bool Hidden { get; }
    }
}
