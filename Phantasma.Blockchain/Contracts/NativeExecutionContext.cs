using Phantasma.Blockchain.Contracts.Native;
using Phantasma.VM;
using System.Collections.Generic;

namespace Phantasma.Blockchain.Contracts
{
    public class NativeExecutionContext : ExecutionContext
    {
        public readonly NativeContract Contract;

        public NativeExecutionContext(NativeContract contract)
        {
            this.Contract = contract;
        }

        public override ExecutionState Execute(ExecutionFrame frame, Stack<VMObject> stack)
        {
            var methodName = stack.Pop().AsString();
            var method = this.Contract.ABI.FindMethod(methodName);

            if (stack.Count < method.parameters.Length)
            {
                return ExecutionState.Fault;
            }

            var args = new object[method.parameters.Length];
            for (int i=0; i<args.Length; i++) {
                var arg = stack.Pop();

                var temp = arg.Data;

                // when a string is passed instead of an address we do an automatic lookup and replace
                if (method.parameters[i] == VMType.Object && temp is string)
                {
                    var name = (string)temp;
                    var runtime = (RuntimeVM)frame.VM;
                    var address = runtime.Nexus.LookUpName(name);
                    temp = address;
                }

                args[i] = temp;
            }

            var result = this.Contract.CallMethod(methodName, args);

            if (method.returnType != VMType.None)
            {
                var obj = VMObject.FromObject(result);
                stack.Push(obj);
            }

            return ExecutionState.Running;
        }

        public override int GetSize()
        {
            return 0;
        }
    }
}
