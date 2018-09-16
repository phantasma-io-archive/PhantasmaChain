using Phantasma.VM;
using Phantasma.VM.Contracts;
using System;
using System.Collections.Generic;

namespace Phantasma.Blockchain
{
    public enum NativeABI
    {
        Token
    }

    public partial class Chain
    {
        private static Dictionary<NativeABI, ContractInterface> _abis = null;

        private static void CreateABIs()
        {
            _abis = new Dictionary<NativeABI, ContractInterface>();

            var methods = new List<ContractMethod>();

            methods.Add(new ContractMethod("Transfer", VMType.None, VMType.Object, VMType.Object, VMType.Number));
            methods.Add(new ContractMethod("BalanceOf", VMType.Number, VMType.Object));
            methods.Add(new ContractMethod("Mint", VMType.None, VMType.Object, VMType.Number));
            methods.Add(new ContractMethod("Burn", VMType.None, VMType.Object, VMType.Number));
            methods.Add(new ContractMethod("GetSupply", VMType.Number));
            methods.Add(new ContractMethod("GetName", VMType.String));
            methods.Add(new ContractMethod("GetSymbol", VMType.String));

            _abis[NativeABI.Token] = new ContractInterface(methods);

            methods.Clear();
        }

        public static ContractInterface FindABI(NativeABI abi)
        {
            if (_abis == null)
            {
                CreateABIs();
            }

            return _abis.ContainsKey(abi) ? _abis[abi] : null;
        }
    }
}
