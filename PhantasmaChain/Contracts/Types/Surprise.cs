using System;

namespace Phantasma.Contracts.Types
{
    public abstract class Surprise<T>
    {
        public abstract T Value { get; }
        public abstract Timestamp Timestamp { get; }
        public abstract bool Hidden { get; }

        public bool Public
        {
            get
            {
                return !Hidden;
            }
        }
    }
}
