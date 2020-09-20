using System.Threading.Tasks;
using Phantasma.Ethereum.Signer.Crypto;

namespace Phantasma.Ethereum.Signer
{
#if !DOTNET35

    public enum ExternalSignerTransactionFormat
    {
        RLP,
        Hash,
        Transaction
    }
    
#endif
}