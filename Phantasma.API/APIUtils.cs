using Phantasma.Domain;
using Phantasma.VM;
using System;
using System.Collections.Generic;

namespace Phantasma.API
{
    public static class APIUtils
    {
        public static VMObject ExecuteScript(byte[] script, ContractInterface abi, string methodName)
        {
            var method = abi.FindMethod(methodName);

            if (method == null)
            {
                throw new Exception("ABI is missing: " + method.name);
            }

            var vm = new GasMachine(script, (uint)method.offset);
            var result = vm.Execute();
            if (result == ExecutionState.Halt)
            {
                return vm.Stack.Pop();
            }

            throw new Exception("Script execution failed for: " + method.name);
        }

        internal static void FetchProperty(string propertyName, IToken tokenInfo, List<TokenPropertyResult> properties)
        {
            if (tokenInfo.ABI.HasMethod(propertyName))
            {
                var result = ExecuteScript(tokenInfo.Script, tokenInfo.ABI, propertyName);
                properties.Add(new TokenPropertyResult() { Key = propertyName, Value = result.ToString() });
            }
        }
    }
}
