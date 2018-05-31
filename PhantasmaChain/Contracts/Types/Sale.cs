using Phantasma.Contracts.Interfaces;
using System.Numerics;

namespace Phantasma.Contracts.Types
{
    public enum SaleState
    {
        Pending,
        Active,
        Completed,
        Cancelled
    }

    public interface ISale : IContract
    {
        IFungibleToken GetToken();
        SaleState GetState();

        BigInteger GetSoftCap();
        BigInteger GetHardCap();

        BigInteger GetRate();

        [Payable]
        bool Contribute();
    }

}
