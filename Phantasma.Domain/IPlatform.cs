using Phantasma.Cryptography;

namespace Phantasma.Domain
{
    public interface IPlatform
    {
        string Name { get; }
        string Symbol { get; } // for fuel
        Address Address { get; }
    }

    public struct InteropBlock
    {
        public string Platform;
        public Hash Hash;
        public Hash[] Transactions;
    }

    public struct InteropTransaction
    {
        public string Platform;
        public Hash Hash;
        public Event[] Events;
    }
}
