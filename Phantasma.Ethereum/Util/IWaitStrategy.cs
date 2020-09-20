using System.Threading.Tasks;

namespace Phantasma.Ethereum.Utils
{
    public interface IWaitStrategy
    {
        Task Apply(uint retryCount);
    }
}