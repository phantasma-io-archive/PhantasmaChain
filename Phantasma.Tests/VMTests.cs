using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using Phantasma.Numerics;
using Phantasma.Cryptography;
using Phantasma.VM;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Phantasma.Blockchain;
using Phantasma.CodeGen.Assembler;
using Phantasma.VM.Utils;

namespace Phantasma.Tests
{
    public class TestVM : VirtualMachine
    {
        private Dictionary<string, Func<ExecutionFrame, ExecutionState>> _interops = new Dictionary<string, Func<ExecutionFrame, ExecutionState>>();
        private Func<string, ExecutionContext> _contextLoader;
        private Dictionary<string, ScriptContext> contexts;

        public enum DebugEnum
        {
            enum1,
            enum2,
            enum3
        }

        public struct DebugStruct
        {
            public int x;
            public int y;
        }

        public class DebugClass
        {
            public int x;

            public DebugClass(int y)
            {
                x = y;
            }
        }

        public TestVM(byte[] script, uint offset) : base(script, offset)
        {
            RegisterDefaultInterops();
            RegisterContextLoader(ContextLoader);
            contexts = new Dictionary<string, ScriptContext>();
        }

        private ExecutionContext ContextLoader(string contextName)
        {
            if (contexts.ContainsKey(contextName))
                return contexts[contextName];

            if (contextName == "test")
            {
                var scriptString = new string[]
                {
                $"pop r1",
                $"inc r1",
                $"push r1",
                @"ret",
                };

                var byteScript = BuildScript(scriptString);

                contexts.Add(contextName, new ScriptContext("test", byteScript));

                return contexts[contextName];
            }

            return null;
        }

        public byte[] BuildScript(string[] lines)
        {
            IEnumerable<Semanteme> semantemes = null;
            try
            {
                semantemes = Semanteme.ProcessLines(lines);
            }
            catch (Exception e)
            {
                throw new InternalTestFailureException("Error parsing the script");
            }

            var sb = new ScriptBuilder();
            byte[] script = null;

            try
            {
                foreach (var entry in semantemes)
                {
                    Trace.WriteLine($"{entry}");
                    entry.Process(sb);
                }
                script = sb.ToScript();
            }
            catch (Exception e)
            {
                throw new InternalTestFailureException("Error assembling the script");
            }

            return script;
        }

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

            RegisterInterop("PushEnum", (frame) =>
            {
                var obj = frame.VM.Stack.Pop();
                var n = obj.AsNumber();
                DebugEnum enm;

                switch (n.ToDecimal())
                {
                    case "1":
                        enm = DebugEnum.enum1;
                        break;
                    case "2":
                        enm = DebugEnum.enum2;
                        break;
                    default:
                        enm = DebugEnum.enum3;
                        break;
                }
                
                frame.VM.Stack.Push(VMObject.FromObject(enm));
                return ExecutionState.Running;
            });

            RegisterInterop("PushDebugClass", (frame) =>
            {
                var obj = frame.VM.Stack.Pop();
                int n = (int)obj.AsNumber();

                DebugClass dbClass = new DebugClass(n);
                
                frame.VM.Stack.Push(VMObject.FromObject(dbClass));
                return ExecutionState.Running;
            });

            RegisterInterop("IncrementDebugClass", (frame) =>
            {
                var obj = frame.VM.Stack.Pop();
                var dbClass = obj.AsInterop<DebugClass>();

                dbClass.x++;

                frame.VM.Stack.Push(VMObject.FromObject(dbClass));
                return ExecutionState.Running;
            });

            RegisterInterop("PushBytes", (frame) =>
            {
                var obj = frame.VM.Stack.Pop();
                string str = obj.AsString();

                var byteArray = Encoding.ASCII.GetBytes(str);

                frame.VM.Stack.Push(VMObject.FromObject(byteArray));
                return ExecutionState.Running;
            });

            RegisterInterop("PushDebugStruct", (frame) =>
            {
                var obj = frame.VM.Stack.Pop();
                int n = (int) obj.AsNumber();

                DebugStruct dbStruct = new DebugStruct();
                dbStruct.x = n;
                dbStruct.y = n;

                frame.VM.Stack.Push(VMObject.FromObject(dbStruct));
                return ExecutionState.Running;
            });

            RegisterInterop("IncrementDebugStruct", (frame) =>
            {
                var obj = frame.VM.Stack.Pop();
                var dbStruct = obj.AsInterop<DebugStruct>();

                dbStruct.x++;

                frame.VM.Stack.Push(VMObject.FromObject(dbStruct));
                return ExecutionState.Running;
            });
        }

        public override void DumpData(List<string> lines)
        {
            // do nothing
        }
    }

    [TestClass]
    public class VMTests
    {
        [TestMethod]
        public void Interop()
        {
            var source = PhantasmaKeys.Generate();
            var script = ScriptUtils.BeginScript().CallInterop("Upper", "hello").EndScript();

            var vm = new TestVM(script, 0);
            vm.RegisterDefaultInterops();
            var state = vm.Execute();
            Assert.IsTrue(state == ExecutionState.Halt);

            Assert.IsTrue(vm.Stack.Count == 1);

            var result = vm.Stack.Pop().AsString();
            Assert.IsTrue(result == "HELLO");
        }    
    }
}
