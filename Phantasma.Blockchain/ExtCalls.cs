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
using System.Diagnostics;
using System.Linq;
using Phantasma.Core.Utils;
using Phantasma.Storage.Context;

namespace Phantasma.Blockchain
{
    public static class ExtCalls
    {
        // naming scheme should be "namespace.methodName" for methods, and "type()" for constructors
        internal static void RegisterWithRuntime(RuntimeVM vm)
        {
            vm.RegisterMethod("Runtime.TransactionHash", Runtime_TransactionHash);
            vm.RegisterMethod("Runtime.Time", Runtime_Time);
            vm.RegisterMethod("Runtime.IsWitness", Runtime_IsWitness);
            vm.RegisterMethod("Runtime.IsTrigger", Runtime_IsTrigger);
            vm.RegisterMethod("Runtime.Log", Runtime_Log);
            vm.RegisterMethod("Runtime.Notify", Runtime_Notify);
            vm.RegisterMethod("Runtime.DeployContract", Runtime_DeployContract);
            vm.RegisterMethod("Runtime.GetBalance", Runtime_GetBalance);
            vm.RegisterMethod("Runtime.TransferTokens", Runtime_TransferTokens);
            vm.RegisterMethod("Runtime.TransferBalance", Runtime_TransferBalance);
            vm.RegisterMethod("Runtime.MintTokens", Runtime_MintTokens);
            vm.RegisterMethod("Runtime.BurnTokens", Runtime_BurnTokens);
            vm.RegisterMethod("Runtime.SwapTokens", Runtime_SwapTokens);
            vm.RegisterMethod("Runtime.TransferToken", Runtime_TransferToken);
            vm.RegisterMethod("Runtime.MintToken", Runtime_MintToken);
            vm.RegisterMethod("Runtime.BurnToken", Runtime_BurnToken);
            vm.RegisterMethod("Runtime.ReadTokenROM", Runtime_ReadTokenROM);
            vm.RegisterMethod("Runtime.ReadToken", Runtime_ReadToken);
            vm.RegisterMethod("Runtime.WriteToken", Runtime_WriteToken);

            vm.RegisterMethod("Nexus.Init", Runtime_NexusInit);
            vm.RegisterMethod("Nexus.CreateToken", Runtime_CreateToken);
            vm.RegisterMethod("Nexus.CreateChain", Runtime_CreateChain);
            vm.RegisterMethod("Nexus.CreatePlatform", Runtime_CreatePlatform);
            vm.RegisterMethod("Nexus.CreateOrganization", Runtime_CreateOrganization);
            vm.RegisterMethod("Nexus.SetTokenPlatformHash", Runtime_SetTokenPlatformHash);

            vm.RegisterMethod("Organization.AddMember", Organization_AddMember);

            vm.RegisterMethod("Data.Field", Data_Field);
            vm.RegisterMethod("Data.Get", Data_Get);
            vm.RegisterMethod("Data.Set", Data_Set);
            vm.RegisterMethod("Data.Delete", Data_Delete);

            vm.RegisterMethod("Map.Get", Map_Get);
            vm.RegisterMethod("Map.Set", Map_Set);
            vm.RegisterMethod("Map.Remove", Map_Remove);
            vm.RegisterMethod("Map.Count", Map_Count);
            vm.RegisterMethod("Map.Clear", Map_Clear);

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
            catch (Exception e)
            {
                throw new VMException(vm, e.Message);
            }

            return ExecutionState.Running;
        }

        private static ExecutionState Constructor_Address(RuntimeVM vm)
        {
            return Constructor_Object<byte[], Address>(vm, bytes =>
            {
                if (bytes.Length == 0)
                {
                    return Address.Null;
                }

                Throw.If(bytes == null || bytes.Length != Address.LengthInBytes, "cannot build Address from invalid data");
                return Address.FromBytes(bytes);
            });
        }

        private static ExecutionState Constructor_Hash(RuntimeVM vm)
        {
            return Constructor_Object<byte[], Hash>(vm, bytes =>
            {
                Throw.If(bytes == null || bytes.Length != Hash.Length, "cannot build Hash from invalid data");
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

        private static ExecutionState Runtime_Notify(RuntimeVM vm)
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
            ExpectStackSize(vm, 1);

            var url = PopString(vm, "url");

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
            ExpectStackSize(vm, 1);

            var symbol = PopString(vm, "price");

            var price = vm.GetTokenPrice(symbol);

            vm.Stack.Push(VMObject.FromObject(price));

            return ExecutionState.Running;
        }

        private static ExecutionState Oracle_Quote(RuntimeVM vm)
        {
            ExpectStackSize(vm, 3);

            var amount = PopNumber(vm, "amount");
            var quoteSymbol = PopString(vm, "quoteSymbol");
            var baseSymbol = PopString(vm, "baseSymbol");

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


        private static ExecutionState Runtime_IsWitness(RuntimeVM vm)
        {
            try
            {
                var tx = vm.Transaction;
                Throw.IfNull(tx, nameof(tx));

                ExpectStackSize(vm, 1);

                var address = PopAddress(vm);
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
        // returns the key for a field from a contract
        private static ExecutionState Data_Field(RuntimeVM runtime)
        {
            var contract = PopString(runtime, "contract");
            var field = PopString(runtime, "contract");
            var key_bytes = SmartContract.GetKeyForField(contract, field, false);

            var val = new VMObject();
            val.SetValue(key_bytes, VMType.Bytes);
            runtime.Stack.Push(val);

            return ExecutionState.Running;
        }

        private static ExecutionState Data_Get(RuntimeVM vm)
        {
            var key = PopBytes(vm, "key");
            vm.Expect(key.Length > 0, "invalid key");

            var type_obj = vm.Stack.Pop();
            var vmType = type_obj.AsEnum<VMType>();

            var value_bytes = vm.Storage.Get(key);
            var val = new VMObject();
            val.SetValue(value_bytes, vmType);
            vm.Stack.Push(val);

            return ExecutionState.Running;
        }

        private static ExecutionState Data_Set(RuntimeVM vm)
        {
            ExpectStackSize(vm, 2);

            var key = PopBytes(vm, "key");
            vm.Expect(key.Length > 0, "invalid key");

            var val = vm.Stack.Pop().AsByteArray();

            vm.Expect(key.Length > 0, "invalid key");

            var firstChar = (char)key[0];
            vm.Expect(firstChar != '.', "permission denied"); // NOTE link correct PEPE here

            vm.Storage.Put(key, val);

            return ExecutionState.Running;
        }

        private static ExecutionState Data_Delete(RuntimeVM vm)
        {
            var key = PopBytes(vm, "key");
            vm.Expect(key.Length > 0, "invalid key");

            vm.Expect(key.Length > 0, "invalid key");

            var firstChar = (char)key[0];
            vm.Expect(firstChar != '.', "permission denied"); // NOTE link correct PEPE here

            vm.Storage.Delete(key);

            return ExecutionState.Running;
        }
        #endregion

        #region MAP
        private static ExecutionState Map_Get(RuntimeVM vm)
        {
            ExpectStackSize(vm, 3);

            var mapKey = PopBytes(vm, "mapKey");
            vm.Expect(mapKey.Length > 0, "invalid map key");

            var entry_obj = vm.Stack.Pop();
            var entryKey = entry_obj.AsByteArray();
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
            ExpectStackSize(vm, 3);

            var mapKey = PopBytes(vm, "mapKey");
            vm.Expect(mapKey.Length > 0, "invalid map key");

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
            ExpectStackSize(vm, 2);

            var mapKey = PopBytes(vm, "mapKey");
            vm.Expect(mapKey.Length > 0, "invalid map key");

            var entry_obj = vm.Stack.Pop();
            var entryKey = entry_obj.AsByteArray();
            vm.Expect(entryKey.Length > 0, "invalid entry key");

            var map = new StorageMap(mapKey, vm.Storage);

            map.Remove<byte[]>(entryKey);

            return ExecutionState.Running;
        }

        private static ExecutionState Map_Clear(RuntimeVM vm)
        {
            ExpectStackSize(vm, 1);

            var mapKey = PopBytes(vm, "mapKey");
            vm.Expect(mapKey.Length > 0, "invalid map key");

            var map = new StorageMap(mapKey, vm.Storage);
            map.Clear();

            return ExecutionState.Running;
        }

        private static ExecutionState Map_Count(RuntimeVM vm)
        {
            ExpectStackSize(vm, 1);

            var mapKey = PopBytes(vm, "mapKey");
            vm.Expect(mapKey.Length > 0, "invalid map key");

            var map = new StorageMap(mapKey, vm.Storage);

            var count = map.Count();
            var val = VMObject.FromObject(count);
            vm.Stack.Push(val);

            return ExecutionState.Running;
        }
        #endregion


        private static void ExpectStackSize(RuntimeVM vm, int minSize)
        {
            if (vm.Stack.Count < minSize)
            {
                var callingFrame = new StackFrame(1);
                var method = callingFrame.GetMethod();

                throw new VMException(vm, $"not enough arguments in stack, expected {minSize} @ {method}");
            }
        }

        private static Address PopAddress(RuntimeVM vm)
        {
            var temp = vm.Stack.Pop();
            if (temp.Type == VMType.String)
            {
                var text = temp.AsString();
                //TODO_FIX_TX
                //if (Address.IsValidAddress(text) && vm.Chain.Height > 65932)
                if (Address.IsValidAddress(text) && vm.ProtocolVersion >= 2)
                {
                    return Address.FromText(text);
                }
                return vm.Nexus.LookUpName(vm.Storage, text);
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

        private static BigInteger PopNumber(RuntimeVM vm, string ArgumentName)
        {
            var temp = vm.Stack.Pop();

            if (temp.Type == VMType.String)
            {
                vm.Expect(BigInteger.IsParsable(temp.AsString()), $"expected number for {ArgumentName}");
            }
            else
            {
                vm.Expect(temp.Type == VMType.Number, $"expected number for {ArgumentName}");
            }

            return temp.AsNumber();
        }

        private static string PopString(RuntimeVM vm, string ArgumentName)
        {
            var temp = vm.Stack.Pop();

            vm.Expect(temp.Type == VMType.String, $"expected string for {ArgumentName}");

            return temp.AsString();
        }

        private static byte[] PopBytes(RuntimeVM vm, string ArgumentName)
        {
            var temp = vm.Stack.Pop();

            vm.Expect(temp.Type == VMType.Bytes, $"expected bytes for {ArgumentName}");

            return temp.AsByteArray();
        }

        private static ExecutionState Runtime_GetBalance(RuntimeVM vm)
        {
            ExpectStackSize(vm, 2);

            var source = PopAddress(vm);
            var symbol = PopString(vm, "symbol");

            var balance = vm.GetBalance(symbol, source);

            var result = new VMObject();
            result.SetValue(balance);
            vm.Stack.Push(result);
            
            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_TransferTokens(RuntimeVM vm)
        {
            ExpectStackSize(vm, 4);

            var source = PopAddress(vm);
            var destination = PopAddress(vm);

            var symbol = PopString(vm, "symbol");
            var amount = PopNumber(vm, "amount");

            vm.TransferTokens(symbol, source, destination, amount);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_TransferBalance(RuntimeVM vm)
        {
            ExpectStackSize(vm, 3);

            var source = PopAddress(vm);
            var destination = PopAddress(vm);

            var symbol = PopString(vm, "symbol");

            var token = vm.GetToken(symbol);
            vm.Expect(token.IsFungible(), "must be fungible");

            var amount = vm.GetBalance(symbol, source);

            vm.TransferTokens(symbol, source, destination, amount);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_SwapTokens(RuntimeVM Runtime)
        {
            ExpectStackSize(Runtime, 5);

            VMObject temp;

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.String, "expected string for target chain");
            var targetChain = temp.AsString();

            var source = PopAddress(Runtime);
            var destination = PopAddress(Runtime);

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.String, "expected string for symbol");
            var symbol = temp.AsString();

            var value = PopNumber(Runtime, "amount");

            var token = Runtime.GetToken(symbol);
            if (token.IsFungible())
            {
                Runtime.SwapTokens(Runtime.Chain.Name, source, targetChain, destination, symbol, value, null, null);
            }
            else
            {
                var nft = Runtime.ReadToken(symbol, value);
                Runtime.SwapTokens(Runtime.Chain.Name, source, targetChain, destination, symbol, value, nft.ROM, nft.ROM);
            }

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_MintTokens(RuntimeVM vm)
        {
            ExpectStackSize(vm, 4);

            var source = PopAddress(vm);
            var destination = PopAddress(vm);

            var symbol = PopString(vm, "symbol");
            var amount = PopNumber(vm, "amount");

            if (vm.Nexus.HasGenesis)
            {
                vm.Expect(symbol != DomainSettings.FuelTokenSymbol && symbol != DomainSettings.StakingTokenSymbol, "cannot mint system tokens after genesis");
            }

            vm.MintTokens(symbol, source, destination, amount);

            return ExecutionState.Running;
        }


        private static ExecutionState Runtime_BurnTokens(RuntimeVM vm)
        {
            ExpectStackSize(vm, 3);

            VMObject temp;

            var source = PopAddress(vm);

            var symbol = PopString(vm, "symbol");
            var amount = PopNumber(vm, "amount");

            if (vm.Nexus.HasGenesis)
            {
                vm.Expect(symbol != DomainSettings.FuelTokenSymbol && symbol != DomainSettings.StakingTokenSymbol, "cannot mint system tokens after genesis");
            }

            vm.BurnTokens(symbol, source, amount);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_TransferToken(RuntimeVM Runtime)
        {
            ExpectStackSize(Runtime, 4);
            
            VMObject temp;

            var source = PopAddress(Runtime);
            var destination = PopAddress(Runtime);

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.String, "expected string for symbol");
            var symbol = temp.AsString();

            var tokenID = PopNumber(Runtime, "token ID");

            Runtime.TransferToken(symbol, source, destination, tokenID);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_MintToken(RuntimeVM Runtime)
        {
            ExpectStackSize(Runtime, 4);

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
            ExpectStackSize(Runtime, 3);

            VMObject temp;

            var source = PopAddress(Runtime);

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.String, "expected string for symbol");
            var symbol = temp.AsString();

            var tokenID = PopNumber(Runtime, "token ID");

            Runtime.BurnToken(symbol, source, tokenID);

            return ExecutionState.Running;
        }

        private static TokenContent Runtime_ReadTokenInternal(RuntimeVM Runtime)
        {
            ExpectStackSize(Runtime, 2);

            VMObject temp;

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.String, "expected string for symbol");
            var symbol = temp.AsString();

            var tokenID = PopNumber(Runtime, "token ID");

            return Runtime.ReadToken(symbol, tokenID);
        }

        private static ExecutionState Runtime_ReadToken(RuntimeVM Runtime)
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

        private static ExecutionState Runtime_WriteToken(RuntimeVM Runtime)
        {
            ExpectStackSize(Runtime, 3);

            VMObject temp;

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.String, "expected string for symbol");
            var symbol = temp.AsString();

            var tokenID = PopNumber(Runtime, "token ID");

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.Bytes, "expected bytes for ram");
            var ram = temp.AsByteArray();

            Runtime.WriteToken(symbol, tokenID, ram);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_DeployContract(RuntimeVM Runtime)
        {
            var tx = Runtime.Transaction;
            Throw.IfNull(tx, nameof(tx));

            ExpectStackSize(Runtime, 1);

            var owner = PopAddress(Runtime);
            Runtime.Expect(owner.IsUser, "address must be user");

            if (Runtime.Nexus.HasGenesis)
            {
                //Runtime.Expect(org != DomainSettings.ValidatorsOrganizationName, "cannot deploy contract via this organization");
                Runtime.Expect(Runtime.IsStakeMaster(owner), "needs to be master");
            }

            Runtime.Expect(Runtime.IsWitness(owner), "invalid witness");

            var contractName = PopString(Runtime, "contractName");

            var contractAddress = SmartContract.GetAddressForName(contractName);
            var deployed = Runtime.Chain.IsContractDeployed(Runtime.Storage, contractAddress);

            Runtime.Expect(!deployed, $"{contractName} is already deployed");

            byte[] script;
            ContractInterface abi;

            bool hasConstructor;
            var constructorName = "Initialize";

            if (Nexus.IsNativeContract(contractName))
            {
                if (contractName == "validator" && Runtime.GenesisAddress == Address.Null)
                {
                    Runtime.Nexus.Initialize(owner);
                }

                script = new byte[] { (byte)Opcode.RET };

                var contractInstance = Runtime.Nexus.GetNativeContractByAddress(contractAddress);
                abi = contractInstance.ABI;
                hasConstructor = contractInstance.HasInternalMethod(constructorName);
            }
            else
            {
                script = PopBytes(Runtime, "contractScript");

                var abiBytes = PopBytes(Runtime, "contractABI");
                abi = ContractInterface.Unserialize(abiBytes);

                var temp = System.Text.Encoding.UTF8.GetBytes(constructorName);
                var constructorIndex = script.SearchBytes(temp);
                hasConstructor = constructorIndex >= 0;
            }


            var success = Runtime.Chain.DeployContractScript(Runtime.Storage, contractName, contractAddress, script, abi);
            Runtime.Expect(success, $"deployment of {contractName} failed");

            if (hasConstructor)
            {
                Runtime.CallContext(contractName, constructorName, owner);
            }

            Runtime.Notify(EventKind.ContractDeploy, owner, contractName);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_NexusInit(RuntimeVM Runtime)
        {
            Runtime.Expect(Runtime.Chain == null || Runtime.Chain.Height == 0, "nexus already initialized");

            ExpectStackSize(Runtime, 1);

            var owner = PopAddress(Runtime);

            Runtime.Nexus.Initialize(owner);

            return ExecutionState.Running;
        }
        
        private static ExecutionState Runtime_CreateToken(RuntimeVM Runtime)
        {
            ExpectStackSize(Runtime, 7);

            VMObject temp;

            var source = PopAddress(Runtime);

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.String, "expected string for symbol");
            var symbol = temp.AsString();

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.String, "expected string for name");
            var name = temp.AsString();

            /*
            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.String, "expected string for platform");
            var platform = temp.AsString();

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.Bytes, "expected bytes for hash");
            var hash = Serialization.Unserialize<Hash>(temp.AsByteArray());*/

            var maxSupply = PopNumber(Runtime, "maxSupply");

            var decimals = (int)PopNumber(Runtime, "decimals");

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.Enum, "expected enum for flags");
            var flags = temp.AsEnum<TokenFlags>();

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.Bytes, "expected bytes for script");
            var script = temp.AsByteArray();

            Runtime.CreateToken(source, symbol, name, maxSupply, decimals, flags, script);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_CreateChain(RuntimeVM Runtime)
        {
            ExpectStackSize(Runtime, 3);

            VMObject temp;

            var source = PopAddress(Runtime);

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.String, "expected string for organization");
            var org = temp.AsString();

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.String, "expected string for name");
            var name = temp.AsString();

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.String, "expected string for parent");
            var parentName = temp.AsString();

            Runtime.CreateChain(source, org, name, parentName);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_CreatePlatform(RuntimeVM Runtime)
        {
            ExpectStackSize(Runtime, 3);

            VMObject temp;

            var source = PopAddress(Runtime);

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.String, "expected string for name");
            var name = temp.AsString();

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.String, "expected string for pubaddress");
            var externalAddress = temp.AsString();

            var interopAddress = PopAddress(Runtime);

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.String, "expected string for symbol");
            var symbol = temp.AsString();

            var target = Runtime.CreatePlatform(source, name, externalAddress, interopAddress, symbol);

            var result = new VMObject();
            result.SetValue(target);
            Runtime.Stack.Push(result);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_SetTokenPlatformHash(RuntimeVM Runtime)
        {
            ExpectStackSize(Runtime, 3);

            VMObject temp;

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.String, "expected string for symbol");
            var symbol = temp.AsString();

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.String, "expected string for platform");
            var platform = temp.AsString();

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.Bytes, "expected bytes for hash");
            var hash = new Hash(temp.AsByteArray().Skip(1).ToArray());

            Runtime.SetTokenPlatformHash(symbol, platform, hash);

            return ExecutionState.Running;
        }

        private static ExecutionState Runtime_CreateOrganization(RuntimeVM Runtime)
        {
            ExpectStackSize(Runtime, 4);

            VMObject temp;

            var source = PopAddress(Runtime);

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.String, "expected string for ID");
            var ID = temp.AsString();

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.String, "expected string for name");
            var name = temp.AsString();

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.Bytes, "expected bytes for script");
            var script = temp.AsByteArray();

            Runtime.CreateOrganization(source, ID, name, script);

            return ExecutionState.Running;
        }

        private static ExecutionState Organization_AddMember(RuntimeVM Runtime)
        {
            ExpectStackSize(Runtime, 3);

            VMObject temp;

            var source = PopAddress(Runtime);

            temp = Runtime.Stack.Pop();
            Runtime.Expect(temp.Type == VMType.String, "expected string for name");
            var name = temp.AsString();

            var target = PopAddress(Runtime);

            Runtime.AddMember(name, source, target);

            return ExecutionState.Running;
        }
    }
}
