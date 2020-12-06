using System.Numerics;
using Phantasma.Cryptography;

namespace Phantasma.Domain
{
    public interface IOrganization
    {
        string ID { get; }
        string Name { get; }
        Address Address { get; }
        byte[] Script { get; }
        BigInteger Size { get; } // number of members

        bool IsMember(Address address);
        Address[] GetMembers();
    }
}
