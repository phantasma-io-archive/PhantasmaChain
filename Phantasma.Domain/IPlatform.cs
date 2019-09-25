using Phantasma.Cryptography;

namespace Phantasma.Domain
{
    public interface IPlatform
    {
        string Name { get; }
        string Symbol { get; } // for fuel
        Address Address { get; }
    }
}
