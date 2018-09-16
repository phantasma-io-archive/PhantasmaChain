using Phantasma.Numerics;
using Phantasma.Core;

namespace Phantasma.Cryptography.Ring
{
    // Those can be any parameters suitable for Digital Signature Algorithm (DSA). Bouncy castle library can generate them
    public struct GroupParameters
    {
        public readonly BigInteger Prime, Generator, SubgroupSize;

        public GroupParameters(BigInteger prime, BigInteger generator, BigInteger subgroupSize)
        {
            Prime = prime;
            Generator = generator;
            SubgroupSize = subgroupSize;

            Throw.If(Generator < 2 || Generator > Prime - BigInteger.One, "Generator out of range");
            Throw.If(Generator.ModPow(SubgroupSize, Prime) != BigInteger.One, "Generator is wrong");
        }
    }

}
