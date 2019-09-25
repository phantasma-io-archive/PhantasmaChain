using Phantasma.Cryptography;

namespace Phantasma.Domain
{
    public enum FeedMode
    {
        First,
        Last,
        Max,
        Min,
        Average
    }

    public interface IFeed
    {
        string Name { get;  }
        Address Address { get;  }
        FeedMode Mode { get;  }
    }
}
