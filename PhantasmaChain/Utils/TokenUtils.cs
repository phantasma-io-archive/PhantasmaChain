using Phantasma.Blockchain;
using Phantasma.Mathematics;
using Phantasma.VM.Types;

namespace Phantasma.Utils
{
    public static class TokenUtils
    {
        public static BigInteger GetTokenBalance(this Chain chain, Address tokenAddress, Address accountAddress)
        {
            var contract = chain.FindContract(tokenAddress.PublicKey);

            if (contract == null)
            {
                return 0;
            }

            var balance = (BigInteger)VMUtils.InvokeScript(contract.Script, new object[] { "balanceOf", accountAddress });
            return balance;
        }
    }
}
