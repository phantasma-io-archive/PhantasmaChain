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

            vm.RegisterMethod("Address()", Constructor_Address);
            vm.RegisterMethod("Hash()", Constructor_Hash);
            vm.RegisterMethod("Timestamp()", Constructor_Timestamp);
            vm.RegisterMethod("ABI()", Constructor_ABI);
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
