using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.VM;
using Phantasma.Core.Performance;
using System;
using System.Collections.Generic;
using Phantasma.Blockchain.Contracts;
using Phantasma.Core.Utils;

namespace Phantasma.Blockchain
{
    public class CustomExecutionContext : ExecutionContext
    {
        public readonly CustomContract Contract;

        public override string Name => Contract.Name;

        public CustomExecutionContext(CustomContract contract)
        {
            this.Contract = contract;
        }

        public override ExecutionState Execute(ExecutionFrame frame, Stack<VMObject> stack)
        {
            if (stack.Count <= 0)
            {
                throw new VMException(frame.VM, $"VM nativecall failed: method name not present in the VM stack");
            }

            var runtime = (RuntimeVM)frame.VM;

            BigInteger gasCost = 50;
            var result = runtime.ConsumeGas(gasCost);
            if (result != ExecutionState.Running)
            {
                return result;
            }

            var context = new ScriptContext(Contract.Name, Contract.Script);
            result = context.Execute(frame, stack);

            // we terminate here execution, since it will be restarted in next context
            if (result == ExecutionState.Running)
            {
                result = ExecutionState.Halt;
            }

            return result;
        }

        private ExecutionState InternalCall(ContractMethod method, ExecutionFrame frame, Stack<VMObject> stack)
        {
            var args = new object[method.parameters.Length];
            for (int i = 0; i < args.Length; i++)
            {
                var arg = stack.Pop();
                args[i] = arg.Data;
            }

            object result;
            using (var m = new ProfileMarker(method.name))
                result = this.Contract.CallInternalMethod((RuntimeVM)frame.VM, method.name, args);

            if (method.returnType != VMType.None)
            {
                var obj = VMObject.FromObject(result);
                stack.Push(obj);
            }

            return ExecutionState.Running;
        }
    }
}
