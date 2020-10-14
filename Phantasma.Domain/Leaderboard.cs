using Phantasma.Cryptography;
using Phantasma.Numerics;

namespace Phantasma.Domain
{
    public struct LeaderboardRow
    {
        public Address address;
        public BigInteger score;
    }

    public struct Leaderboard
    {
        public string name;
        public Address owner;
        public BigInteger size;
        public BigInteger round;
    }

}
