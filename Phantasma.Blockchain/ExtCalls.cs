using System;
using System.IO;
using Phantasma.Blockchain.Contracts;
using Phantasma.Cryptography;
using Phantasma.Core;
using Phantasma.VM;
using Phantasma.Core.Types;
using Phantasma.Numerics;
using Phantasma.Domain;
using Phantasma.Storage;
using Phantasma.Contracts;

namespace Phantasma.Blockchain
{
    public static class ExtCalls
    {
        // naming scheme should be "namespace.methodName" for methods, and "type()" for constructors
        internal static void RegisterWithRuntime(RuntimeVM vm)
        {
            vm.RegisterMethod("Runtime.Log", Runtime_Log);
            vm.RegisterMethod("Runtime.Event", Runtime_Event);
            vm.RegisterMethod("Runtime.IsWitness", Runtime_IsWitness);
            vm.RegisterMethod("Runtime.IsTrigger", Runtime_IsTrigger);
            vm.RegisterMethod("Runtime.DeployContract", Runtime_DeployContract);
            vm.RegisterMethod("Runtime.TransferTokens", Runtime_TransferTokens);
            vm.RegisterMethod("Runtime.TransferBalance", Runtime_TransferBalance);
            vm.RegisterMethod("Runtime.MintTokens", Runtime_MintTokens);
            vm.RegisterMethod("Runtime.BurnTokens", Runtime_BurnTokens);
            vm.RegisterMethod("Runtime.TransferToken", Runtime_TransferToken);
            vm.RegisterMethod("Runtime.MintToken", Runtime_MintToken);
            vm.RegisterMethod("Runtime.BurnToken", Runtime_BurnToken);
            vm.RegisterMethod("Runtime.CreateToken", Runtime_CreateToken);
            vm.RegisterMethod("Runtime.CreateChain", Runtime_CreateChain);
            vm.RegisterMethod("Runtime.CreatePlatform", Runtime_CreatePlatform);

            vm.RegisterMethod("Data.Get", Data_Get);
            vm.RegisterMethod("Data.Set", Data_Set);
            vm.RegisterMethod("Data.Delete", Data_Delete);

            vm.RegisterMethod("Oracle.Read", Oracle_Read);
            vm.RegisterMethod("Oracle.Price", Oracle_Price);
            vm.RegisterMethod("Oracle.Quote", Oracle_Quote);
            // TODO
            //vm.RegisterMethod("Oracle.Block", Oracle_Block);
            //vm.RegisterMethod("Oracle.Transaction", Oracle_Transaction);
            /*vm.RegisterMethod("Oracle.Register", Oracle_Register);
            vm.RegisterMethod("Oracle.List", Oracle_List);
            */

            vm.RegisterMethod("ABI()", Constructor_ABI);
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

        #region ORACLES
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

            if (vm.Oracle == null)
            {
                return ExecutionState.Fault;
            }
            
            url = url.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(url))
            {
                return ExecutionState.Fault;
            }

            var result = vm.Oracle.Read(/*vm.Transaction.Hash, */url);

            return ExecutionState.Running;
        }

        private static ExecutionState Oracle_Price(RuntimeVM vm)
        {
            if (vm.Stack.Count < 1)
            {
                return ExecutionState.Fault;
            }

            VMObject temp;

            temp = vm.Stack.Pop();
            if (temp.Type != VMType.String)
            {
                return ExecutionState.Fault;
            }

            var symbol = temp.AsString();

            var price = vm.GetTokenPrice(symbol);

            vm.Stack.Push(VMObject.FromObject(price));

            return ExecutionState.Running;
        }

        private static ExecutionState Oracle_Quote(RuntimeVM vm)
        {
            if (vm.Stack.Count < 3)
            {
                return ExecutionState.Fault;
            }

            VMObject temp;

            temp = vm.Stack.Pop();
            if (temp.Type != VMType.Number)
            {
                return ExecutionState.Fault;
            }

            var amount = temp.AsNumber();

            temp = vm.Stack.Pop();
            if (temp.Type != VMType.String)
            {
                return ExecutionState.Fault;
            }

            var quoteSymbol = temp.AsString();

            temp = vm.Stack.Pop();
            if (temp.Type != VMType.String)
            {
                return ExecutionState.Fault;
            }

            var baseSymbol = temp.AsString();

            var price = vm.GetTokenQuote(baseSymbol, quoteSymbol, amount);

            vm.Stack.Push(VMObject.FromObject(price));

            return ExecutionState.Running;
        }

        /*
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
        }*/

        #endregion

        private static ExecutionState Runtime_IsWitness(RuntimeVM vm)
        {
            try
            {
                var tx = vm.Transaction;
                Throw.IfNull(tx, nameof(tx));

                if (vm.Stack.Count < 1)
                {
                    return ExecutionState.Fault;
                }

                var temp = vm.Stack.Pop();

                if (temp.Type != VMType.Object)
                {
                    return ExecutionState.Fault;
                }

                var address = temp.AsInterop<Address>();

                var success = tx.IsSignedBy(address);

                var result = new VMObject();
                result.SetValue(success);
                vm.Stack.Push(result);
            }
            catch
            {
                return ExecutionState.Fault;
            }

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_IsTrigger(RuntimeVM vm)
        {
            try
            {
                var tx = vm.Transaction;
                Throw.IfNull(tx, nameof(tx));

                var success = vm.IsTrigger;

                var result = new VMObject();
                result.SetValue(success);
                vm.Stack.Push(result);
            }
            catch
            {
                return ExecutionState.Fault;
            }

            return ExecutionState.Running;
        }

        private static ExecutionState Data_Get(RuntimeVM runtime)
        {
            var key = runtime.Stack.Pop();
            var key_bytes = key.AsByteArray();

            runtime.Expect(key_bytes.Length > 0, "invalid key");

            var value_bytes = runtime.Storage.Get(key_bytes);
            var val = new VMObject();
            val.SetValue(value_bytes, VMType.Bytes);
            runtime.Stack.Push(val);

            return ExecutionState.Running;
        }

        private static ExecutionState Data_Set(RuntimeVM runtime)
        {
            var key = runtime.Stack.Pop();
            var key_bytes = key.AsByteArray();

            var val = runtime.Stack.Pop();
            var val_bytes = val.AsByteArray();

            runtime.Expect(key_bytes.Length > 0, "invalid key");

            var firstChar = (char)key_bytes[0];
            runtime.Expect(firstChar != '.', "permission denied"); // NOTE link correct PEPE here

            runtime.Storage.Put(key_bytes, val_bytes);

            return ExecutionState.Running;
        }

        private static ExecutionState Data_Delete(RuntimeVM runtime)
        {
            var key = runtime.Stack.Pop();
            var key_bytes = key.AsByteArray();

            runtime.Expect(key_bytes.Length > 0, "invalid key");

            var firstChar = (char)key_bytes[0];
            runtime.Expect(firstChar != '.', "permission denied"); // NOTE link correct PEPE here

            runtime.Storage.Delete(key_bytes);

            return ExecutionState.Running;
        }

        private static Address PopAddress(RuntimeVM vm)
        {
            var temp = vm.Stack.Pop();
            if (temp.Type == VMType.String)
            {
                var name = temp.AsString();
                return vm.Nexus.LookUpName(vm.Storage, name);
            }
            else
            if (temp.Type == VMType.Bytes)
            {
                var bytes = temp.AsByteArray();
                var addr = Serialization.Unserialize<Address>(bytes);
                return addr;
            }
            else
            {
                var addr = temp.AsInterop<Address>();
                return addr;
            }
        }

        private static ExecutionState Runtime_TransferTokens(RuntimeVM Runtime)
        {
            Runtime.Expect(Runtime.Stack.Count >= 4, "not enough arguments in stack");

            VMObject temp;

            var source = PopAddress(Runtime);
            var destination = PopAddress(Runtime);

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.String, "expected string for symbol");
            var symbol = temp.AsString();

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.Number, "expected number for amount");
            var amount = temp.AsNumber();

            Runtime.TransferTokens(symbol, source, destination, amount);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_TransferBalance(RuntimeVM Runtime)
        {
            Runtime.Expect(Runtime.Stack.Count >= 3, "not enough arguments in stack");

            VMObject temp;

            var source = PopAddress(Runtime);
            var destination = PopAddress(Runtime);

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.String, "expected string for symbol");
            var symbol = temp.AsString();

            var token = Runtime.GetToken(symbol);
            Runtime.Expect(token.IsFungible(), "must be fungible");

            var amount = Runtime.GetBalance(symbol, source);

            Runtime.TransferTokens(symbol, source, destination, amount);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_MintTokens(RuntimeVM Runtime)
        {
            Runtime.Expect(Runtime.Stack.Count >= 4, "not enough arguments in stack");

            VMObject temp;

            var source = PopAddress(Runtime);
            var destination = PopAddress(Runtime);

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.String, "expected string for symbol");
            var symbol = temp.AsString();

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.Number, "expected number for amount");
            var amount = temp.AsNumber();

            if (Runtime.Nexus.HasGenesis)
            {
                Runtime.Expect(symbol != DomainSettings.FuelTokenSymbol && symbol != DomainSettings.StakingTokenSymbol, "cannot mint system tokens after genesis");
            }

            Runtime.MintTokens(symbol, source, destination, amount);

            return ExecutionState.Running;
        }


        private static ExecutionState Runtime_BurnTokens(RuntimeVM Runtime)
        {
            Runtime.Expect(Runtime.Stack.Count >= 3, "not enough arguments in stack");

            VMObject temp;

            var source = PopAddress(Runtime);

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.String, "expected string for symbol");
            var symbol = temp.AsString();

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.Number, "expected number for amount");
            var amount = temp.AsNumber();

            if (Runtime.Nexus.HasGenesis)
            {
                Runtime.Expect(symbol != DomainSettings.FuelTokenSymbol && symbol != DomainSettings.StakingTokenSymbol, "cannot mint system tokens after genesis");
            }

            Runtime.BurnTokens(symbol, source, amount);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_TransferToken(RuntimeVM Runtime)
        {
            Runtime.Expect(Runtime.Stack.Count >= 4, "not enough arguments in stack");

            VMObject temp;

            var source = PopAddress(Runtime);
            var destination = PopAddress(Runtime);

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.String, "expected string for symbol");
            var symbol = temp.AsString();

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.Number, "expected number for amount");
            var tokenID = temp.AsNumber();

            Runtime.TransferToken(symbol, source, destination, tokenID);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_MintToken(RuntimeVM Runtime)
        {
            Runtime.Expect(Runtime.Stack.Count >= 4, "not enough arguments in stack");

            VMObject temp;

            var source = PopAddress(Runtime);
            var destination = PopAddress(Runtime);

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.String, "expected string for symbol");
            var symbol = temp.AsString();

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.Bytes, "expected bytes for rom");
            var rom = temp.AsByteArray();

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.Bytes, "expected bytes for ram");
            var ram = temp.AsByteArray();

            var tokenID = Runtime.MintToken(symbol, source, destination, rom, ram);

            var result = new VMObject();
            result.SetValue(tokenID);
            Runtime.Stack.Push(result);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_BurnToken(RuntimeVM Runtime)
        {
            Runtime.Expect(Runtime.Stack.Count >= 3, "not enough arguments in stack");

            VMObject temp;

            var source = PopAddress(Runtime);

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.String, "expected string for symbol");
            var symbol = temp.AsString();

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.Number, "expected number for amount");
            var tokenID = temp.AsNumber();

            Runtime.BurnToken(symbol, source, tokenID);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_DeployContract(RuntimeVM Runtime)
        {
            var tx = Runtime.Transaction;
            Throw.IfNull(tx, nameof(tx));

            if (Runtime.Stack.Count < 1)
            {
                return ExecutionState.Fault;
            }

            VMObject temp;

            var owner = Runtime.Nexus.GetChainOwnerByName(Runtime.Chain.Name);
            Runtime.Expect(owner.IsUser, "address must be user");

            if (Runtime.Nexus.HasGenesis)
            {
                Runtime.Expect(Runtime.IsStakeMaster(owner), "needs to be master");
            }

            Runtime.Expect(Runtime.IsWitness(owner), "invalid witness");

            temp = Runtime.Stack.Pop();

            bool success;
            switch (temp.Type) 
            {
                case VMType.String:
                    {
                        var name = temp.AsString();
                        success = Runtime.Chain.DeployNativeContract(Runtime.Storage, SmartContract.GetAddressForName(name));

                        var contract = Runtime.Nexus.GetContractByName(name);
                        BigInteger gasCost;
                        var constructor = "Initialize";
                        if (contract.HasInternalMethod(constructor, out gasCost))
                        {
                            Runtime.CallContext(name, constructor, owner);
                        }
                    }
                    break;

                default:
                    success = false;
                    break;
            }

            var result = new VMObject();
            result.SetValue(success);
            Runtime.Stack.Push(result);
            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_CreateToken(RuntimeVM Runtime)
        {
            Runtime.Expect(Runtime.Stack.Count >= 9, "not enough arguments in stack");

            VMObject temp;

            var source = PopAddress(Runtime);

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.String, "expected string for symbol");
            var symbol = temp.AsString();

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.String, "expected string for name");
            var name = temp.AsString();

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.String, "expected string for platform");
            var platform = temp.AsString();

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.Bytes, "expected bytes for hash");
            var hash = Serialization.Unserialize<Hash>(temp.AsByteArray());

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.Number, "expected number for maxSupply");
            var maxSupply = temp.AsNumber();

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.Number, "expected number for decimals");
            var decimals = (int)temp.AsNumber();

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.Enum, "expected enum for flags");
            var flags = temp.AsEnum<TokenFlags>();

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.Bytes, "expected bytes for script");
            var script = temp.AsByteArray();

            Runtime.CreateToken(source, symbol, name, platform, hash, maxSupply, decimals, flags, script);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_CreateChain(RuntimeVM Runtime)
        {
            Runtime.Expect(Runtime.Stack.Count >= 3, "not enough arguments in stack");

            VMObject temp;

            var source = PopAddress(Runtime);

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.String, "expected string for name");
            var name = temp.AsString();

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.String, "expected string for parent");
            var parentName = temp.AsString();

            Runtime.CreateChain(source, name, parentName);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_CreatePlatform(RuntimeVM Runtime)
        {
            Runtime.Expect(Runtime.Stack.Count >= 3, "not enough arguments in stack");

            VMObject temp;

            var source = PopAddress(Runtime);
            var target = PopAddress(Runtime);

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.String, "expected string for symbol");
            var symbol = temp.AsString();

            Runtime.CreatePlatform(source, target, symbol);

            return ExecutionState.Running;
        }
    }
}
