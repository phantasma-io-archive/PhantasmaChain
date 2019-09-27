using Phantasma.Contracts;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.VM;
using System;
using System.Collections.Generic;

namespace Phantasma.Blockchain.Contracts
{
    public class NativeExecutionContext : ExecutionContext
    {
        public readonly SmartContract Contract;

        public override string Name => Contract.Name;
        public override bool Admin => true; // TODO change this later if necessary

        public NativeExecutionContext(SmartContract contract)
        {
            this.Contract = contract;
        }

        public override ExecutionState Execute(ExecutionFrame frame, Stack<VMObject> stack)
        {
            if (this.Contract.ABI == null)
            {
#if DEBUG
                throw new VMDebugException(frame.VM, $"VM nativecall failed: ABI is missing for contract '{this.Contract.Name}'");
#else                            
                return ExecutionState.Fault;
#endif
            }

            if (stack.Count <= 0)
            {
#if DEBUG
                throw new VMDebugException(frame.VM, $"VM nativecall failed: method name not present in the VM stack");
#else
                                return ExecutionState.Fault;
#endif
            }

            var stackObj = stack.Pop();
            var methodName = stackObj.AsString();
            var method = this.Contract.ABI.FindMethod(methodName);

            if (method == null)
            {
#if DEBUG
                throw new VMDebugException(frame.VM, $"VM nativecall failed: contract '{this.Contract.Name}' does not have method '{methodName}' in its ABI");
#else
                return ExecutionState.Fault;
#endif
            }

            if (stack.Count < method.parameters.Length)
            {
#if DEBUG
                throw new VMDebugException(frame.VM, $"VM nativecall failed: calling method {methodName} with {stack.Count} arguments instead of {method.parameters.Length}");
#else
                return ExecutionState.Fault;
#endif
            }

            var runtime = (RuntimeVM)frame.VM;

            BigInteger gasCost;
            if (this.Contract.HasInternalMethod(methodName, out gasCost))
            {
                ExecutionState result;
                try
                {
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
#if DEBUG
                    throw new VMDebugException(frame.VM, $"VM nativecall failed: calling method {methodName} with arguments of wrong type, " + ex.ToString());
#else
                    result = ExecutionState.Fault;
#endif
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
#if DEBUG
                throw new VMDebugException(frame.VM, $"VM nativecall failed: contract '{this.Contract.Name}' is not a valid custom contract");
#else
                return ExecutionState.Fault;
#endif
            }

            stack.Push(stackObj);

            var context = new ScriptContext(customContract.Name, customContract.Script, true);
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

            var result = this.Contract.CallInternalMethod((RuntimeVM) frame.VM, method.name, args);

            if (method.returnType != VMType.None)
            {
                var obj = VMObject.FromObject(result);
                stack.Push(obj);
            }

            return ExecutionState.Running;
        }
    }
}
