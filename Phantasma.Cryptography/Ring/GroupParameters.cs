using Phantasma.Numerics;
using Phantasma.Core;

namespace Phantasma.Cryptography.Ring
{
    // Those can be any parameters suitable for Digital Signature Algorithm (DSA). Bouncy castle library can generate them
    public struct GroupParameters
    {
        public readonly LargeInteger Prime, Generator, SubgroupSize;

        public GroupParameters(LargeInteger prime, LargeInteger generator, LargeInteger subgroupSize)
        {
            Prime = prime;
            Generator = generator;
            SubgroupSize = subgroupSize;

            Throw.If(Generator < 2 || Generator > Prime - LargeInteger.One, "Generator out of range");
            Throw.If(LargeInteger.ModPow(Generator, SubgroupSize, Prime) != LargeInteger.One, "Generator is wrong");
        }
    }

}
