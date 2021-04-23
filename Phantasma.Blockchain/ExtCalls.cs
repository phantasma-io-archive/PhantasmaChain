using System;
using System.IO;
using System.Linq;
using Phantasma.Cryptography;
using Phantasma.Core;
using Phantasma.VM;
using Phantasma.Core.Types;
using Phantasma.Numerics;
using Phantasma.Domain;
using Phantasma.Storage;
using Phantasma.Storage.Context;
using System.Collections.Generic;
using Phantasma.Blockchain.Contracts;

namespace Phantasma.Blockchain
{
    public static class ExtCalls
    {
        // naming scheme should be "namespace.methodName" for methods, and "type()" for constructors
        internal static void RegisterWithRuntime(RuntimeVM vm)
        {
            vm.RegisterMethod("Runtime.TransactionHash", Runtime_TransactionHash);
            vm.RegisterMethod("Runtime.Time", Runtime_Time);
            vm.RegisterMethod("Runtime.GasTarget", Runtime_GasTarget);
            vm.RegisterMethod("Runtime.Validator", Runtime_Validator);
            vm.RegisterMethod("Runtime.Context", Runtime_Context);
            vm.RegisterMethod("Runtime.GenerateUID", Runtime_GenerateUID);
            vm.RegisterMethod("Runtime.Random", Runtime_Random);            
            vm.RegisterMethod("Runtime.SetSeed", Runtime_SetSeed);
            vm.RegisterMethod("Runtime.IsWitness", Runtime_IsWitness);
            vm.RegisterMethod("Runtime.IsTrigger", Runtime_IsTrigger);
            vm.RegisterMethod("Runtime.IsMinter", Runtime_IsMinter);
            vm.RegisterMethod("Runtime.Log", Runtime_Log);
            vm.RegisterMethod("Runtime.Notify", Runtime_Notify);
            vm.RegisterMethod("Runtime.DeployContract", Runtime_DeployContract);
            vm.RegisterMethod("Runtime.UpgradeContract", Runtime_UpgradeContract);
            vm.RegisterMethod("Runtime.GetBalance", Runtime_GetBalance);
            vm.RegisterMethod("Runtime.TransferTokens", Runtime_TransferTokens);
            vm.RegisterMethod("Runtime.TransferBalance", Runtime_TransferBalance);
            vm.RegisterMethod("Runtime.MintTokens", Runtime_MintTokens);
            vm.RegisterMethod("Runtime.BurnTokens", Runtime_BurnTokens);
            vm.RegisterMethod("Runtime.SwapTokens", Runtime_SwapTokens);
            vm.RegisterMethod("Runtime.TransferToken", Runtime_TransferToken);
            vm.RegisterMethod("Runtime.MintToken", Runtime_MintToken);
            vm.RegisterMethod("Runtime.BurnToken", Runtime_BurnToken);
            vm.RegisterMethod("Runtime.InfuseToken", Runtime_InfuseToken);
            vm.RegisterMethod("Runtime.ReadTokenROM", Runtime_ReadTokenROM);
            vm.RegisterMethod("Runtime.ReadTokenRAM", Runtime_ReadTokenRAM);
            vm.RegisterMethod("Runtime.ReadToken", Runtime_ReadToken);
            vm.RegisterMethod("Runtime.WriteToken", Runtime_WriteToken);
            vm.RegisterMethod("Runtime.TokenExists", Runtime_TokenExists);
            vm.RegisterMethod("Runtime.GetTokenDecimals", Runtime_TokenGetDecimals);
            vm.RegisterMethod("Runtime.GetTokenFlags", Runtime_TokenGetFlags);
            vm.RegisterMethod("Runtime.AESDecrypt", Runtime_AESDecrypt);
            vm.RegisterMethod("Runtime.AESEncrypt", Runtime_AESEncrypt);

            vm.RegisterMethod("Nexus.Init", Nexus_Init);
            vm.RegisterMethod("Nexus.CreateToken", Nexus_CreateToken);
            vm.RegisterMethod("Nexus.CreateTokenSeries", Nexus_CreateTokenSeries);
            vm.RegisterMethod("Nexus.CreateChain", Nexus_CreateChain);
            vm.RegisterMethod("Nexus.CreatePlatform", Nexus_CreatePlatform);
            vm.RegisterMethod("Nexus.CreateOrganization", Nexus_CreateOrganization);
            vm.RegisterMethod("Nexus.SetPlatformTokenHash", Nexus_SetPlatformTokenHash);

            vm.RegisterMethod("Organization.AddMember", Organization_AddMember);

            vm.RegisterMethod("Task.Start", Task_Start);
            vm.RegisterMethod("Task.Stop", Task_Stop);
            vm.RegisterMethod("Task.Get", Task_Get);
            vm.RegisterMethod("Task.Current", Task_Current);

            vm.RegisterMethod("Data.Get", Data_Get);
            vm.RegisterMethod("Data.Set", Data_Set);
            vm.RegisterMethod("Data.Delete", Data_Delete);

            vm.RegisterMethod("Map.Has", Map_Has);
            vm.RegisterMethod("Map.Get", Map_Get);
            vm.RegisterMethod("Map.Set", Map_Set);
            vm.RegisterMethod("Map.Remove", Map_Remove);
            vm.RegisterMethod("Map.Count", Map_Count);
            vm.RegisterMethod("Map.Clear", Map_Clear);

            vm.RegisterMethod("List.Get", List_Get);
            vm.RegisterMethod("List.Add", List_Add);
            vm.RegisterMethod("List.Replace", List_Replace);
            //vm.RegisterMethod("List.Remove", List_Remove); TODO implement later, remove by value instead of index
            vm.RegisterMethod("List.RemoveAt", List_RemoveAt);
            vm.RegisterMethod("List.Count", List_Count);
            vm.RegisterMethod("List.Clear", List_Clear);

            vm.RegisterMethod("Account.Name", Account_Name);
            vm.RegisterMethod("Account.LastActivity", Account_Activity);
            vm.RegisterMethod("Account.Transactions", Account_Transactions);

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
            vm.RegisterMethod("Address()", Constructor_AddressV2);
            vm.RegisterMethod("Hash()", Constructor_Hash);
            vm.RegisterMethod("Timestamp()", Constructor_Timestamp);
        }

        private static ExecutionState Constructor_Object<IN, OUT>(VirtualMachine vm, Func<IN, OUT> loader)
        {
            var rawInput = vm.Stack.Pop();
            var inputType = VMObject.GetVMType(typeof(IN));
            var convertedInput = rawInput.AsType(inputType);

            try
            {
                OUT obj = loader((IN)convertedInput);
                var temp = new VMObject();
                temp.SetValue(obj);
                vm.Stack.Push(temp);
            }
            catch (Exception e)
            {
                throw new VMException(vm, e.Message);
            }

            return ExecutionState.Running;
        }

        public static ExecutionState Constructor_Address(VirtualMachine vm)
        {
            return Constructor_Object<byte[], Address>(vm, bytes =>
            {
                if (bytes == null || bytes.Length == 0)
                {
                    return Address.Null;
                }

                var addressData = bytes;
                if (addressData.Length == Address.LengthInBytes + 1)
                {
                    addressData = addressData.Skip(1).ToArray(); // HACK this is to work around sometimes addresses being passed around in Serializable format...
                }

                Throw.If(addressData.Length != Address.LengthInBytes, "cannot build Address from invalid data");
                return Address.FromBytes(addressData);
            });
        }

        public static ExecutionState Constructor_AddressV2(RuntimeVM vm)
        {
            var addr = vm.PopAddress();
            var temp = new VMObject();
            temp.SetValue(addr);
            vm.Stack.Push(temp);
            return ExecutionState.Running;
        }

        public static ExecutionState Constructor_Hash(VirtualMachine vm)
        {
            return Constructor_Object<byte[], Hash>(vm, bytes =>
            {
                Throw.If(bytes == null || bytes.Length != Hash.Length, "cannot build Hash from invalid data");
                return new Hash(bytes);
            });
        }

        public static ExecutionState Constructor_Timestamp(VirtualMachine vm)
        {
            return Constructor_Object<BigInteger, Timestamp>(vm, val =>
            {
                Throw.If(val < 0, "invalid number");
                return new Timestamp((uint)val);
            });
        }

        public static ExecutionState Constructor_ABI(VirtualMachine vm)
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
            vm.Log(text);
            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_Notify(RuntimeVM vm)
        {
            vm.Expect(vm.CurrentContext.Name != VirtualMachine.EntryContextName, "cannot notify in current context");

            var kind = vm.Stack.Pop().AsEnum<EventKind>();
            var address = vm.PopAddress();
            var obj = vm.Stack.Pop();

            var bytes = obj.Serialize();

            vm.Notify(kind, address, bytes);
            return ExecutionState.Running;
        }

        #region ORACLES
        // TODO proper exceptions
        private static ExecutionState Oracle_Read(RuntimeVM vm)
        {
            vm.ExpectStackSize(1);

            var url = vm.PopString("url");

            if (vm.Oracle == null)
            {
                return ExecutionState.Fault;
            }
            
            url = url.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(url))
            {
                return ExecutionState.Fault;
            }

            var result = vm.Oracle.Read<byte[]>(vm.Time,/*vm.Transaction.Hash, */url);
            vm.Stack.Push(VMObject.FromObject(result));

            return ExecutionState.Running;
        }

        private static ExecutionState Oracle_Price(RuntimeVM vm)
        {
            vm.ExpectStackSize(1);

            var symbol = vm.PopString("price");

            var price = vm.GetTokenPrice(symbol);

            vm.Stack.Push(VMObject.FromObject(price));

            return ExecutionState.Running;
        }

        private static ExecutionState Oracle_Quote(RuntimeVM vm)
        {
            vm.ExpectStackSize(3);

            var amount = vm.PopNumber("amount");
            var quoteSymbol = vm.PopString("quoteSymbol");
            var baseSymbol = vm.PopString("baseSymbol");

            var price = vm.GetTokenQuote(baseSymbol, quoteSymbol, amount);

            vm.Stack.Push(VMObject.FromObject(price));

            return ExecutionState.Running;
        }

        /*
        private static ExecutionState Oracle_Register(RuntimeVM vm)
        {
            ExpectStackSize(vm, 2);

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

        private static ExecutionState Runtime_Time(RuntimeVM vm)
        {
            var result = new VMObject();
            result.SetValue(vm.Time);
            vm.Stack.Push(result);
            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_TransactionHash(RuntimeVM vm)
        {
            try
            {
                var tx = vm.Transaction;
                Throw.IfNull(tx, nameof(tx));

                var result = new VMObject();
                result.SetValue(tx.Hash);
                vm.Stack.Push(result);
            }
            catch (Exception e)
            {
                throw new VMException(vm, e.Message);
            }

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_IsMinter(RuntimeVM vm)
        {
            try
            {
                var tx = vm.Transaction;
                Throw.IfNull(tx, nameof(tx));

                vm.ExpectStackSize(1);

                var address = vm.PopAddress();
                var symbol = vm.PopString("symbol");

                bool success = vm.IsMintingAddress(address, symbol);

                var result = new VMObject();
                result.SetValue(success);
                vm.Stack.Push(result);
            }
            catch (Exception e)
            {
                throw new VMException(vm, e.Message);
            }

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_GasTarget(RuntimeVM vm)
        {
            if (vm.GasTarget.IsNull)
            {
                new VMException(vm, "Gas target is now available yet");
            }

            var result = new VMObject();
            result.SetValue(vm.GasTarget);
            vm.Stack.Push(result);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_Validator(RuntimeVM vm)
        {
            var result = new VMObject();
            result.SetValue(vm.Validator);
            vm.Stack.Push(result);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_Context(RuntimeVM vm)
        {
            var result = new VMObject();
            result.SetValue(vm.CurrentContext.Name);
            vm.Stack.Push(result);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_GenerateUID(RuntimeVM vm)
        {
            try
            {
                var number = vm.GenerateUID();

                var result = new VMObject();
                result.SetValue(number);
                vm.Stack.Push(result);
            }
            catch (Exception e)
            {
                throw new VMException(vm, e.Message);
            }

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_Random(RuntimeVM vm)
        {
            try
            {
                var number = vm.GenerateRandomNumber();

                var result = new VMObject();
                result.SetValue(number);
                vm.Stack.Push(result);
            }
            catch (Exception e)
            {
                throw new VMException(vm, e.Message);
            }

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_SetSeed(RuntimeVM vm)
        {
            vm.ExpectStackSize(1);

            var seed = vm.PopNumber("seed");

            vm.SetRandomSeed(seed);
            return ExecutionState.Running;
        }


        private static ExecutionState Runtime_IsWitness(RuntimeVM vm)
        {
            try
            {
                var tx = vm.Transaction;
                Throw.IfNull(tx, nameof(tx));

                vm.ExpectStackSize(1);

                var address = vm.PopAddress();
                //var success = tx.IsSignedBy(address);
                // TODO check if this was just a bug or there was a real reason 
                var success = vm.IsWitness(address);

                var result = new VMObject();
                result.SetValue(success);
                vm.Stack.Push(result);
            }
            catch (Exception e)
            {
                throw new VMException(vm, e.Message);
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
            catch (Exception e)
            {
                throw new VMException(vm, e.Message);
            }

            return ExecutionState.Running;
        }

        #region DATA
        private static ExecutionState Data_Get(RuntimeVM vm)
        {
            vm.ExpectStackSize(3);

            var contractName = vm.PopString("contract");
            vm.Expect(vm.ContractDeployed(contractName), $"contract {contractName} is not deployed");

            var field = vm.PopString("field");
            var key = SmartContract.GetKeyForField(contractName, field, false);

            var type_obj = vm.Stack.Pop();
            var vmType = type_obj.AsEnum<VMType>();

            if (vmType == VMType.Object)
            {
                vmType = VMType.Bytes;
            }

            var value_bytes = vm.Storage.Get(key);
            var val = new VMObject();
            val.SetValue(value_bytes, vmType);
            vm.Stack.Push(val);

            return ExecutionState.Running;
        }

        private static ExecutionState Data_Set(RuntimeVM vm)
        {
            vm.ExpectStackSize(2);

            // for security reasons we don't accept the caller to specify a contract name
            var contractName = vm.CurrentContext.Name;
            vm.Expect(vm.ContractDeployed(contractName), $"contract {contractName} is not deployed");

            var field = vm.PopString("field");
            var key = SmartContract.GetKeyForField(contractName, field, false);

            var obj = vm.Stack.Pop();
            var valBytes = obj.AsByteArray();

            var contractAddress = SmartContract.GetAddressForName(contractName);
            vm.CallNativeContext(NativeContractKind.Storage, nameof(StorageContract.WriteData), contractAddress, key, valBytes);

            return ExecutionState.Running;
        }

        private static ExecutionState Data_Delete(RuntimeVM vm)
        {
            vm.ExpectStackSize(1);

            // for security reasons we don't accept the caller to specify a contract name
            var contractName = vm.CurrentContext.Name;
            vm.Expect(vm.ContractDeployed(contractName), $"contract {contractName} is not deployed");

            var field = vm.PopString("field");
            var key = SmartContract.GetKeyForField(contractName, field, false);

            var contractAddress = SmartContract.GetAddressForName(contractName);
            vm.CallNativeContext(NativeContractKind.Storage, nameof(StorageContract.DeleteData), contractAddress, key);

            return ExecutionState.Running;
        }
        #endregion

        #region MAP
        private static ExecutionState Map_Has(RuntimeVM vm)
        {
            vm.ExpectStackSize(3);

            var contractName = vm.PopString("contract");
            var field = vm.PopString("field");
            var mapKey = SmartContract.GetKeyForField(contractName, field, false);

            var entryKey = vm.Stack.Pop().AsByteArray();
            vm.Expect(entryKey.Length > 0, "invalid entry key");

            var type_obj = vm.Stack.Pop();
            var vmType = type_obj.AsEnum<VMType>();

            var map = new StorageMap(mapKey, vm.Storage);

            var keyExists = map.ContainsKey(entryKey);

            var val = new VMObject();
            val.SetValue(keyExists);
            vm.Stack.Push(val);

            return ExecutionState.Running;
        }


        private static ExecutionState Map_Get(RuntimeVM vm)
        {
            vm.ExpectStackSize(4);

            var contractName = vm.PopString("contract");
            var field = vm.PopString("field");
            var mapKey = SmartContract.GetKeyForField(contractName, field, false);

            var entryKey = vm.Stack.Pop().AsByteArray();
            vm.Expect(entryKey.Length > 0, "invalid entry key");

            var type_obj = vm.Stack.Pop();
            var vmType = type_obj.AsEnum<VMType>();

            var map = new StorageMap(mapKey, vm.Storage);

            var value_bytes = map.GetRaw(entryKey);

            var val = new VMObject();

            if (value_bytes == null)
            {
                val.SetDefaultValue(vmType);
            }
            else
            {
                val.SetValue(value_bytes, vmType);
            }
            vm.Stack.Push(val);

            return ExecutionState.Running;
        }

        private static ExecutionState Map_Set(RuntimeVM vm)
        {
            vm.ExpectStackSize(3);

            // for security reasons we don't accept the caller to specify a contract name
            var contractName = vm.CurrentContext.Name;
            vm.Expect(vm.ContractDeployed(contractName), $"contract {contractName} is not deployed");

            var field = vm.PopString("field");
            var mapKey = SmartContract.GetKeyForField(contractName, field, false);

            var entry_obj = vm.Stack.Pop();
            var entryKey = entry_obj.AsByteArray();
            vm.Expect(entryKey.Length > 0, "invalid entry key");

            var value = vm.Stack.Pop();

            var map = new StorageMap(mapKey, vm.Storage);

            var value_bytes = value.AsByteArray();

            map.SetRaw(entryKey, value_bytes);

            return ExecutionState.Running;
        }

        private static ExecutionState Map_Remove(RuntimeVM vm)
        {
            vm.ExpectStackSize(2);

            // for security reasons we don't accept the caller to specify a contract name
            var contractName = vm.CurrentContext.Name;

            var field = vm.PopString("field");
            var mapKey = SmartContract.GetKeyForField(contractName, field, false);

            var entry_obj = vm.Stack.Pop();
            var entryKey = entry_obj.AsByteArray();
            vm.Expect(entryKey.Length > 0, "invalid entry key");

            var map = new StorageMap(mapKey, vm.Storage);

            map.Remove<byte[]>(entryKey);

            return ExecutionState.Running;
        }

        private static ExecutionState Map_Clear(RuntimeVM vm)
        {
            vm.ExpectStackSize(1);

            // for security reasons we don't accept the caller to specify a contract name
            var contractName = vm.CurrentContext.Name;

            var field = vm.PopString("field");
            var mapKey = SmartContract.GetKeyForField(contractName, field, false);

            var map = new StorageMap(mapKey, vm.Storage);
            map.Clear();

            return ExecutionState.Running;
        }

        private static ExecutionState Map_Count(RuntimeVM vm)
        {
            vm.ExpectStackSize(2);

            var contractName = vm.PopString("contract");
            var field = vm.PopString("field");
            var mapKey = SmartContract.GetKeyForField(contractName, field, false);

            var map = new StorageMap(mapKey, vm.Storage);

            var count = map.Count();
            var val = VMObject.FromObject(count);
            vm.Stack.Push(val);

            return ExecutionState.Running;
        }
        #endregion

        #region LIST
        private static ExecutionState List_Get(RuntimeVM vm)
        {
            vm.ExpectStackSize(4);

            var contractName = vm.PopString("contract");
            var field = vm.PopString("field");
            var listKey = SmartContract.GetKeyForField(contractName, field, false);

            var index = vm.PopNumber("index");
            vm.Expect(index >= 0, "invalid index");

            var type_obj = vm.Stack.Pop();
            var vmType = type_obj.AsEnum<VMType>();

            var list = new StorageList(listKey, vm.Storage);

            var value_bytes = list.GetRaw(index);

            var val = new VMObject();

            if (value_bytes == null)
            {
                val.SetDefaultValue(vmType);
            }
            else
            {
                val.SetValue(value_bytes, vmType);
            }
            vm.Stack.Push(val);

            return ExecutionState.Running;
        }

        private static ExecutionState List_Add(RuntimeVM vm)
        {
            vm.ExpectStackSize(2);

            // for security reasons we don't accept the caller to specify a contract name
            var contractName = vm.CurrentContext.Name;
            vm.Expect(vm.ContractDeployed(contractName), $"contract {contractName} is not deployed");

            var field = vm.PopString("field");
            var listKey = SmartContract.GetKeyForField(contractName, field, false);

            var value = vm.Stack.Pop();

            var list = new StorageList(listKey, vm.Storage);

            var value_bytes = value.AsByteArray();

            list.AddRaw(value_bytes);

            return ExecutionState.Running;
        }

        private static ExecutionState List_Replace(RuntimeVM vm)
        {
            vm.ExpectStackSize(3);

            // for security reasons we don't accept the caller to specify a contract name
            var contractName = vm.CurrentContext.Name;
            vm.Expect(vm.ContractDeployed(contractName), $"contract {contractName} is not deployed");

            var field = vm.PopString("field");
            var listKey = SmartContract.GetKeyForField(contractName, field, false);

            var index = vm.PopNumber("index");
            vm.Expect(index >= 0, "invalid index");

            var value = vm.Stack.Pop();

            var list = new StorageList(listKey, vm.Storage);

            var value_bytes = value.AsByteArray();

            list.ReplaceRaw(index, value_bytes);

            return ExecutionState.Running;
        }

        private static ExecutionState List_RemoveAt(RuntimeVM vm)
        {
            vm.ExpectStackSize(2);

            // for security reasons we don't accept the caller to specify a contract name
            var contractName = vm.CurrentContext.Name;

            var field = vm.PopString("field");
            var listKey = SmartContract.GetKeyForField(contractName, field, false);

            var index = vm.PopNumber("index");
            vm.Expect(index >= 0, "invalid index");

            var list = new StorageList(listKey, vm.Storage);

            list.RemoveAt(index);

            return ExecutionState.Running;
        }

        private static ExecutionState List_Clear(RuntimeVM vm)
        {
            vm.ExpectStackSize(1);

            // for security reasons we don't accept the caller to specify a contract name
            var contractName = vm.CurrentContext.Name;

            var field = vm.PopString("field");
            var listKey = SmartContract.GetKeyForField(contractName, field, false);

            var list = new StorageList(listKey, vm.Storage);
            list.Clear();

            return ExecutionState.Running;
        }

        private static ExecutionState List_Count(RuntimeVM vm)
        {
            vm.ExpectStackSize(2);

            var contractName = vm.PopString("contract");
            var field = vm.PopString("field");
            var listKey = SmartContract.GetKeyForField(contractName, field, false);

            var list = new StorageList(listKey, vm.Storage);

            var count = list.Count();
            var val = VMObject.FromObject(count);
            vm.Stack.Push(val);

            return ExecutionState.Running;
        }
        #endregion

        private static ExecutionState Runtime_GetBalance(RuntimeVM vm)
        {
            vm.ExpectStackSize(2);

            var source = vm.PopAddress();
            var symbol = vm.PopString("symbol");

            var balance = vm.GetBalance(symbol, source);

            var result = new VMObject();
            result.SetValue(balance);
            vm.Stack.Push(result);
            
            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_TransferTokens(RuntimeVM vm)
        {
            vm.ExpectStackSize(4);

            var source = vm.PopAddress();
            var destination = vm.PopAddress();

            var symbol = vm.PopString("symbol");
            var amount = vm.PopNumber("amount");

            vm.TransferTokens(symbol, source, destination, amount);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_TransferBalance(RuntimeVM vm)
        {
            vm.ExpectStackSize(3);

            var source = vm.PopAddress();
            var destination = vm.PopAddress();

            var symbol = vm.PopString("symbol");

            var token = vm.GetToken(symbol);
            vm.Expect(token.IsFungible(), "must be fungible");

            var amount = vm.GetBalance(symbol, source);

            vm.TransferTokens(symbol, source, destination, amount);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_SwapTokens(RuntimeVM vm)
        {
            vm.ExpectStackSize(5);

            VMObject temp;

            temp = vm.Stack.Pop();
            vm.Expect(temp.Type == VMType.String, "expected string for target chain");
            var targetChain = temp.AsString();

            var source = vm.PopAddress();
            var destination = vm.PopAddress();

            temp = vm.Stack.Pop();
            vm.Expect(temp.Type == VMType.String, "expected string for symbol");
            var symbol = temp.AsString();

            var value = vm.PopNumber("amount");

            vm.SwapTokens(vm.Chain.Name, source, targetChain, destination, symbol, value);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_MintTokens(RuntimeVM vm)
        {
            vm.ExpectStackSize(4);

            var source = vm.PopAddress();
            var destination = vm.PopAddress();

            var symbol = vm.PopString("symbol");
            var amount = vm.PopNumber("amount");

            if (vm.Nexus.HasGenesis)
            {
                var isMinter = vm.IsMintingAddress(source, symbol);
                vm.Expect(isMinter, $"{source} is not a valid minting address for {symbol}");
            }

            vm.MintTokens(symbol, source, destination, amount);

            return ExecutionState.Running;
        }


        private static ExecutionState Runtime_BurnTokens(RuntimeVM vm)
        {
            vm.ExpectStackSize(3);

            var target = vm.PopAddress();
            var symbol = vm.PopString("symbol");
            var amount = vm.PopNumber("amount");

            vm.BurnTokens(symbol, target, amount);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_TransferToken(RuntimeVM vm)
        {
            vm.ExpectStackSize(4);
            
            VMObject temp;

            var source = vm.PopAddress();
            var destination = vm.PopAddress();

            temp = vm.Stack.Pop();
            vm.Expect(temp.Type == VMType.String, "expected string for symbol");
            var symbol = temp.AsString();

            var tokenID = vm.PopNumber("token ID");

            vm.TransferToken(symbol, source, destination, tokenID);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_MintToken(RuntimeVM vm)
        {
            vm.ExpectStackSize(4);

            var source = vm.PopAddress();
            var destination = vm.PopAddress();

            var symbol = vm.PopString("symbol");

            var rom = vm.PopBytes("rom");
            var ram = vm.PopBytes("ram");

            BigInteger seriesID;

            if (vm.ProtocolVersion >= 4)
            {
                seriesID = vm.PopNumber("series");
            }
            else
            {
                seriesID = 0;
            }

            var tokenID = vm.MintToken(symbol, source, destination, rom, ram, seriesID);

            var result = new VMObject();
            result.SetValue(tokenID);
            vm.Stack.Push(result);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_BurnToken(RuntimeVM vm)
        {
            vm.ExpectStackSize(3);

            var source = vm.PopAddress();
            var symbol = vm.PopString("symbol");
            var tokenID = vm.PopNumber("token ID");

            vm.BurnToken(symbol, source, tokenID);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_InfuseToken(RuntimeVM vm)
        {
            vm.ExpectStackSize(5);

            var source = vm.PopAddress();
            var targetSymbol = vm.PopString("target symbol");
            var tokenID = vm.PopNumber("token ID");
            var infuseSymbol = vm.PopString("infuse symbol");
            var value = vm.PopNumber("value");

            vm.InfuseToken(targetSymbol, source, tokenID, infuseSymbol, value);

            return ExecutionState.Running;
        }

        private static TokenContent Runtime_ReadTokenInternal(RuntimeVM vm)
        {
            vm.ExpectStackSize(2);

            var symbol = vm.PopString("symbol");
            var tokenID = vm.PopNumber("token ID");

            var result = vm.ReadToken(symbol, tokenID);

            vm.Expect(result.TokenID == tokenID, "retrived NFT content does not have proper tokenID");

            return result;
        }

        private static ExecutionState Runtime_ReadToken(RuntimeVM vm)
        {
            if (vm.ProtocolVersion < 4)
            {
                return Runtime_ReadTokenRAM(vm);
            }

            var content = Runtime_ReadTokenInternal(vm);

            var fieldList = vm.PopString("fields").Split(',');

            var result = new VMObject();

            var fields = new Dictionary<VMObject, VMObject>();
            foreach (var field in fieldList)
            {
                object obj;

                switch (field)
                {
                    case "chain": obj = content.CurrentChain; break;
                    case "owner": obj = content.CurrentOwner.Text; break;
                    case "creator": obj = content.Creator.Text; break;
                    case "ROM": obj = content.ROM; break;
                    case "RAM": obj = content.RAM; break;
                    case "tokenID": obj = content.TokenID; break;
                    case "seriesID": obj = content.SeriesID; break;
                    case "mintID": obj = content.MintID; break;

                    default:
                        throw new VMException(vm, "unknown nft field: " + field);
                }

                var key = VMObject.FromObject(field);
                fields[key] = VMObject.FromObject(obj);
            }
            
            result.SetValue(fields);
            vm.Stack.Push(result);

            return ExecutionState.Running;
        }
         
        private static ExecutionState Runtime_ReadTokenRAM(RuntimeVM Runtime)
        {
            var content = Runtime_ReadTokenInternal(Runtime);

            var result = new VMObject();
            result.SetValue(content.RAM, VMType.Bytes);
            Runtime.Stack.Push(result);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_ReadTokenROM(RuntimeVM Runtime)
        {
            var content = Runtime_ReadTokenInternal(Runtime);

            var result = new VMObject();
            result.SetValue(content.ROM, VMType.Bytes);
            Runtime.Stack.Push(result);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_WriteToken(RuntimeVM vm)
        {
            vm.ExpectStackSize(vm.ProtocolVersion >= 6 ? 4 : 3);

            Address from;

            if (vm.ProtocolVersion >= 6)
            {
                from = vm.PopAddress();
            }
            else
            {
                from = Address.Null;
            }

            var symbol = vm.PopString("symbol");
            var tokenID = vm.PopNumber("token ID");
            var ram = vm.PopBytes("ram");

            vm.WriteToken(from, symbol, tokenID, ram);

            return ExecutionState.Running;
        }

        private static ExecutionState Nexus_CreateTokenSeries(RuntimeVM vm)
        {
            vm.ExpectStackSize(5);

            var from = vm.PopAddress();
            var symbol = vm.PopString("symbol");
            var seriesID = vm.PopNumber("series ID");
            var maxSupply = vm.PopNumber("max supply");
            var mode = vm.PopEnum<TokenSeriesMode>("mode");
            var script = vm.PopBytes("script");
            var abiBytes = vm.PopBytes("abi bytes");

            var abi = ContractInterface.FromBytes(abiBytes);

            vm.CreateTokenSeries(symbol, from, seriesID, maxSupply, mode, script, abi);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_TokenExists(RuntimeVM vm)
        {
            vm.ExpectStackSize(1);

            var temp = vm.Stack.Pop();
            vm.Expect(temp.Type == VMType.String, "expected string for symbol");
            var symbol = temp.AsString();

            var success = vm.TokenExists(symbol);

            var result = new VMObject();
            result.SetValue(success);
            vm.Stack.Push(result);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_TokenGetDecimals(RuntimeVM vm)
        {
            vm.ExpectStackSize(1);

            var symbol = vm.PopString("symbol");

            if (!vm.TokenExists(symbol))
            {
                return ExecutionState.Fault;
            }

            var token = vm.GetToken(symbol);

            var result = new VMObject();
            result.SetValue(token.Decimals);
            vm.Stack.Push(result);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_TokenGetFlags(RuntimeVM vm)
        {
            vm.ExpectStackSize(1);

            var symbol = vm.PopString("symbol");

            if (!vm.TokenExists(symbol))
            {
                return ExecutionState.Fault;
            }

            var token = vm.GetToken(symbol);

            var result = new VMObject();
            result.SetValue(token.Flags);
            vm.Stack.Push(result);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_DeployContract(RuntimeVM vm)
        {
            var tx = vm.Transaction;
            Throw.IfNull(tx, nameof(tx));

            var pow = tx.Hash.GetDifficulty();
            vm.Expect(pow >= (int)ProofOfWork.Minimal, "expected proof of work");

            vm.ExpectStackSize(1);

            var from = vm.PopAddress();
            vm.Expect(from.IsUser, "address must be user");

            if (vm.Nexus.HasGenesis)
            {
                //Runtime.Expect(org != DomainSettings.ValidatorsOrganizationName, "cannot deploy contract via this organization");
                vm.Expect(vm.IsStakeMaster(from), "needs to be master");
            }

            vm.Expect(vm.IsWitness(from), "invalid witness");

            var contractName = vm.PopString("contractName");

            var contractAddress = SmartContract.GetAddressForName(contractName);
            var deployed = vm.Chain.IsContractDeployed(vm.Storage, contractAddress);

            // TODO 
            if (vm.ProtocolVersion >= 2)
            {
                vm.Expect(!deployed, $"{contractName} is already deployed");
            }
            else
            if (deployed)
            {
                return ExecutionState.Running;
            }

            byte[] script;
            ContractInterface abi;

            bool isNative = Nexus.IsNativeContract(contractName);
            if (isNative)
            {
                if (contractName == "validator" && vm.GenesisAddress == Address.Null)
                {
                    vm.Nexus.Initialize(from);
                }

                script = new byte[] { (byte)Opcode.RET };

                var contractInstance = vm.Nexus.GetNativeContractByAddress(contractAddress);
                abi = contractInstance.ABI;
            }
            else
            {
                if (ValidationUtils.IsValidTicker(contractName))
                {
                    throw new VMException(vm, "use createToken instead for this kind of contract");
                }
                else
                {
                    vm.Expect(ValidationUtils.IsValidIdentifier(contractName), "invalid contract name");
                }

                var isReserved = ValidationUtils.IsReservedIdentifier(contractName);

                if (isReserved && vm.IsWitness(vm.GenesisAddress))
                {
                    isReserved = false;
                }

                vm.Expect(!isReserved, $"name '{contractName}' reserved by system");

                script = vm.PopBytes("contractScript");

                var abiBytes = vm.PopBytes("contractABI");
                abi = ContractInterface.FromBytes(abiBytes);

                var fuelCost = vm.GetGovernanceValue(Nexus.FuelPerContractDeployTag);
                // governance value is in usd fiat, here convert from fiat to fuel amount
                fuelCost = vm.GetTokenQuote(DomainSettings.FiatTokenSymbol, DomainSettings.FuelTokenSymbol, fuelCost);

                // burn the "cost" tokens
                vm.BurnTokens(DomainSettings.FuelTokenSymbol, from, fuelCost);
            }

            // ABI validation
            ValidateABI(vm, contractName, abi, isNative);

            var success = vm.Chain.DeployContractScript(vm.Storage, from, contractName, contractAddress, script, abi);
            vm.Expect(success, $"deployment of {contractName} failed");

            var constructor = abi.FindMethod(SmartContract.ConstructorName);

            if (constructor != null)
            {
                vm.CallContext(contractName, constructor, from);
            }

            vm.Notify(EventKind.ContractDeploy, from, contractName);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_UpgradeContract(RuntimeVM vm)
        {
            var tx = vm.Transaction;
            Throw.IfNull(tx, nameof(tx));

            var pow = tx.Hash.GetDifficulty();
            vm.Expect(pow >= (int)ProofOfWork.Minimal, "expected proof of work");

            vm.ExpectStackSize(1);

            var from = vm.PopAddress();
            vm.Expect(from.IsUser, "address must be user");

            vm.Expect(vm.IsStakeMaster(from), "needs to be master");

            vm.Expect(vm.IsWitness(from), "invalid witness");

            var contractName = vm.PopString("contractName");

            var contractAddress = SmartContract.GetAddressForName(contractName);
            var deployed = vm.Chain.IsContractDeployed(vm.Storage, contractAddress);

            vm.Expect(deployed, $"{contractName} does not exist");

            byte[] script;
            ContractInterface abi;

            bool isNative = Nexus.IsNativeContract(contractName);
            vm.Expect(!isNative, "cannot upgrade native contract");

            bool isToken = ValidationUtils.IsValidTicker(contractName);

            script = vm.PopBytes("contractScript");

            var abiBytes = vm.PopBytes("contractABI");
            abi = ContractInterface.FromBytes(abiBytes);

            var fuelCost = vm.GetGovernanceValue(Nexus.FuelPerContractDeployTag);
            // governance value is in usd fiat, here convert from fiat to fuel amount
            fuelCost = vm.GetTokenQuote(DomainSettings.FiatTokenSymbol, DomainSettings.FuelTokenSymbol, fuelCost);

            // burn the "cost" tokens
            vm.BurnTokens(DomainSettings.FuelTokenSymbol, from, fuelCost);

            // ABI validation
            ValidateABI(vm, contractName, abi, isNative);

            SmartContract oldContract;
            if (isToken)
            {
                oldContract = vm.Nexus.GetTokenContract(vm.Storage, contractName);
            }
            else
            {
                oldContract = vm.Chain.GetContractByName(vm.Storage, contractName);
            }

            vm.Expect(oldContract != null, "could not fetch previous contract");
            vm.Expect(abi.Implements(oldContract.ABI), "new abi does not implement all methods of previous abi");
            vm.ValidateTriggerGuard($"{contractName}.{AccountTrigger.OnUpgrade.ToString()}");

            vm.Expect(vm.InvokeTrigger(false, script, contractName, abi, AccountTrigger.OnUpgrade.ToString(), from) == TriggerResult.Success, "OnUpgrade trigger failed");

            if (isToken)
            {
                vm.Nexus.UpgradeTokenContract(vm.RootStorage, contractName, script, abi);
            }
            else
            {
                vm.Chain.UpgradeContract(vm.Storage, contractName, script, abi);
            }

            vm.Notify(EventKind.ContractUpgrade, from, contractName);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_AESDecrypt(RuntimeVM vm)
        {
            vm.ExpectStackSize(2);

            var data = vm.PopBytes("data");
            var key = vm.PopBytes("key");

            var decryptedData = CryptoExtensions.AESGCMDecrypt(data, key);

            var result = new VMObject();
            result.SetValue(decryptedData);
            vm.Stack.Push(result);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_AESEncrypt(RuntimeVM vm)
        {
            vm.ExpectStackSize(2);

            var data = vm.PopBytes("data");
            var key = vm.PopBytes("key");

            var encryptedData = CryptoExtensions.AESGCMEncrypt(data, key);

            var result = new VMObject();
            result.SetValue(encryptedData);
            vm.Stack.Push(result);

            return ExecutionState.Running;
        }

        private static ExecutionState Nexus_Init(RuntimeVM vm)
        {
            vm.Expect(vm.Chain == null || vm.Chain.Height == 0, "nexus already initialized");

            vm.ExpectStackSize(1);

            var owner = vm.PopAddress();

            vm.Nexus.Initialize(owner);

            return ExecutionState.Running;
        }
        
        private static ExecutionState Nexus_CreateToken(RuntimeVM vm)
        {
            vm.ExpectStackSize(7);

            var owner = vm.PopAddress();

            var symbol = vm.PopString("symbol");
            var name = vm.PopString("name");
            var maxSupply = vm.PopNumber("maxSupply");
            var decimals = (int)vm.PopNumber("decimals");
            var flags = vm.PopEnum<TokenFlags>("flags");
            var script = vm.PopBytes("script");

            ContractInterface abi;

            if (vm.ProtocolVersion >= 4)
            {
                var abiBytes = vm.PopBytes("abi bytes");
                abi = ContractInterface.FromBytes(abiBytes);
            }
            else
            {
                abi = new ContractInterface();
            }

            vm.CreateToken(owner, symbol, name, maxSupply, decimals, flags, script, abi);

            return ExecutionState.Running;
        }

        private static ExecutionState Nexus_CreateChain(RuntimeVM vm)
        {
            vm.ExpectStackSize(4);

            var source = vm.PopAddress();
            var org = vm.PopString("organization");
            var name = vm.PopString("name");
            var parentName = vm.PopString("parent");

            vm.CreateChain(source, org, name, parentName);

            return ExecutionState.Running;
        }

        private static ExecutionState Nexus_CreatePlatform(RuntimeVM vm)
        {
            vm.ExpectStackSize(5);

            var source = vm.PopAddress();
            var name = vm.PopString("name");
            var externalAddress = vm.PopString("external address");
            var interopAddress = vm.PopAddress();
            var symbol = vm.PopString("symbol");

            var target = vm.CreatePlatform(source, name, externalAddress, interopAddress, symbol);

            var result = new VMObject();
            result.SetValue(target);
            vm.Stack.Push(result);

            return ExecutionState.Running;
        }

        private static ExecutionState Nexus_SetPlatformTokenHash(RuntimeVM vm)
        {
            vm.ExpectStackSize(3);

            var symbol = vm.PopString("symbol");
            var platform = vm.PopString("platform");

            var bytes = vm.PopBytes("hash");
            var hash = new Hash(bytes.Skip(1).ToArray());

            vm.SetPlatformTokenHash(symbol, platform, hash);

            return ExecutionState.Running;
        }

        private static ExecutionState Nexus_CreateOrganization(RuntimeVM vm)
        {
            vm.ExpectStackSize(4);

            var source = vm.PopAddress();
            var ID = vm.PopString("id");
            var name = vm.PopString("name");
            var script = vm.PopBytes("script");

            vm.CreateOrganization(source, ID, name, script);

            return ExecutionState.Running;
        }

        private static ExecutionState Organization_AddMember(RuntimeVM vm)
        {
            vm.ExpectStackSize(3);

            var source = vm.PopAddress();
            var name = vm.PopString("name");
            var target = vm.PopAddress();

            vm.AddMember(name, source, target);

            return ExecutionState.Running;
        }

        private static void ValidateABI(RuntimeVM vm, string contractName, ContractInterface abi, bool isNative)
        {
            var offsets = new HashSet<int>();
            var names = new HashSet<string>();
            foreach (var method in abi.Methods)
            {
                vm.Expect(ValidationUtils.IsValidMethod(method.name, method.returnType), "invalid method: " + method.name);
                var normalizedName = method.name.ToLower();
                vm.Expect(!names.Contains(normalizedName), $"duplicated method name in {contractName}: {normalizedName}");

                names.Add(normalizedName);

                if (!isNative)
                {
                    vm.Expect(method.offset >= 0, $"invalid offset in {contractName} contract abi for method {method.name}");
                    vm.Expect(!offsets.Contains(method.offset), $"duplicated offset in {contractName} contract abi for method {method.name}");
                    offsets.Add(method.offset);
                }
            }
        }

        #region TASKS
        private static ExecutionState Task_Current(RuntimeVM vm)
        {
            var result = new VMObject();
            result.SetValue(vm.CurrentTask);
            vm.Stack.Push(result);
            return ExecutionState.Running;
        }

        private static ExecutionState Task_Get(RuntimeVM vm)
        {
            vm.ExpectStackSize(1);

            var taskID = vm.PopNumber("task");
            var task = (ChainTask)vm.GetTask(taskID);

            var result = new VMObject();
            result.SetValue(task);
            vm.Stack.Push(result);
            return ExecutionState.Running;
        }

        private static ExecutionState Task_Stop(RuntimeVM vm)
        {
            vm.ExpectStackSize(1);

            vm.Expect(vm.CurrentTask == null, "cannot stop task from within a task");

            var taskID = vm.PopNumber("task");
            var task = vm.GetTask(taskID);
            vm.Expect(task != null, "task not found");

            vm.StopTask(task);

            return ExecutionState.Running;
        }

        private static ExecutionState Task_Start(RuntimeVM vm)
        {
            vm.ExpectStackSize(1);

            var contractName = vm.PopString("contract");
            var methodBytes = vm.PopBytes("method bytes");
            var from = vm.PopAddress();
            var frequency = (uint)vm.PopNumber("frequency");
            var delay = (uint)vm.PopNumber("delay");
            var mode = vm.PopEnum<TaskFrequencyMode>("mode");
            var gasLimit = vm.PopNumber("gas limit");

            var method = ContractMethod.FromBytes(methodBytes);

            var task = vm.StartTask(from, contractName, method, frequency, delay, mode, gasLimit);

            var result = new VMObject();
            result.SetValue(task.ID);
            vm.Stack.Push(result);

            return ExecutionState.Running;
        }

        #endregion

        #region ACCOUNT 
        private static ExecutionState Account_Name(RuntimeVM vm)
        {
            vm.ExpectStackSize(1);

            var address = vm.PopAddress();

            var result = vm.GetAddressName(address);
            vm.Stack.Push(VMObject.FromObject(result));

            return ExecutionState.Running;
        }

        private static ExecutionState Account_Activity(RuntimeVM vm)
        {
            vm.ExpectStackSize(1);

            var address = vm.PopAddress();

            var result = vm.Chain.GetLastActivityOfAddress(address);
            vm.Stack.Push(VMObject.FromObject(result));

            return ExecutionState.Running;
        }

        private static ExecutionState Account_Transactions(RuntimeVM vm)
        {
            vm.ExpectStackSize(1);

            var address = vm.PopAddress();

            var result = vm.Chain.GetTransactionHashesForAddress(address);

            var dict = new Dictionary<VMObject, VMObject>();
            for (int i=0; i< result.Length; i++)
            {
                var hash = result[i];
                var temp = new VMObject();
                temp.SetValue(hash);
                dict[VMObject.FromObject(i)] = temp;
            }

            var obj = new VMObject();
            obj.SetValue(dict);
            vm.Stack.Push(obj);

            return ExecutionState.Running;
        }

        #endregion

    }
}
