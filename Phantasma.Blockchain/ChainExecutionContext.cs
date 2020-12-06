using System;
using System.Numerics;
using Phantasma.Blockchain.Contracts;
using Phantasma.VM;
using Phantasma.Domain;
using System.Collections.Generic;
using Phantasma.Core.Performance;

namespace Phantasma.Blockchain
{
    public class ChainExecutionContext : ExecutionContext
    {
        public readonly SmartContract Contract;

        public override string Name => Contract.Name;

        public ChainExecutionContext(SmartContract contract)
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
                throw new VMException(frame.VM, $"VM nativecall failed: method name not present in the VM stack {frame.Context.Name}");
            }

            var stackObj = stack.Pop();
            var methodName = stackObj.AsString();
            
            var runtime = (RuntimeVM)frame.VM;

            if (methodName.Equals(SmartContract.ConstructorName, StringComparison.OrdinalIgnoreCase) && runtime.HasGenesis)
            {
                BigInteger usedQuota;
                
                if (Nexus.IsNativeContract(Contract.Name)) 
                {
                    usedQuota = 1024; // does not matter what number, just than its greater than 0
                }
                else
                {
                    usedQuota = runtime.CallNativeContext(NativeContractKind.Storage, nameof(StorageContract.GetUsedDataQuota), this.Contract.Address).AsNumber();
                }

                if (usedQuota > 0)
                {
                    throw new VMException(frame.VM, $"VM nativecall failed: constructor can only be called once");
                }
            }

            var method = this.Contract.ABI.FindMethod(methodName);

            if (method == null)
            {
                throw new VMException(frame.VM, $"VM nativecall failed: contract '{this.Contract.Name}' does not have method '{methodName}' in its ABI");
            }

            if (stack.Count < method.parameters.Length)
            {
                throw new VMException(frame.VM, $"VM nativecall failed: calling method {methodName} with {stack.Count} arguments instead of {method.parameters.Length}");
            }

            ExecutionState result;

            BigInteger gasCost = 10;
            result = runtime.ConsumeGas(gasCost);
            if (result != ExecutionState.Running)
            {
                return result;
            }

                var native = Contract as NativeContract;
            if (native != null)
            {
                using (var m = new ProfileMarker("InternalCall"))
                {
                    try
                    {            
                        native.SetRuntime(runtime);
                        native.LoadFromStorage(runtime.Storage);

                        result = InternalCall(native, method, frame, stack);
                        native.SaveChangesToStorage();
                     
                    }
                    catch (ArgumentException ex)
                    {
                        throw new VMException(frame.VM, $"VM nativecall failed: calling method {methodName} with arguments of wrong type, " + ex.ToString());
                    }
                }
            }
            else
            {
                var custom = Contract as CustomContract;
                
                if (custom != null)
                {
                    if (method.offset < 0)
                    {
                        throw new VMException(frame.VM, $"VM context call failed: abi contains invalid offset for {method.name}");
                    }

#if SUSHI_MODE
                    var debugPath = @"C:\Code\Poltergeist\Builds\Windows\debug.asm";
                    var disasm = new VM.Disassembler(custom.Script);
                    var asm = string.Join("\n", disasm.Instructions.Select(x => x.ToString()));
                    System.IO.File.WriteAllText(debugPath, asm);
#endif

                    var context = new ScriptContext(Contract.Name, custom.Script, (uint)method.offset);
                    result = context.Execute(frame, stack);
                }
                else
                {
                    throw new VMException(frame.VM, $"VM context call failed: unknown contract instance class {Contract.GetType().Name}");
                }

            }


            // we terminate here execution, since it will be restarted in next context
            if (result == ExecutionState.Running)
            {
                result = ExecutionState.Halt;
            }

            return result;
        }

        private ExecutionState InternalCall(NativeContract contract, ContractMethod method, ExecutionFrame frame, Stack<VMObject> stack)
        {
            var args = new object[method.parameters.Length];
            for (int i = 0; i < args.Length; i++)
            {
                var arg = stack.Pop();
                args[i] = arg.Data;
            }

            object result;
            using (var m = new ProfileMarker(method.name))
                result = contract.CallInternalMethod((RuntimeVM) frame.VM, method.name, args);

            if (method.returnType != VMType.None)
            {
                var obj = VMObject.FromObject(result);
                stack.Push(obj);
            }

            return ExecutionState.Running;
        }
    }
}
