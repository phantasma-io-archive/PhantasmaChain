using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.VM;
using System;
using System.Collections.Generic;

namespace Phantasma.API
{
    public static class APIUtils
    {
        public static VMObject ExecuteScript(byte[] script, ContractInterface abi, string methodName, params object[] args)
        {
            var method = abi.FindMethod(methodName);

            if (method == null)
            {
                throw new Exception("ABI is missing: " + method.name);
            }

            var vm = new GasMachine(script, (uint)method.offset);

            // TODO maybe this needs to be in inverted order?
            foreach (var arg in args)
            {
                vm.Stack.Push(VMObject.FromObject(arg));
            }

            var result = vm.Execute();
            if (result == ExecutionState.Halt)
            {
                return vm.Stack.Pop();
            }

            throw new Exception("Script execution failed for: " + method.name);
        }

        internal static void FetchProperty(string methodName, ITokenSeries series, BigInteger tokenID, List<TokenPropertyResult> properties)
        {
            if (series.ABI.HasMethod(methodName))
            {
                var result = ExecuteScript(series.Script, series.ABI, methodName, tokenID);

                string propName = methodName;

                if (propName.StartsWith("is"))
                {
                    propName = propName.Substring(2);
                }
                else
                if (propName.StartsWith("get"))
                {
                    propName = propName.Substring(3);
                }

                properties.Add(new TokenPropertyResult() { Key = propName, Value = result.AsString() });
            }
        }
    }
}
