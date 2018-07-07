using System;

namespace Phantasma.Core
{
    public enum GovernanceSubject
    {
        NetworkProtocol,    // controls version of network protocol
        FeeMultiplier,      // controls multiplier used for fee calculation
        FeeAllocation,      // controls percentage of distribution of block fees
        GovernanceContract, // TODO control system contract migration
        DistributionContract, // TODO 
        StakeContract,
        StakeLimit, // minimum stakable amount
        BlockLimit,
        TransactionLimit,
        ShardLimit
    }
}
