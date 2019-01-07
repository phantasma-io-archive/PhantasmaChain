using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using Phantasma.Numerics;
using Phantasma.Cryptography;
using Phantasma.VM;
using System.Collections.Generic;
using Phantasma.Blockchain;
using Phantasma.VM.Utils;

namespace Phantasma.Tests
{
    public class TestVM : VirtualMachine
    {
        private Dictionary<string, Func<ExecutionFrame, ExecutionState>> _interops = new Dictionary<string, Func<ExecutionFrame, ExecutionState>>();
        private Func<string, ExecutionContext> _contextLoader;

        public TestVM(byte[] script) : base(script)
        {
        }

#if DEBUG
        public override ExecutionState HandleException(VMDebugException ex)
        {
            return ExecutionState.Fault;
        }
#endif

        public void RegisterInterop(string method, Func<ExecutionFrame, ExecutionState> callback)
        {
            _interops[method] = callback;
        }

        public void RegisterContextLoader(Func<string, ExecutionContext> callback)
        {
            _contextLoader = callback;
        }

        public override ExecutionState ExecuteInterop(string method)
        {
            if (_interops.ContainsKey(method))
            {
                return _interops[method](this.CurrentFrame);
            }

            throw new NotImplementedException();
        }

        public override ExecutionContext LoadContext(string contextName)
        {
            if (_contextLoader != null)
            {
                return _contextLoader(contextName);
            }

            throw new NotImplementedException();
        }

        public void RegisterDefaultInterops()
        {
            RegisterInterop("Upper", (frame) =>
            {
                var obj = frame.VM.Stack.Pop();
                var str = obj.AsString();
                str = str.ToUpper();
                frame.VM.Stack.Push(VMObject.FromObject(str));
                return ExecutionState.Running;
            });
        } 
    }

    [TestClass]
    public class VMTests
    {
        [TestMethod]
        public void Interop()
        {
            var source = KeyPair.Generate();
            var script = ScriptUtils.BeginScript().CallInterop("Upper", "hello").EndScript();

            var vm = new TestVM(script);
            vm.RegisterDefaultInterops();
            var state = vm.Execute();
            Assert.IsTrue(state == ExecutionState.Halt);

            Assert.IsTrue(vm.Stack.Count == 1);

            var result = vm.Stack.Pop().AsString();
            Assert.IsTrue(result == "HELLO");
        }    
    }
}
