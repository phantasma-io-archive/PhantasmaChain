using Phantasma.Blockchain.Contracts;
using Phantasma.VM;
using System;
using System.Collections.Generic;

namespace Phantasma.Blockchain
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
            for (int i=args.Length - 1; i>=0; i--) {
                args[i] = stack.Pop();
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
