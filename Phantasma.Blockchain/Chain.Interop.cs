using System;
using System.IO;
using Phantasma.Blockchain.Contracts;
using Phantasma.Cryptography;
using Phantasma.Core;
using Phantasma.VM;
using Phantasma.VM.Contracts;
using Phantasma.Core.Types;
using Phantasma.Numerics;

namespace Phantasma.Blockchain
{
    public partial class Chain
    {
        internal static void RegisterInterop(RuntimeVM vm)
        {
            vm.RegisterMethod("Runtime.Log", Runtime_Log);
            vm.RegisterMethod("Runtime.Event", Runtime_Event);

            vm.RegisterMethod("ABI()", Constructor_ABI);

            vm.RegisterMethod("Oracle.Read", Oracle_Read);
            vm.RegisterMethod("Oracle.Register", Oracle_Register);
            vm.RegisterMethod("Oracle.List", Oracle_List);

            vm.RegisterMethod("Address()", Constructor_Address);
            vm.RegisterMethod("Hash()", Constructor_Hash);
            vm.RegisterMethod("Timestamp()", Constructor_Timestamp);
        }

        private static ExecutionState Constructor_Object<IN,OUT>(RuntimeVM vm, Func<IN, OUT> loader) 
        {
            var type = VMObject.GetVMType(typeof(IN));
            var input = vm.Stack.Pop().AsType(type);

            try
            {
                OUT obj = loader((IN)input);
                var temp = new VMObject();
                temp.SetValue(obj);
                vm.Stack.Push(temp);
            }
            catch 
            {
                return ExecutionState.Fault;
            }

            return ExecutionState.Running;
        }

        
        private static ExecutionState Constructor_Address(RuntimeVM vm)
        {
            return Constructor_Object<byte[], Address>(vm, bytes =>
            {
                Throw.If(bytes == null || bytes.Length != Address.PublicKeyLength, "invalid key");
                return new Address(bytes);
            });
        }

        private static ExecutionState Constructor_Hash(RuntimeVM vm)
        {
            return Constructor_Object<byte[], Hash>(vm, bytes =>
            {
                Throw.If(bytes == null || bytes.Length != Hash.Length, "invalid hash");
                return new Hash(bytes);
            });
        }

        private static ExecutionState Constructor_Timestamp(RuntimeVM vm)
        {
            return Constructor_Object<BigInteger, Timestamp>(vm, val =>
            {
                Throw.If(val < 0, "invalid number");
                return new Timestamp((uint)val);
            });
        }

        private static ExecutionState Constructor_ABI(RuntimeVM vm)
        {
            return Constructor_Object<byte[], ContractInterface>(vm, bytes =>
            {
                Throw.If(bytes == null, "invalid abi");

                using (var stream = new MemoryStream(bytes))
                {
                    using (var reader = new BinaryReader(stream))
                    {
                        return ContractInterface.Unserialize(reader);
                    }
                }
            });
        }

        private static ExecutionState Runtime_Log(RuntimeVM vm)
        {
            var text = vm.Stack.Pop().AsString();
            //this.Log.Write(Core.Log.LogEntryKind.Message, text);
            Console.WriteLine(text); // TODO fixme
            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_Event(RuntimeVM vm)
        {
            var bytes = vm.Stack.Pop().AsByteArray();
            var address = vm.Stack.Pop().AsInterop<Address>();
            var kind = vm.Stack.Pop().AsEnum<EventKind>();

            vm.Notify(kind, address, bytes);
            return ExecutionState.Running;
        }

        // TODO proper exceptions
        private static ExecutionState Oracle_Read(RuntimeVM vm)
        {
            if (vm.Stack.Count < 1)
            {
                return ExecutionState.Fault;
            }

            var temp = vm.Stack.Pop();
            if (temp.Type != VMType.String)
            {
                return ExecutionState.Fault;
            }

            var url = temp.AsString();

            if (vm.OracleReader == null)
            {
                return ExecutionState.Fault;
            }
            
            url = url.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(url))
            {
                return ExecutionState.Fault;
            }

            var result = vm.OracleReader(/*vm.Transaction.Hash, */url);

            return ExecutionState.Running;
        }

        private static ExecutionState Oracle_Register(RuntimeVM vm)
        {
            if (vm.Stack.Count < 2)
            {
                return ExecutionState.Fault;
            }

            VMObject temp;

            temp = vm.Stack.Pop();
            if (temp.Type != VMType.Object)
            {
                return ExecutionState.Fault;
            }

            var address = temp.AsInterop<Address>();

            temp = vm.Stack.Pop();
            if (temp.Type != VMType.String)
            {
                return ExecutionState.Fault;
            }

            var name = temp.AsString();

            return ExecutionState.Running;
        }

        // should return list of all registered oracles
        private static ExecutionState Oracle_List(RuntimeVM vm)
        {
            throw new NotImplementedException();
        }

        /*private static ExecutionState Contract_Deploy(RuntimeVM vm)
        {
            var script = vm.currentFrame.GetRegister(0).AsByteArray();
            var abi = vm.currentFrame.GetRegister(1).AsByteArray();

            var runtime = (RuntimeVM)vm;
            var tx = runtime.Transaction;

            var contract = new CustomContract(script, abi);

            //Log.Message($"Deploying contract: Address??");

            var obj = new VMObject();
            obj.SetValue(contract);
            vm.Stack.Push(obj);

            return ExecutionState.Running;
        }
        */

    }
}
