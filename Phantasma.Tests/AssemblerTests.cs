using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.Blockchain;
using Phantasma.Core.Log;
using Phantasma.Cryptography;
using Phantasma.CodeGen.Assembler;
using Phantasma.Core.Types;
using Phantasma.Numerics;
using Phantasma.VM;
using Phantasma.Simulator;
using Phantasma.Storage.Context;
using Phantasma.VM.Utils;
using Phantasma.Domain;
using Phantasma.Storage;
using Phantasma.Blockchain.Contracts;
using Phantasma.Blockchain.Tokens;

namespace Phantasma.Tests
{
    [TestClass]
    public class AssemblerTests
    {

        [TestMethod]
        public void Alias()
        {
            string[] scriptString;
            TestVM vm;

            scriptString = new string[]
            {
                $"alias r1, $hello",
                $"alias r2, $world",
                $"load $hello, 3",
                $"load $world, 2",
                $"add r1, r2, r3",
                $"push r3",
                $"ret"
            };

            vm = ExecuteScriptIsolated(scriptString);

            var result = vm.Stack.Pop().AsNumber();
            Assert.IsTrue(result == 5);
        }

        private bool IsInvalidCast(Exception e)
        {
            return e.Message.StartsWith("Cannot convert") 
                || e.Message.StartsWith("Invalid cast")
                || e.Message.StartsWith("logical op unsupported");
        }

        [TestMethod]
        public void EventNotify()
        {
            string[] scriptString;

            var owner = PhantasmaKeys.Generate();
            var addressStr = Base16.Encode(owner.Address.ToByteArray());

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            string message = "customEvent";
            var methodName = "notify";

            scriptString = new string[]
            {
                $"@{methodName}: NOP ",
                $"load r11 0x{addressStr}",
                $"push r11",
                $@"extcall ""Address()""",
                $"pop r11",

                $"load r10, {(int)EventKind.Custom}",
                $@"load r12, ""{message}""",

                $"push r12",
                $"push r11",
                $"push r10",
                $@"extcall ""Runtime.Notify""",
                @"ret",
            };

            DebugInfo debugInfo;
            Dictionary<string, int> labels;
            var script = AssemblerUtils.BuildScript(scriptString, "test", out debugInfo, out labels);

            var methods = new[]
            {
                new ContractMethod(methodName , VMType.None, labels[methodName], new ContractParameter[0])
            };
            var abi = new ContractInterface(methods, Enumerable.Empty<ContractEvent>());
            var abiBytes = abi.ToByteArray();

            var contractName = "test";
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(owner, ProofOfWork.Minimal,
                () => ScriptUtils.BeginScript().AllowGas(owner.Address, Address.Null, 1, 9999)
                    .CallInterop("Runtime.DeployContract", owner.Address, contractName, script, abiBytes)
                    .SpendGas(owner.Address)
                    .EndScript());
            simulator.EndBlock();

            scriptString = new string[]
            {
                $"load r1, \\\"test\\\"",
                $"ctx r1, r2",
                $"load r3, \\\"notify\\\"",
                $"push r3",
                $"switch r2",
            };

            script = AssemblerUtils.BuildScript(scriptString);
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(owner, ProofOfWork.None, (() =>
                ScriptUtils.BeginScript()
                    .AllowGas(owner.Address, Address.Null, 1, 9999)
                    .EmitRaw(script)
                    .SpendGas(owner.Address)
                    .EndScript()));
            simulator.EndBlock();

            var events = simulator.Nexus.FindBlockByTransaction(tx).GetEventsForTransaction(tx.Hash);
            Assert.IsTrue(events.Count(x => x.Kind == EventKind.Custom) == 1);

            var eventData = events.First(x => x.Kind == EventKind.Custom).Data;
            var eventMessage = (VMObject)Serialization.Unserialize(eventData, typeof(VMObject));

            Assert.IsTrue(eventMessage.AsString() == message);
        }

        [TestMethod]
        public void TokenTriggers()
        {
            string[] scriptString;
            //TestVM vm;


            var owner = PhantasmaKeys.Generate();
            var target = PhantasmaKeys.Generate();
            var symbol = "TEST";
            var flags = TokenFlags.Transferable | TokenFlags.Finite | TokenFlags.Fungible | TokenFlags.Divisible;
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            string message = "customEvent";
            var addressStr = Base16.Encode(owner.Address.ToByteArray());

            scriptString = new string[]
            {
                $"alias r1, $triggerSend",
                $"alias r2, $triggerReceive",
                $"alias r3, $triggerBurn",
                $"alias r4, $triggerMint",
                $"alias r5, $currentTrigger",
                $"alias r6, $comparisonResult",

                $@"load $triggerSend, ""{TokenTrigger.OnSend}""",
                $@"load $triggerReceive, ""{TokenTrigger.OnReceive}""",
                $@"load $triggerBurn, ""{TokenTrigger.OnBurn}""",
                $@"load $triggerMint, ""{TokenTrigger.OnMint}""",
                $"pop $currentTrigger",

                $"equal $triggerSend, $currentTrigger, $comparisonResult",
                $"jmpif $comparisonResult, @sendHandler",

                $"equal $triggerReceive, $currentTrigger, $comparisonResult",
                $"jmpif $comparisonResult, @receiveHandler",

                $"equal $triggerBurn, $currentTrigger, $comparisonResult",
                $"jmpif $comparisonResult, @burnHandler",

                $"equal $triggerMint, $currentTrigger, $comparisonResult",
                $"jmpif $comparisonResult, @OnMint",

                $"ret",

                $"@sendHandler: ret",

                $"@receiveHandler: ret",

                $"@burnHandler: load r7 \"test burn handler exception\"",
                $"throw r7",

                $"@OnMint: ret",
            };

            var script = AssemblerUtils.BuildScript(scriptString, null, out var debugInfo, out var labels);

            simulator.BeginBlock();
            simulator.GenerateToken(owner, symbol, $"{symbol}Token", 1000000000, 3, flags, script, labels);
            var tx = simulator.MintTokens(owner, owner.Address, symbol, 1000);
            simulator.GenerateTransfer(owner, target.Address, simulator.Nexus.RootChain, symbol, 10);
            simulator.EndBlock();

            //var token = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, symbol);
            //var balance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, owner.Address);
            //Assert.IsTrue(balance == 1000);

            //Assert.ThrowsException<ChainException>(() =>
            //{
            //    simulator.BeginBlock();
            //    simulator.GenerateTransfer(owner, target.Address, simulator.Nexus.RootChain, symbol, 10);
            //    simulator.EndBlock();
            //});

            //balance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, owner.Address);
            //Assert.IsTrue(balance == 1000);
        }

        [TestMethod]
        public void TokenTriggersEventPropagation()
        {
            string[] scriptString;
            //TestVM vm;


            var owner = PhantasmaKeys.Generate();
            var target = PhantasmaKeys.Generate();
            var symbol = "TEST";
            var flags = TokenFlags.Transferable | TokenFlags.Finite | TokenFlags.Fungible | TokenFlags.Divisible;
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            string message = "customEvent";
            var addressStr = Base16.Encode(owner.Address.ToByteArray());

            scriptString = new string[]
            {
                $"alias r1, $triggerSend",
                $"alias r2, $triggerReceive",
                $"alias r3, $triggerBurn",
                $"alias r4, $triggerMint",
                $"alias r5, $currentTrigger",
                $"alias r6, $comparisonResult",

                $@"load $triggerSend, ""{TokenTrigger.OnSend}""",
                $@"load $triggerReceive, ""{TokenTrigger.OnReceive}""",
                $@"load $triggerBurn, ""{TokenTrigger.OnBurn}""",
                $@"load $triggerMint, ""{TokenTrigger.OnMint}""",
                $"pop $currentTrigger",

                //$"equal $triggerSend, $currentTrigger, $comparisonResult",
                //$"jmpif $comparisonResult, @sendHandler",

                //$"equal $triggerReceive, $currentTrigger, $comparisonResult",
                //$"jmpif $comparisonResult, @receiveHandler",

                //$"equal $triggerBurn, $currentTrigger, $comparisonResult",
                //$"jmpif $comparisonResult, @burnHandler",

                //$"equal $triggerMint, $currentTrigger, $comparisonResult",
                //$"jmpif $comparisonResult, @OnMint",

                $"jmp @return",

                $"@sendHandler: load r7 \"test send handler exception\"",
                $"throw r7",

                $"@receiveHandler: load r7 \"test received handler exception\"",
                $"throw r7",

                $"@burnHandler: load r7 \"test burn handler exception\"",
                $"throw r7",

                $"@OnMint: load r11 0x{addressStr}",
                $"push r11",
                $@"extcall ""Address()""",
                $"pop r11",

                $"load r10, {(int)EventKind.Custom}",
                $@"load r12, ""{message}""",

                $"push r12",
                $"push r11",
                $"push r10",
                $@"extcall ""Runtime.Notify""",
                "ret",

                $"@return: ret",
            };

            var script = AssemblerUtils.BuildScript(scriptString, null, out var debugInfo, out var labels);

            simulator.BeginBlock();
            simulator.GenerateToken(owner, symbol, $"{symbol}Token", 1000000000, 3, flags, script, labels);
            simulator.EndBlock();

            simulator.BeginBlock();
            var tx = simulator.MintTokens(owner, owner.Address, symbol, 1000);
            simulator.EndBlock();

            var token = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, symbol);
            var balance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, owner.Address);
            Assert.IsTrue(balance == 1000);

            var events = simulator.Nexus.FindBlockByTransaction(tx).GetEventsForTransaction(tx.Hash);
            Assert.IsTrue(events.Count(x => x.Kind == EventKind.Custom) == 1);

            var eventData = events.First(x => x.Kind == EventKind.Custom).Data;
            var eventMessage = (VMObject)Serialization.Unserialize(eventData, typeof(VMObject));

            Assert.IsTrue(eventMessage.AsString() == message);

            /*Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateTransfer(owner, target.Address, simulator.Nexus.RootChain, symbol, 10000);
                simulator.EndBlock();
            });*/

            balance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, owner.Address);
            Assert.IsTrue(balance == 1000);
        }

        [TestMethod]
        public void AccountTriggersAllowance()
        {
            string[] scriptString;

            var owner = PhantasmaKeys.Generate();
            var target = PhantasmaKeys.Generate();
            var other = PhantasmaKeys.Generate();

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var symbol = "SOUL";
            var addressStr = Base16.Encode(other.Address.ToByteArray());

            scriptString = new string[]
            {
                $"alias r1, $temp",
                $"alias r2, $from",
                $"alias r3, $to",
                $"alias r4, $symbol",
                $"alias r5, $amount",

                $"jmp @end",

                $"@OnReceive: nop",
                $"pop $from",
                $"pop $to",
                $"pop $symbol",
                $"pop $amount",

                $@"load r1 ""{symbol}""",
                $@"equal r1, $symbol, $temp",
                $"jmpnot $temp, @end",

                $"load $temp 2",
                $"div $amount $temp $temp",

                $"push $temp",
                $"push $symbol",
                $"load r11 0x{addressStr}",
                $"push r11",
                $@"extcall ""Address()""",
                $"push $to",

                $"load r0 \"Runtime.TransferTokens\"",
                $"extcall r0",
                $"jmp @end",

                $"@end: ret"
            };


            Dictionary<string, int> labels;
            DebugInfo debugInfo;
            var script = AssemblerUtils.BuildScript(scriptString, "test", out debugInfo, out labels);

            var methods = TokenUtils.GetTriggersForABI(labels);
            var abi = new ContractInterface(methods, Enumerable.Empty<ContractEvent>());

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, target.Address, simulator.Nexus.RootChain, "KCAL", UnitConversion.GetUnitValue(DomainSettings.FuelTokenDecimals));
            simulator.GenerateTransfer(owner, target.Address, simulator.Nexus.RootChain, "SOUL", UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals));
            simulator.GenerateCustomTransaction(target, ProofOfWork.None,
                () => ScriptUtils.BeginScript()
                        .AllowGas(target.Address, Address.Null, 1, 9999)
                    .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), target.Address, UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals))
                        .SpendGas(target.Address)
                    .EndScript());
            simulator.EndBlock();


            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(target, ProofOfWork.None,
                () => ScriptUtils.BeginScript()
                        .AllowGas(target.Address, Address.Null, 1, 9999)
                    .CallContract(NativeContractKind.Account, nameof(AccountContract.RegisterScript), target.Address, script, abi.ToByteArray())
                        .SpendGas(target.Address)
                    .EndScript());
            simulator.EndBlock();

            var initialBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, symbol, target.Address);

            var amount = UnitConversion.ToBigInteger(5, DomainSettings.StakingTokenDecimals);

            simulator.BeginBlock();
            var tx = simulator.GenerateTransfer(owner, target.Address, simulator.Nexus.RootChain, symbol, amount * 2);
            simulator.EndBlock();

            var balance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, symbol, target.Address);
            var expectedBalance = initialBalance + amount;
            Assert.IsTrue(balance == expectedBalance);

            balance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, symbol, other.Address);
            Assert.IsTrue(balance == amount);
        }

        [TestMethod]
        [Ignore]
        public void AccountTriggersEventPropagation()
        {
            string[] scriptString;

            var owner = PhantasmaKeys.Generate();
            var target = PhantasmaKeys.Generate();
            var symbol = "TEST";
            var flags = TokenFlags.Transferable | TokenFlags.Finite | TokenFlags.Fungible | TokenFlags.Divisible;
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            string message = "customEvent";
            var addressStr = Base16.Encode(target.Address.ToByteArray());
            var isTrue = true;

            scriptString = new string[]
            {
                $"alias r1, $triggerSend",
                $"alias r2, $triggerReceive",
                $"alias r3, $triggerBurn",
                $"alias r4, $triggerMint",
                $"alias r5, $currentTrigger",
                $"alias r6, $comparisonResult",
                $"alias r7, $triggerWitness",
                $"alias r8, $currentAddress",
                $"alias r9, $sourceAddress",

                $@"load $triggerSend, ""{AccountTrigger.OnSend}""",
                $@"load $triggerReceive, ""{AccountTrigger.OnReceive}""",
                $@"load $triggerBurn, ""{AccountTrigger.OnBurn}""",
                $@"load $triggerMint, ""{AccountTrigger.OnMint}""",
                $@"load $triggerWitness, ""{AccountTrigger.OnWitness}""",
                $"pop $currentTrigger",
                $"pop $currentAddress",

                $"equal $triggerWitness, $currentTrigger, $comparisonResult",
                $"jmpif $comparisonResult, @witnessHandler",

                $"equal $triggerSend, $currentTrigger, $comparisonResult",
                $"jmpif $comparisonResult, @sendHandler",

                $"equal $triggerReceive, $currentTrigger, $comparisonResult",
                $"jmpif $comparisonResult, @receiveHandler",

                $"equal $triggerBurn, $currentTrigger, $comparisonResult",
                $"jmpif $comparisonResult, @burnHandler",

                $"equal $triggerMint, $currentTrigger, $comparisonResult",
                $"jmpif $comparisonResult, @OnMint",

                $"jmp @end",

                $"@witnessHandler: ",
                $"load r11 0x{addressStr}",
                $"push r11",
                "extcall \"Address()\"",
                $"pop $sourceAddress",
                $"equal $sourceAddress, $currentAddress, $comparisonResult",
                "jmpif $comparisonResult, @endWitness",
                $"load r1 \"test witness handler xception\"",
                $"throw r1",
                
                "jmp @end",

                $"@sendHandler: jmp @end",

                $"@receiveHandler: jmp @end",

                $"@burnHandler: jmp @end",

                $"@OnMint: load r11 0x{addressStr}",
                $"push r11",
                $@"extcall ""Address()""",
                $"pop r11",

                $"load r10, {(int)EventKind.Custom}",
                $@"load r12, ""{message}""",

                $"push r12",
                $"push r11",
                $"push r10",
                $@"extcall ""Runtime.Notify""",

                $"@endWitness: ret",
                $"load r11 {isTrue}",
                $"push r11",

                $"@end: ret"
            };

            var script = AssemblerUtils.BuildScript(scriptString, null, out var something, out var labels);
            var methods = new List<ContractMethod>();
            methods.Add(new ContractMethod("OnMint", VMType.None, 205, new ContractParameter[0]));
            var abi = new ContractInterface(methods, new List<ContractEvent>());

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, target.Address, simulator.Nexus.RootChain, "KCAL", 60000000000000);
            simulator.GenerateTransfer(owner, target.Address, simulator.Nexus.RootChain, "SOUL", UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals)*50000);
            simulator.GenerateCustomTransaction(target, ProofOfWork.None,
                () => ScriptUtils.BeginScript().AllowGas(target.Address, Address.Null, 1, 9999)
                    .CallContract("stake", "Stake", target.Address, UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals)*50000)
                    .CallContract("account", "RegisterScript", target.Address, script, abi.ToByteArray()).SpendGas(target.Address)
                    .EndScript());
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateToken(target, symbol, $"{symbol}Token", 1000000000, 3, flags);
            var tx = simulator.MintTokens(target, target.Address, symbol, 1000);
            simulator.EndBlock();

            var accountScript = simulator.Nexus.LookUpAddressScript(simulator.Nexus.RootStorage, target.Address);
            Assert.IsTrue(accountScript != null && accountScript.Length > 0);

            var token = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, symbol);
            var balance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, target.Address);
            Assert.IsTrue(balance == 1000);

            var events = simulator.Nexus.FindBlockByTransaction(tx).GetEventsForTransaction(tx.Hash);
            Assert.IsTrue(events.Count(x => x.Kind == EventKind.Custom) == 1);

            var eventData = events.First(x => x.Kind == EventKind.Custom).Data;
            var eventMessage = (VMObject)Serialization.Unserialize(eventData, typeof(VMObject));

            Assert.IsTrue(eventMessage.AsString() == message);
            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateTransfer(owner, target.Address, simulator.Nexus.RootChain, symbol, 10);
                simulator.EndBlock();
            });

            balance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, target.Address);
            Assert.IsTrue(balance == 1000);
        }

        #region RegisterOps

        [TestMethod]
        public void Move()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<int>>()
            {
                new List<int>() {1, 1},
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                object r1 = argsLine[0];
                object target = argsLine[0];    //index 0 is not a typo, we want to copy the reference, not the contents

                scriptString = new string[]
                {
                    //put a DebugClass with x = {r1} on register 1
                    $@"load r1, {r1}",
                    $"push r1",
                    $"extcall \\\"PushDebugClass\\\"", 
                    $"pop r1",

                    //move it to r2, change its value on the stack and see if it changes on both registers
                    @"move r1, r2",
                    @"push r2",
                    $"push r1",
                    @"ret"
                };

                vm = ExecuteScriptIsolated(scriptString);

                Assert.IsTrue(vm.Stack.Count == 2);

                var r1obj = vm.Stack.Pop();
                var r2obj = vm.Stack.Pop();

                Assert.IsTrue(r1obj.Type == VMType.None);
                Assert.IsTrue(r2obj.Type == VMType.Object);
            }
        }

        [TestMethod]
        public void Copy()
        {
            string[] scriptString;
            TestVM vm;

            scriptString = new string[]
            {
                //put a DebugClass with x = {r1} on register 1
                //$@"load r1, {value}",
                $"load r5, 1",
                $"push r5",
                $"extcall \\\"PushDebugStruct\\\"",
                $"pop r1",
                $"load r3, \\\"key\\\"",
                $"put r1, r2, r3",

                //move it to r2, change its value on the stack and see if it changes on both registers
                @"copy r1, r2",
                @"push r2",
                $"extcall \\\"IncrementDebugStruct\\\"",
                $"push r1",
                @"ret"
            };

            vm = ExecuteScriptIsolated(scriptString);

            var r1struct = vm.Stack.Pop().AsInterop<TestVM.DebugStruct>();
            var r2struct = vm.Stack.Pop().AsInterop<TestVM.DebugStruct>();

            Assert.IsTrue(r1struct.x != r2struct.x);

        }

        [TestMethod]
        public void Load()
        {
            //TODO: test all VMTypes

            string[] scriptString;
            TestVM vm;

            scriptString = new string[]
            {
                $"load r1, \\\"hello\\\"",
                $"load r2, 123",
                $"load r3, true",
                //load struct
                //load bytes
                //load enum
                //load object

                $"push r3",
                $"push r2",
                $"push r1",
                $"ret"
            };

            vm = ExecuteScriptIsolated(scriptString);

            Assert.IsTrue(vm.Stack.Count == 3);

            var str = vm.Stack.Pop().AsString();
            Assert.IsTrue(str.CompareTo("hello") == 0);

            var num = vm.Stack.Pop().AsNumber();
            Assert.IsTrue(num == new BigInteger(123));

            var bo = vm.Stack.Pop().AsBool();
            Assert.IsTrue(bo);
        }

        [TestMethod]
        public void Push()
        {
            Load(); //it is effectively the same test
        }

        [TestMethod]
        public void Pop()
        {
            //TODO: test all VMTypes

            string[] scriptString;
            TestVM vm;

            scriptString = new string[]
            {
                $"load r1, \\\"hello\\\"",
                $"load r2, 123",
                $"load r3, true",
                //load struct
                //load bytes
                //load enum
                //load object

                $"push r3",
                $"push r2",
                $"push r1",

                $"pop r11",
                $"pop r12",
                $"pop r13",

                $"push r13",
                $"push r12",
                $"push r11",
                $"ret"
            };

            vm = ExecuteScriptIsolated(scriptString);

            Assert.IsTrue(vm.Stack.Count == 3);

            var str = vm.Stack.Pop().AsString();
            Assert.IsTrue(str.CompareTo("hello") == 0);

            var num = vm.Stack.Pop().AsNumber();
            Assert.IsTrue(num == new BigInteger(123));

            var bo = vm.Stack.Pop().AsBool();
            Assert.IsTrue(bo);
        }

        [TestMethod]
        public void Swap()
        {
            string[] scriptString;
            TestVM vm;

            scriptString = new string[]
            {
                $"load r1, \\\"hello\\\"",
                $"load r2, 123",
                $"swap r1, r2",
                $"push r1",
                $"push r2",
                $"ret"
            };

            vm = ExecuteScriptIsolated(scriptString);

            Assert.IsTrue(vm.Stack.Count == 2);

            var str = vm.Stack.Pop().AsString();
            Assert.IsTrue(str.CompareTo("hello") == 0);

            var num = vm.Stack.Pop().AsNumber();
            Assert.IsTrue(num == new BigInteger(123));
        }

        #endregion

        #region FlowOps

        [TestMethod]
        public void Call()
        {
            var initVal = 2;
            var targetVal = initVal + 1;

            var scriptString = new string[]
            {
                $@"load r1 {initVal}",
                @"push r1",
                @"call @label",
                @"ret",
                $"@label: pop r1",
                @"inc r1",
                $"push r1",
                $"ret"
            };

            var vm = ExecuteScriptIsolated(scriptString);

            Assert.IsTrue(vm.Stack.Count == 1);

            var result = vm.Stack.Pop().AsNumber();
            Assert.IsTrue(result == targetVal);
        }

        [TestMethod]
        public void ExtCall()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"abc", "ABC"},
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                var r1 = argsLine[0];
                var target = argsLine[1];

                scriptString = new string[]
                {
                    $"load r1, \\\"{r1}\\\"",
                    $"push r1",
                    $"extcall \\\"Upper\\\"",
                    @"ret"
                };

                vm = ExecuteScriptIsolated(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }
        }

        [TestMethod]
        public void Jmp()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<int>>()
            {
                new List<int>() {1, 1},
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                var r1 = argsLine[0];
                var target = argsLine[1];

                scriptString = new string[]
                {
                    $"load r1, 1",
                    $"jmp @label",
                    $"inc r1",
                    $"@label: push r1",
                    $"ret"
                };

                vm = ExecuteScriptIsolated(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsNumber();
                Assert.IsTrue(result == target);
            }
        }

        [TestMethod]
        public void JmpConditional()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<int>>()
            {
                new List<int>() {1, 1},
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                var r1 = argsLine[0];
                var target = argsLine[1];

                scriptString = new string[]
                {
                    $"load r1, true",
                    $"load r2, false",
                    $"load r3, {r1}",
                    $"load r4, {r1}",
                    $"jmpif r1, @label",
                    $"inc r3",
                    $"@label: jmpnot r2, @label2",
                    $"inc r4",
                    $"@label2: push r3",
                    $"push r4",
                    $"ret"
                };

                vm = ExecuteScriptIsolated(scriptString);

                Assert.IsTrue(vm.Stack.Count == 2);

                var result = vm.Stack.Pop().AsNumber();
                Assert.IsTrue(result == target, "Opcode JmpNot isn't working correctly");

                result = vm.Stack.Pop().AsNumber();
                Assert.IsTrue(result == target, "Opcode JmpIf isn't working correctly");
            }
        }

        [TestMethod]
        public void Throw()
        {
            string[] scriptString;
            TestVM vm = null;

            var args = new List<List<bool>>()
            {
                new List<bool>() {true, true},
            };

            var msg = "exception";

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                var r1 = argsLine[0];
                var target = argsLine[1];

                scriptString = new string[]
                {
                    $"load r1, {r1}",
                    $"push r1",
                    $"load r1 \"test throw exception\"",
                    $"throw r1",
                    $"not r1, r1",
                    $"pop r2",
                    $"push r1",
                    $"ret"
                };

                bool result = false;
                Assert.ThrowsException<VMException>(() =>
                {
                    vm = ExecuteScriptIsolated(scriptString);
                    Assert.IsTrue(vm.Stack.Count == 1);
                    result = vm.Stack.Pop().AsBool();
                    Assert.IsTrue(result == target, "Opcode JmpNot isn't working correctly");
                });



            }
        }


        #endregion

        #region LogicalOps
        [TestMethod]
        public void Not()
        {
            var scriptString = new string[]
            {
                $@"load r1, true",
                @"not r1, r2",
                @"push r2",
                @"ret"
            };

            var vm = ExecuteScriptIsolated(scriptString);

            Assert.IsTrue(vm.Stack.Count == 1);

            var result = vm.Stack.Pop().AsString();
            Assert.IsTrue(result == "false");

            scriptString = new string[]
            {
                $"load r1, \\\"abc\\\"",
                @"not r1, r2",
                @"push r2",
                @"ret"
            };

            try
            {
                vm = ExecuteScriptIsolated(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(IsInvalidCast(e));
                return;
            }

            throw new Exception("Didn't throw an exception after trying to NOT a non-bool variable.");
        }

        [TestMethod]
        public void And()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"true", "true", "true"},
                new List<string>() {"true", "false", "false"},
                new List<string>() {"false", "true", "false"},
                new List<string>() {"false", "false", "false"}
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string r2 = argsLine[1];
                string target = argsLine[2];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    $@"load r2, {r2}",
                    @"and r1, r2, r3",
                    @"push r3",
                    @"ret"
                };

                vm = ExecuteScriptIsolated(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $"load r1, \\\"abc\\\"",
                $@"load r2, false",
                @"and r1, r2, r3",
                @"push r3",
                @"ret"
            };

            try
            {
                vm = ExecuteScriptIsolated(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(IsInvalidCast(e));
                return;
            }

            throw new Exception("Didn't throw an exception after trying to AND a non-bool variable.");
        }

        [TestMethod]
        public void Or()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"true", "true", "true"},
                new List<string>() {"true", "false", "true"},
                new List<string>() {"false", "true", "true"},
                new List<string>() {"false", "false", "false"}
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string r2 = argsLine[1];
                string target = argsLine[2];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    $@"load r2, {r2}",
                    @"or r1, r2, r3",
                    @"push r3",
                    @"ret"
                };

                vm = ExecuteScriptIsolated(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $"load r1, \\\"abc\\\"",
                $@"load r2, false",
                @"or r1, r2, r3",
                @"push r3",
                @"ret"
            };

            try
            {
                vm = ExecuteScriptIsolated(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(IsInvalidCast(e));
                return;
            }

            throw new Exception("Didn't throw an exception after trying to OR a non-bool variable.");
        }

        [TestMethod]
        public void Xor()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"true", "true", "false"},
                new List<string>() {"true", "false", "true"},
                new List<string>() {"false", "true", "true"},
                new List<string>() {"false", "false", "false"}
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string r2 = argsLine[1];
                string target = argsLine[2];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    $@"load r2, {r2}",
                    @"xor r1, r2, r3",
                    @"push r3",
                    @"ret"
                };

                vm = ExecuteScriptIsolated(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $"load r1, \\\"abc\\\"",
                $@"load r2, false",
                @"xor r1, r2, r3",
                @"push r3",
                @"ret"
            };

            try
            {
                vm = ExecuteScriptIsolated(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(IsInvalidCast(e));
                return;
            }

            throw new Exception("Didn't throw an exception after trying to XOR a non-bool variable.");
        }

        [TestMethod]
        public void Equals()
        {
            string[] scriptString;
            TestVM vm;
            string result;

            var args = new List<List<string>>()
            {
                new List<string>() {"true", "true", "true"},
                new List<string>() {"true", "false", "false"},
                new List<string>() {"1", "1", "true"},
                new List<string>() {"1", "2", "false"},
                new List<string>() { "\\\"hello\\\"", "\\\"hello\\\"", "true"},
                new List<string>() { "\\\"hello\\\"", "\\\"world\\\"", "false"},
                
                //TODO: add lines for bytes, structs, enums and structs
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string r2 = argsLine[1];
                string target = argsLine[2];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    $@"load r2, {r2}",
                    @"equal r1, r2, r3",
                    @"push r3",
                    @"ret"
                };

                vm = ExecuteScriptIsolated(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);


                result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }
        }

        [TestMethod]
        public void LessThan()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"1", "0", "false"},
                new List<string>() {"1", "1", "false"},
                new List<string>() {"1", "2", "true"},
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string r2 = argsLine[1];
                string target = argsLine[2];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    $@"load r2, {r2}",
                    @"lt r1, r2, r3",
                    @"push r3",
                    @"ret"
                };

                vm = ExecuteScriptIsolated(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $"load r1, \\\"abc\\\"",
                $@"load r2, 2",
                @"lt r1, r2, r3",
                @"push r3",
                @"ret"
            };

            try
            {
                vm = ExecuteScriptIsolated(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(IsInvalidCast(e));
                return;
            }

            throw new Exception("Didn't throw an exception after trying to compare non-integer variables.");
        }

        [TestMethod]
        public void GreaterThan()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"1", "0", "true"},
                new List<string>() {"1", "1", "false"},
                new List<string>() {"1", "2", "false"},
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string r2 = argsLine[1];
                string target = argsLine[2];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    $@"load r2, {r2}",
                    @"gt r1, r2, r3",
                    @"push r3",
                    @"ret"
                };

                vm = ExecuteScriptIsolated(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $"load r1, \\\"abc\\\"",
                $@"load r2, 2",
                @"gt r1, r2, r3",
                @"push r3",
                @"ret"
            };

            try
            {
                vm = ExecuteScriptIsolated(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(IsInvalidCast(e));
                return;
            }

            throw new Exception("Didn't throw an exception after trying to compare non-integer variables.");
        }

        [TestMethod]
        public void LesserThanOrEquals()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"1", "0", "false"},
                new List<string>() {"1", "1", "true"},
                new List<string>() {"1", "2", "true"},
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string r2 = argsLine[1];
                string target = argsLine[2];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    $@"load r2, {r2}",
                    @"lte r1, r2, r3",
                    @"push r3",
                    @"ret"
                };

                vm = ExecuteScriptIsolated(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $"load r1, \\\"abc\\\"",
                $@"load r2, 2",
                @"lte r1, r2, r3",
                @"push r3",
                @"ret"
            };

            try
            {
                vm = ExecuteScriptIsolated(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(IsInvalidCast(e));
                return;
            }

            throw new Exception("Didn't throw an exception after trying to compare non-integer variables.");
        }

        [TestMethod]
        public void GreaterThanOrEquals()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"1", "0", "true"},
                new List<string>() {"1", "1", "true"},
                new List<string>() {"1", "2", "false"},
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string r2 = argsLine[1];
                string target = argsLine[2];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    $@"load r2, {r2}",
                    @"gte r1, r2, r3",
                    @"push r3",
                    @"ret"
                };

                vm = ExecuteScriptIsolated(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $"load r1, \\\"abc\\\"",
                $@"load r2, 2",
                @"gte r1, r2, r3",
                @"push r3",
                @"ret"
            };

            try
            {
                vm = ExecuteScriptIsolated(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(IsInvalidCast(e));
                return;
            }

            throw new Exception("Didn't throw an exception after trying to compare non-integer variables.");
        }
        #endregion

        #region NumericOps
        [TestMethod]
        public void Increment()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"1", "2"},
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string target = argsLine[1];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    @"inc r1",
                    @"push r1",
                    @"ret"
                };

                vm = ExecuteScriptIsolated(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $"load r1, \\\"hello\\\"",
                @"inc r1",
                @"push r1",
                @"ret"
            };

            try
            {
                vm = ExecuteScriptIsolated(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(IsInvalidCast(e));
                return;
            }

            throw new Exception("Didn't throw an exception after trying to compare non-integer variables.");
        }

        [TestMethod]
        public void Decrement()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"2", "1"},
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string target = argsLine[1];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    @"dec r1",
                    @"push r1",
                    @"ret"
                };

                vm = ExecuteScriptIsolated(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $"load r1, \\\"hello\\\"",
                @"dec r1",
                @"push r1",
                @"ret"
            };

            try
            {
                vm = ExecuteScriptIsolated(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(IsInvalidCast(e));
                return;
            }

            throw new Exception("Didn't throw an exception after trying to compare non-integer variables.");
        }

        [TestMethod]
        public void Sign()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"-1123124", "-1"},
                new List<string>() {"0", "0"},
                new List<string>() {"14564535", "1"}
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string target = argsLine[1];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    @"sign r1, r2",
                    @"push r2",
                    @"ret"
                };

                vm = ExecuteScriptIsolated(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $"load r1, \\\"abc\\\"",
                @"sign r1, r2",
                @"push r2",
                @"ret"
            };

            try
            {
                vm = ExecuteScriptIsolated(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(IsInvalidCast(e));
                return;
            }

            throw new Exception("Didn't throw an exception after trying to AND a non-bool variable.");
        }

        [TestMethod]
        public void Negate()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"-1123124", "1123124"},
                new List<string>() {"0", "0"},
                new List<string>() {"14564535", "-14564535" }
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string target = argsLine[1];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    @"negate r1, r2",
                    @"push r2",
                    @"ret"
                };

                vm = ExecuteScriptIsolated(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $"load r1, \\\"abc\\\"",
                @"negate r1, r2",
                @"push r2",
                @"ret"
            };

            try
            {
                vm = ExecuteScriptIsolated(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(IsInvalidCast(e));
                return;
            }

            throw new Exception("Didn't throw an exception after trying to AND a non-bool variable.");
        }

        [TestMethod]
        public void Abs()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"-1123124", "1123124"},
                new List<string>() {"0", "0"},
                new List<string>() {"14564535", "14564535" }
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string target = argsLine[1];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    @"abs r1, r2",
                    @"push r2",
                    @"ret"
                };

                vm = ExecuteScriptIsolated(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $"load r1, \"abc\"",
                @"abs r1, r2",
                @"push r2",
                @"ret"
            };

            try
            {
                vm = ExecuteScriptIsolated(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(IsInvalidCast(e));
                return;
            }

            throw new Exception("Didn't throw an exception after trying to AND a non-bool variable.");
        }

        [TestMethod]
        public void Add()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"123098123049830982903580234959875213840923849203758942357834091", "123098123049830982903580234959875213840923849203758942357834091", "246196246099661965807160469919750427681847698407517884715668182"}
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string r2 = argsLine[1];
                string target = argsLine[2];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    $@"load r2, {r2}",
                    @"add r1, r2, r3",
                    @"push r3",
                    @"ret"
                };

                vm = ExecuteScriptIsolated(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $@"load r1, true",
                $"load r2, \\\"stuff\\\"",
                @"add r1, r2, r3",
                @"push r3",
                @"ret"
            };


            try
            {
                vm = ExecuteScriptIsolated(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(IsInvalidCast(e));
                return;
            }

            throw new Exception("Didn't throw an exception after trying to AND a non-bool variable.");
        }

        [TestMethod]
        public void Sub()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"123098123049830982903580234959875213840923849203758942357834091", "123098123049830982903580234959875213840923849203758942357834091", "0"}
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string r2 = argsLine[1];
                string target = argsLine[2];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    $@"load r2, {r2}",
                    @"sub r1, r2, r3",
                    @"push r3",
                    @"ret"
                };

                vm = ExecuteScriptIsolated(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $@"load r1, true",
                $"load r2, \\\"stuff\\\"",
                @"sub r1, r2, r3",
                @"push r3",
                @"ret"
            };


            try
            {
                vm = ExecuteScriptIsolated(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(IsInvalidCast(e));
                return;
            }

            throw new Exception("Didn't throw an exception after trying to AND a non-bool variable.");
        }

        [TestMethod]
        public void Mul()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"123098123049830982903580234959875213840923849203758942357834091", "123098123049830982903580234959875213840923849203758942357834091", "15153147898391329927834760664056143940222558862285292671240041298552647375412113910342337827528430805055673715428680681796281"}
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string r2 = argsLine[1];
                string target = argsLine[2];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    $@"load r2, {r2}",
                    @"mul r1, r2, r3",
                    @"push r3",
                    @"ret"
                };

                vm = ExecuteScriptIsolated(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $@"load r1, true",
                $"load r2, \\\"stuff\\\"",
                @"mul r1, r2, r3",
                @"push r3",
                @"ret"
            };


            try
            {
                vm = ExecuteScriptIsolated(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(IsInvalidCast(e));
                return;
            }

            throw new Exception("Didn't throw an exception after trying to AND a non-bool variable.");
        }

        [TestMethod]
        public void Div()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"123098123049830982903580234959875213840923849203758942357834091", "123098123049830982903580234959875213840923849203758942357834091", "1"}
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string r2 = argsLine[1];
                string target = argsLine[2];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    $@"load r2, {r2}",
                    @"div r1, r2, r3",
                    @"push r3",
                    @"ret"
                };

                vm = ExecuteScriptIsolated(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $@"load r1, true",
                $"load r2, \\\"stuff\\\"",
                @"div r1, r2, r3",
                @"push r3",
                @"ret"
            };


            try
            {
                vm = ExecuteScriptIsolated(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(IsInvalidCast(e));
                return;
            }

            throw new Exception("Didn't throw an exception after trying to AND a non-bool variable.");
        }

        [TestMethod]
        public void Mod()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"123098123049830982903580234959875213840923849203758942357834091", "123098123049830982903580234959875213840923849203758942357834091", "0"}
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string r2 = argsLine[1];
                string target = argsLine[2];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    $@"load r2, {r2}",
                    @"mod r1, r2, r3",
                    @"push r3",
                    @"ret"
                };

                vm = ExecuteScriptIsolated(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $@"load r1, true",
                $"load r2, \\\"stuff\\\"",
                @"mod r1, r2, r3",
                @"push r3",
                @"ret"
            };


            try
            {
                vm = ExecuteScriptIsolated(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(IsInvalidCast(e));
                return;
            }

            throw new Exception("Didn't throw an exception after trying to AND a non-bool variable.");
        }

        [TestMethod]
        public void ShiftLeft()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"123098123049830982903580234959875213840923849203758942357834091", "100", "156045409571086686325343677668972466714151959338084738385422346983957734263469303184507273216"}
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string r2 = argsLine[1];
                string target = argsLine[2];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    $@"load r2, {r2}",
                    @"shl r1, r2, r3",
                    @"push r3",
                    @"ret"
                };

                vm = ExecuteScriptIsolated(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $@"load r1, true",
                $"load r2, \\\"stuff\\\"",
                @"shl r1, r2, r3",
                @"push r3",
                @"ret"
            };


            try
            {
                vm = ExecuteScriptIsolated(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(IsInvalidCast(e));
                return;
            }

            throw new Exception("Didn't throw an exception after trying to AND a non-bool variable.");
        }

        [TestMethod]
        public void ShiftRight()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"123098123049830982903580234959875213840923849203758942357834091", "100", "97107296780097167688396095959314" }
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string r2 = argsLine[1];
                string target = argsLine[2];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    $@"load r2, {r2}",
                    @"shr r1, r2, r3",
                    @"push r3",
                    @"ret"
                };

                vm = ExecuteScriptIsolated(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $@"load r1, true",
                $"load r2, \\\"stuff\\\"",
                @"shr r1, r2, r3",
                @"push r3",
                @"ret"
            };


            try
            {
                vm = ExecuteScriptIsolated(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(IsInvalidCast(e));
                return;
            }

            throw new Exception("Didn't throw an exception after trying to AND a non-bool variable.");
        }


        [TestMethod]
        public void Min()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"1", "0", "0"},
                new List<string>() {"1", "1", "1"},
                new List<string>() {"1", "2", "1"},
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string r2 = argsLine[1];
                string target = argsLine[2];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    $@"load r2, {r2}",
                    @"min r1, r2, r3",
                    @"push r3",
                    @"ret"
                };

                vm = ExecuteScriptIsolated(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $"load r1, \\\"abc\\\"",
                $@"load r2, 2",
                @"min r1, r2, r3",
                @"push r3",
                @"ret"
            };

            try
            {
                vm = ExecuteScriptIsolated(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(IsInvalidCast(e));
                return;
            }

            throw new Exception("Didn't throw an exception after trying to compare non-integer variables.");
        }

        [TestMethod]
        public void Max()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"1", "0", "1"},
                new List<string>() {"1", "1", "1"},
                new List<string>() {"1", "2", "2"},
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string r2 = argsLine[1];
                string target = argsLine[2];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    $@"load r2, {r2}",
                    @"max r1, r2, r3",
                    @"push r3",
                    @"ret"
                };

                vm = ExecuteScriptIsolated(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $"load r1, \\\"abc\\\"",
                $@"load r2, 2",
                @"max r1, r2, r3",
                @"push r3",
                @"ret"
            };

            try
            {
                vm = ExecuteScriptIsolated(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(IsInvalidCast(e));
                return;
            }

            throw new Exception("Didn't throw an exception after trying to compare non-integer variables.");
        }
        #endregion

        #region ContextOps

        [TestMethod]
        public void ContextSwitching()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<int[]>()
            {
                new int[] {1, 2},
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                var r1 = argsLine[0];
                var target = argsLine[1];

                scriptString = new string[]
                {
                    $"load r1, \\\"test\\\"",
                    $"load r3, 1",
                    $"push r3",
                    $"ctx r1, r2",
                    $"switch r2",
                    $"load r5, 42",
                    $"push r5",
                    @"ret",
                };

                vm = ExecuteScriptIsolated(scriptString);

                Assert.IsTrue(vm.Stack.Count == 2);

                var result = vm.Stack.Pop().AsNumber();
                Assert.IsTrue(result == 42);

                result = vm.Stack.Pop().AsNumber();
                Assert.IsTrue(result == 2);
            }
        }

        #endregion

        #region Array

        [TestMethod]
        public void PutGet()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<int>>()
            {
                new List<int>() {1, 1},
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                var r1 = argsLine[0];
                var target = argsLine[1];

                scriptString = new string[]
                {
                    //$"switch \\\"Test\\\"",
                    $"load r1 {r1}",
                    $"load r2 \\\"key\\\"",
                    $"put r1 r3 r2",
                    $"get r3 r4 r2",
                    $"push r4",
                    @"ret",
                };

                vm = ExecuteScriptIsolated(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsNumber();
                Assert.IsTrue(result == target);
            }
        }

        private struct TestInteropStruct
        {
            public BigInteger ID;
            public string name;
            public Address address;
        }

        [TestMethod]
        public void StructInterop()
        {
            string[] scriptString;
            TestVM vm;

            var randomKey = PhantasmaKeys.Generate();

            var demoValue = new TestInteropStruct()
            {
                ID = 1234,
                name = "monkey",
                address = randomKey.Address
            };

            var hexStr = Base16.Encode(demoValue.address.ToByteArray());

            scriptString = new string[]
            {
                // first field
                $"load r1 \\\"ID\\\"",
                $"load r2 {demoValue.ID}",
                $"put r2 r3 r1",
                $"load r1 \\\"name\\\"",

                // second field
                $"load r2 \\\"{demoValue.name}\\\"",
                $"put r2 r3 r1",
                $"load r1 \\\"address\\\"",

                // third field
                // this one is more complex because it is not a primitive type supported in the VM
                $"load r2 0x{hexStr}",
                $"push r2",
                $"extcall \\\"Address()\\\"",
                $"pop r2",
                $"put r2 r3 r1",
                $"push r3",
                @"ret",
            };

            vm = ExecuteScriptIsolated(scriptString, (_vm) =>
            {
                // here we register the interop for extcall "Address()"
                // this part would not need to be here... 
                // however this is normally done in the Chain Runtime, which we don't use for those tests
                // suggestion: maybe move some of those interop to the VM core?
                _vm.RegisterInterop("Address()", (frame) =>
                {
                    var input = _vm.Stack.Pop().AsType(VMType.Bytes);

                    try
                    {
                        var obj = Address.FromBytes((byte[])input);
                        var tempObj = new VMObject();
                        tempObj.SetValue(obj);
                        _vm.Stack.Push(tempObj);
                    }
                    catch
                    {
                        return ExecutionState.Fault;
                    }

                    return ExecutionState.Running;
                });
            });

            Assert.IsTrue(vm.Stack.Count == 1);

            var temp = vm.Stack.Pop();
            Assert.IsTrue(temp != null);

            var result = temp.ToStruct<TestInteropStruct>();
            Assert.IsTrue(demoValue.ID == result.ID);
            Assert.IsTrue(demoValue.name == result.name);
            Assert.IsTrue(demoValue.address == result.address);
        }

        [TestMethod]
        public void ArrayInterop()
        {
            TestVM vm;

            var demoArray = new BigInteger[] { 1, 42, 1024 };

            var script = new List<string>();

            for (int i=0; i<demoArray.Length; i++)
            {
                script.Add($"load r1 {i}");
                script.Add($"load r2 {demoArray[i]}");
                script.Add($"put r2 r3 r1");
            }
            script.Add("push r3");
            script.Add("ret");

            vm = ExecuteScriptIsolated(script);

            Assert.IsTrue(vm.Stack.Count == 1);

            var temp = vm.Stack.Pop();
            Assert.IsTrue(temp != null);

            var result = temp.ToArray<BigInteger>();
            Assert.IsTrue(result.Length == demoArray.Length);
        }

        #endregion

        #region Data
        [TestMethod]
        public void Cat()
        {
            var args = new List<List<string>>()
            {
                new List<string>() {"Hello", null},
                new List<string>() {null, " world"},
                new List<string>() {"", ""},
                new List<string>() {"Hello ", "world"}
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0] == null ? null : $"\"{argsLine[0]}\"";
                //string r2 = argsLine[1] == null ? null : $"\\\"{argsLine[1]}\\\"";
                string r2 = argsLine[1] == null ? null : $"\"{argsLine[1]}\"";

                var scriptString = new string[1];

                switch (i)
                {
                    case 0:
                        scriptString = new string[]
                        {
                            $@"load r1, {r1}",
                            @"cat r1, r2, r3",
                            @"push r3",
                            @"ret"
                        };
                        break;
                    case 1:
                        scriptString = new string[]
                        {
                            $@"load r2, {r2}",
                            @"cat r1, r2, r3",
                            @"push r3",
                            @"ret"
                        };
                        break;
                    case 2:
                        scriptString = new string[]
                        {
                            $@"load r1, {r1}",
                            $@"load r2, {r2}",
                            @"cat r1, r2, r3",
                            @"push r3",
                            @"ret"
                        };
                        break;
                    case 3:
                        scriptString = new string[]
                        {
                            $@"load r1, {r1}",
                            $@"load r2, {r2}",
                            @"cat r1, r2, r3",
                            @"push r3",
                            @"ret"
                        };
                        break;
                }

                var vm = ExecuteScriptIsolated(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == String.Concat(argsLine[0], argsLine[1]));
            }

            var scriptString2 = new string[]
            {
                $"load r1, \\\"Hello\\\"",
                $@"load r2, 1",
                @"cat r1, r2, r3",
                @"push r3",
                @"ret"
            };

            try
            {
                var vm2 = ExecuteScriptIsolated(scriptString2);
            }
            catch (Exception e)
            {
                Assert.IsTrue(IsInvalidCast(e));
                return;
            }

            throw new Exception("VM did not throw exception when trying to cat a string and a non-string object, and it should");
        }

        [TestMethod]
        public void Range()
        {
                //TODO: missing tests with byte data

            string r1 = "Hello funny world";
            int index = 6;
            int len = 5;
            string target = "funny";

            var scriptString = new string[1];

            scriptString = new string[]
            {
                $"load r1, \"{r1}\"",
                $"range r1, r2, {index}, {len}",
                @"push r2",
                @"ret"
            };


            var vm = ExecuteScriptIsolated(scriptString);

            Assert.IsTrue(vm.Stack.Count == 1);

            var resultBytes = vm.Stack.Pop().AsByteArray();
            var result = Encoding.UTF8.GetString(resultBytes);

            Assert.IsTrue(result == target);
        }

        [TestMethod]
        public void Left()
        {
            var args = new List<List<string>>()
            {
                new List<string>() {"Hello world", "5", "Hello"},
                //TODO: missing tests with byte data
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string len = argsLine[1];
                string target = argsLine[2];

                var scriptString = new string[1];

                scriptString = new string[]
                {
                    $"load r1, \"{r1}\"",
                    $"left r1, r2, {len}",
                    @"push r2",
                    @"ret"
                };
        

                var vm = ExecuteScriptIsolated(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var resultBytes = vm.Stack.Pop().AsByteArray();
                var result = Encoding.UTF8.GetString(resultBytes);
                
                Assert.IsTrue(result == target);
            }

            var scriptString2 = new string[]
            {
                $"load r1, 100",
                @"left r1, r2, 1",
                @"push r2",
                @"ret"
            };

            try
            {
                var vm2 = ExecuteScriptIsolated(scriptString2);
            }
            catch (Exception e)
            {
                Assert.IsTrue(IsInvalidCast(e));
                return;
            }
        }

        [TestMethod]
        public void Right()
        {
            var args = new List<List<string>>()
            {
                new List<string>() {"Hello world", "5", "world"},
                //TODO: missing tests with byte data
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string len = argsLine[1];
                string target = argsLine[2];

                var scriptString = new string[1];

                scriptString = new string[]
                {
                    $"load r1, \"{r1}\"",
                    $"right r1, r2, {len}",
                    @"push r2",
                    @"ret"
                };


                var vm = ExecuteScriptIsolated(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var resultBytes = vm.Stack.Pop().AsByteArray();
                var result = Encoding.UTF8.GetString(resultBytes);

                Assert.IsTrue(result == target);
            }

            var scriptString2 = new string[]
            {
                $"load r1, 100",
                @"right r1, r2, 1",
                @"push r2",
                @"ret"
            };

            try
            {
                var vm2 = ExecuteScriptIsolated(scriptString2);
            }
            catch (Exception e)
            {
                Assert.IsTrue(IsInvalidCast(e));
                return;
            }
        }

        [TestMethod]
        public void Size()
        {
            var args = new List<List<string>>()
            {
                new List<string>() {"Hello world"},
                //TODO: missing tests with byte data
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string target = Encoding.UTF8.GetBytes(argsLine[0]).Length.ToString();

                var scriptString = new string[1];

                scriptString = new string[]
                {
                    $"load r1, \"{r1}\"",
                    $"size r1, r2",
                    @"push r2",
                    @"ret"
                };


                var vm = ExecuteScriptIsolated(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();

                Assert.IsTrue(result == target);
            }

            var scriptString2 = new string[]
            {
                $"load r1, 100",
                @"size r1, r2",
                @"push r2",
                @"ret"
            };

            try
            {
                var vm2 = ExecuteScriptIsolated(scriptString2);
            }
            catch (Exception e)
            {
                Assert.IsTrue(IsInvalidCast(e));
                return;
            }
        }
        #endregion

        #region Disassembler
        [TestMethod]
        public void MethodExtract()
        {
            var methodName = "MyCustomMethod";

            string[] scriptString = new string[]
            {
                $"extcall \"{methodName}\"",
                $"ret"
            };

            var script = AssemblerUtils.BuildScript(scriptString);

            var table = DisasmUtils.GetDefaultDisasmTable();
            table[methodName] = 0; // this method has no args

            var calls = DisasmUtils.ExtractMethodCalls(script, table);

            Assert.IsTrue(calls.Count() == 1);
            Assert.IsTrue(calls.First().MethodName == methodName);
        }
        #endregion

        #region AuxFunctions
        private TestVM ExecuteScriptWithNexus(IEnumerable<string> scriptString, Action<TestVM> beforeExecute = null)
        {
            var owner = PhantasmaKeys.Generate();
            var script = AssemblerUtils.BuildScript(scriptString);

            var nexus = new Nexus("asmnet", new DebugLogger());
            nexus.CreateGenesisBlock(owner, Timestamp.Now, 1);
            var tx = new Transaction(nexus.Name, nexus.RootChain.Name, script, 0);

            var vm = new TestVM(tx.Script, 0);

            beforeExecute?.Invoke(vm);

            vm.Execute();

            return vm;
        }

        private TestVM ExecuteScriptIsolated(IEnumerable<string> scriptString, Action<TestVM> beforeExecute = null)
        {
            var script = AssemblerUtils.BuildScript(scriptString);

            var vm = new TestVM(script, 0);

            beforeExecute?.Invoke(vm);

            vm.Execute();

            return vm;
        }

        private TestVM ExecuteScriptIsolated(IEnumerable<string> scriptString, out Transaction tx, Action<TestVM> beforeExecute = null)
        {
            var owner = PhantasmaKeys.Generate();
            var script = AssemblerUtils.BuildScript(scriptString);

            var keys = PhantasmaKeys.Generate();
            var nexus = new Nexus("asmnet", new DebugLogger());
            nexus.CreateGenesisBlock(owner, Timestamp.Now, 1);
            tx = new Transaction(nexus.Name, nexus.RootChain.Name, script, 0);

            var vm = new TestVM(tx.Script, 0);

            beforeExecute?.Invoke(vm);

            vm.Execute();

            return vm;
        }

        #endregion

    }
}
