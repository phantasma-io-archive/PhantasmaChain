using Phantasma.Contracts;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.VM;
using Phantasma.Core.Performance;
using System;
using System.Collections.Generic;

namespace Phantasma.Blockchain.Contracts
{
    public class NativeExecutionContext : ExecutionContext
    {
        public readonly SmartContract Contract;

        public override string Name => Contract.Name;

        public NativeExecutionContext(SmartContract contract)
        {
            this.Contract = contract;
        }

        public override ExecutionState Execute(ExecutionFrame frame, Stack<VMObject> stack)
        {
            if (this.Contract.ABI == null)
            {
                throw new VMException(frame.VM, $"VM nativecall failed: ABI is missing for contract '{this.Contract.Name}'");
            }

            if (stack.Count <= 0)
            {
                throw new VMException(frame.VM, $"VM nativecall failed: method name not present in the VM stack");
            }

            var stackObj = stack.Pop();
            var methodName = stackObj.AsString();
            var method = this.Contract.ABI.FindMethod(methodName);

            if (method == null)
            {
                throw new VMException(frame.VM, $"VM nativecall failed: contract '{this.Contract.Name}' does not have method '{methodName}' in its ABI");
            }

            if (stack.Count < method.parameters.Length)
            {
                throw new VMException(frame.VM, $"VM nativecall failed: calling method {methodName} with {stack.Count} arguments instead of {method.parameters.Length}");
            }

            var runtime = (RuntimeVM)frame.VM;

            using (var m = new ProfileMarker("InternalCall"))
            if (this.Contract.HasInternalMethod(methodName))
            {
                ExecutionState result;
                try
                {
                    BigInteger gasCost = 10;
                    result = runtime.ConsumeGas(gasCost);
                    if (result == ExecutionState.Running)
                    {
                        Contract.LoadRuntimeData(runtime);
                        result = InternalCall(method, frame, stack);
                        Contract.UnloadRuntimeData();
                    }
                }
                catch (ArgumentException ex)
                {
                    throw new VMException(frame.VM, $"VM nativecall failed: calling method {methodName} with arguments of wrong type, " + ex.ToString());
                }

                // we terminate here execution, since it will be restarted in next context
                if (result == ExecutionState.Running)
                {
                    result = ExecutionState.Halt;
                }

                return result;
            }


            if (!(this.Contract is CustomContract customContract))
            {
                throw new VMException(frame.VM, $"VM nativecall failed: contract '{this.Contract.Name}' is not a valid custom contract");
            }

            stack.Push(stackObj);

            var context = new ScriptContext(customContract.Name, customContract.Script);
            return context.Execute(frame, stack);
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
                result = this.Contract.CallInternalMethod((RuntimeVM) frame.VM, method.name, args);

            if (method.returnType != VMType.None)
            {
                var obj = VMObject.FromObject(result);
                stack.Push(obj);
            }

            return ExecutionState.Running;
        }
    }
}
