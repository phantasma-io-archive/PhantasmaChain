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
using Phantasma.Blockchain.Contracts;
using Phantasma.Blockchain.Contracts.Native;
using Phantasma.Blockchain.Tokens;
using Phantasma.Simulator;
using Phantasma.Storage.Context;
using Phantasma.VM.Utils;

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

            vm = ExecuteScript(scriptString);

            var result = vm.Stack.Pop().AsNumber();
            Assert.IsTrue(result == 5);
        }

        [TestMethod]
        public void EventNotify()
        {
            string[] scriptString;

            var owner = KeyPair.Generate();
            var addressStr = Base16.Encode(owner.Address.PublicKey);

            var simulator = new ChainSimulator(owner, 1234);

            string message = "customEvent";

            scriptString = new string[]
            {
                $"load r11 0x{addressStr}",
                $"push r11",
                $@"extcall ""Address()""",
                $"pop r11",

                $"load r10, {(int)EventKind.Custom}",
                $@"load r12, ""{message}""",

                $"push r10",
                $"push r11",
                $"push r12",
                $@"extcall ""Runtime.Event""",
            };

            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(owner, (() =>
                ScriptUtils.BeginScript().
                    AllowGas(owner.Address, Address.Null, 1, 9999).
                    EmitRaw(AssemblerUtils.BuildScript(scriptString)).
                    SpendGas(owner.Address).
                    EndScript()));
            simulator.EndBlock();

            var events = simulator.Nexus.FindBlockByTransaction(tx).GetEventsForTransaction(tx.Hash);
            Assert.IsTrue(events.Count(x => x.Kind == EventKind.Custom) == 1);

            var eventData = events.First(x => x.Kind == EventKind.Custom).Data;
            var eventMessage = Encoding.UTF8.GetString(eventData);

            Assert.IsTrue(eventMessage == message);
        }

        [TestMethod]
        public void TokenTriggers()
        {
            string[] scriptString;
            //TestVM vm;


            var owner = KeyPair.Generate();
            var target = KeyPair.Generate();
            var symbol = "debugNFT";
            var flags = TokenFlags.Transferable | TokenFlags.Finite | TokenFlags.Fungible | TokenFlags.Divisible;
            var simulator = new ChainSimulator(owner, 1234);

            string message = "customEvent";
            var addressStr = Base16.Encode(owner.Address.PublicKey);

            scriptString = new string[]
            {
                $"alias r1, $triggerSend",
                $"alias r2, $triggerReceive",
                $"alias r3, $triggerBurn",
                $"alias r4, $triggerMint",
                $"alias r5, $currentTrigger",
                $"alias r6, $comparisonResult",

                $@"load $triggerSend, ""{TokenContract.TriggerSend}""",
                $@"load $triggerReceive, ""{TokenContract.TriggerReceive}""",
                $@"load $triggerBurn, ""{TokenContract.TriggerBurn}""",
                $@"load $triggerMint, ""{TokenContract.TriggerMint}""",
                $"pop $currentTrigger",

                $"equal $triggerSend, $currentTrigger, $comparisonResult",
                $"jmpif $comparisonResult, @sendHandler",

                $"equal $triggerReceive, $currentTrigger, $comparisonResult",
                $"jmpif $comparisonResult, @receiveHandler",

                $"equal $triggerBurn, $currentTrigger, $comparisonResult",
                $"jmpif $comparisonResult, @burnHandler",

                $"equal $triggerMint, $currentTrigger, $comparisonResult",
                $"jmpif $comparisonResult, @mintHandler",

                $"ret",

                $"@sendHandler: throw",

                $"@receiveHandler: throw",

                $"@burnHandler: throw",

                $"@mintHandler: ret",
            };

            var script = AssemblerUtils.BuildScript(scriptString);

            simulator.BeginBlock();
            simulator.GenerateToken(owner, symbol, $"{symbol}Token", Nexus.PlatformName, Hash.FromString(symbol), 1000000000, 3, flags, script);
            var tx = simulator.MintTokens(owner, symbol, 1000);
            //simulator.GenerateTransfer(owner, target.Address, simulator.Nexus.RootChain, symbol, 10);
            simulator.EndBlock();

            var balance = simulator.Nexus.RootChain.GetTokenBalance(symbol, owner.Address);
            Assert.IsTrue(balance == 1000);

            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateTransfer(owner, target.Address, simulator.Nexus.RootChain, symbol, 10);
                simulator.EndBlock();
            });

            balance = simulator.Nexus.RootChain.GetTokenBalance(symbol, owner.Address);
            Assert.IsTrue(balance == 1000);
        }

        [TestMethod]
        public void TokenTriggersEventPropagation()
        {
            string[] scriptString;
            //TestVM vm;


            var owner = KeyPair.Generate();
            var target = KeyPair.Generate();
            var symbol = "debugNFT";
            var flags = TokenFlags.Transferable | TokenFlags.Finite | TokenFlags.Fungible | TokenFlags.Divisible;
            var simulator = new ChainSimulator(owner, 1234);

            string message = "customEvent";
            var addressStr = Base16.Encode(owner.Address.PublicKey);

            scriptString = new string[]
            {
                $"alias r1, $triggerSend",
                $"alias r2, $triggerReceive",
                $"alias r3, $triggerBurn",
                $"alias r4, $triggerMint",
                $"alias r5, $currentTrigger",
                $"alias r6, $comparisonResult",

                $@"load $triggerSend, ""{TokenContract.TriggerSend}""",
                $@"load $triggerReceive, ""{TokenContract.TriggerReceive}""",
                $@"load $triggerBurn, ""{TokenContract.TriggerBurn}""",
                $@"load $triggerMint, ""{TokenContract.TriggerMint}""",
                $"pop $currentTrigger",

                $"equal $triggerSend, $currentTrigger, $comparisonResult",
                $"jmpif $comparisonResult, @sendHandler",

                $"equal $triggerReceive, $currentTrigger, $comparisonResult",
                $"jmpif $comparisonResult, @receiveHandler",

                $"equal $triggerBurn, $currentTrigger, $comparisonResult",
                $"jmpif $comparisonResult, @burnHandler",

                $"equal $triggerMint, $currentTrigger, $comparisonResult",
                $"jmpif $comparisonResult, @mintHandler",

                $"jmp @return",

                $"@sendHandler: throw",

                $"@receiveHandler: throw",

                $"@burnHandler: throw",

                $"@mintHandler: load r11 0x{addressStr}",
                $"push r11",
                $@"extcall ""Address()""",
                $"pop r11",

                $"load r10, {(int)EventKind.Custom}",
                $@"load r12, ""{message}""",

                $"push r10",
                $"push r11",
                $"push r12",
                $@"extcall ""Runtime.Event""",
                "ret",

                $"@return: ret",
            };

            var script = AssemblerUtils.BuildScript(scriptString);

            simulator.BeginBlock();
            simulator.GenerateToken(owner, symbol, $"{symbol}Token", Nexus.PlatformName, Hash.FromString(symbol), 1000000000, 3, flags, script);
            var tx = simulator.MintTokens(owner, symbol, 1000);
            //simulator.GenerateTransfer(owner, target.Address, simulator.Nexus.RootChain, symbol, 10);
            simulator.EndBlock();

            var balance = simulator.Nexus.RootChain.GetTokenBalance(symbol, owner.Address);
            Assert.IsTrue(balance == 1000);

            var events = simulator.Nexus.FindBlockByTransaction(tx).GetEventsForTransaction(tx.Hash);
            Assert.IsTrue(events.Count(x => x.Kind == EventKind.Custom) == 1);

            var eventData = events.First(x => x.Kind == EventKind.Custom).Data;
            var eventMessage = Encoding.UTF8.GetString(eventData);

            Assert.IsTrue(eventMessage == message);

            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateTransfer(owner, target.Address, simulator.Nexus.RootChain, symbol, 10);
                simulator.EndBlock();
            });

            balance = simulator.Nexus.RootChain.GetTokenBalance(symbol, owner.Address);
            Assert.IsTrue(balance == 1000);
        }

        [TestMethod]
        public void AccountTriggers()
        {
            string[] scriptString;

            var owner = KeyPair.Generate();
            var target = KeyPair.Generate();
            var symbol = "debugNFT";
            var flags = TokenFlags.Transferable | TokenFlags.Finite | TokenFlags.Fungible | TokenFlags.Divisible;
            var simulator = new ChainSimulator(owner, 1234);

            scriptString = new string[]
            {
                $"alias r1, $triggerSend",
                $"alias r2, $triggerReceive",
                $"alias r3, $triggerBurn",
                $"alias r4, $triggerMint",
                $"alias r5, $currentTrigger",
                $"alias r6, $comparisonResult",

                $@"load $triggerSend, ""{AccountContract.TriggerSend}""",
                $@"load $triggerReceive, ""{AccountContract.TriggerReceive}""",
                $@"load $triggerBurn, ""{AccountContract.TriggerBurn}""",
                $@"load $triggerMint, ""{AccountContract.TriggerMint}""",
                $"pop $currentTrigger",

                $"equal $triggerSend, $currentTrigger, $comparisonResult",
                $"jmpif $comparisonResult, @sendHandler",

                $"equal $triggerReceive, $currentTrigger, $comparisonResult",
                $"jmpif $comparisonResult, @receiveHandler",

                $"equal $triggerBurn, $currentTrigger, $comparisonResult",
                $"jmpif $comparisonResult, @burnHandler",

                $"equal $triggerMint, $currentTrigger, $comparisonResult",
                $"jmpif $comparisonResult, @mintHandler",

                $"jmp @end",

                $"@sendHandler: throw",

                $"@receiveHandler: throw",

                $"@burnHandler: throw",

                $"@mintHandler: nop",

                $"@end: ret"
            };

            var script = AssemblerUtils.BuildScript(scriptString);

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, target.Address, simulator.Nexus.RootChain, "KCAL", 100000);
            simulator.GenerateCustomTransaction(target,
                () => ScriptUtils.BeginScript().AllowGas(target.Address, Address.Null, 1, 9999)
                    .CallContract("account", "RegisterScript", target.Address, script).SpendGas(target.Address)
                    .EndScript());
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateToken(owner, symbol, $"{symbol}Token", Nexus.PlatformName, Hash.FromString(symbol), 1000000000, 3, flags);
            var tx = simulator.MintTokens(owner, symbol, 1000);
            simulator.EndBlock();

            var balance = simulator.Nexus.RootChain.GetTokenBalance(symbol, owner.Address);
            Assert.IsTrue(balance == 1000);

            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateTransfer(owner, target.Address, simulator.Nexus.RootChain, symbol, 10);
                simulator.EndBlock();
            });

            balance = simulator.Nexus.RootChain.GetTokenBalance(symbol, owner.Address);
            Assert.IsTrue(balance == 1000);
        }

        [TestMethod]
        public void AccountTriggersEventPropagation()
        {
            string[] scriptString;

            var owner = KeyPair.Generate();
            var target = KeyPair.Generate();
            var symbol = "debugNFT";
            var flags = TokenFlags.Transferable | TokenFlags.Finite | TokenFlags.Fungible | TokenFlags.Divisible;
            var simulator = new ChainSimulator(owner, 1234);

            string message = "customEvent";
            var addressStr = Base16.Encode(owner.Address.PublicKey);

            scriptString = new string[]
            {
                $"alias r1, $triggerSend",
                $"alias r2, $triggerReceive",
                $"alias r3, $triggerBurn",
                $"alias r4, $triggerMint",
                $"alias r5, $currentTrigger",
                $"alias r6, $comparisonResult",

                $@"load $triggerSend, ""{AccountContract.TriggerSend}""",
                $@"load $triggerReceive, ""{AccountContract.TriggerReceive}""",
                $@"load $triggerBurn, ""{AccountContract.TriggerBurn}""",
                $@"load $triggerMint, ""{AccountContract.TriggerMint}""",
                $"pop $currentTrigger",

                $"equal $triggerSend, $currentTrigger, $comparisonResult",
                $"jmpif $comparisonResult, @sendHandler",

                $"equal $triggerReceive, $currentTrigger, $comparisonResult",
                $"jmpif $comparisonResult, @receiveHandler",

                $"equal $triggerBurn, $currentTrigger, $comparisonResult",
                $"jmpif $comparisonResult, @burnHandler",

                $"equal $triggerMint, $currentTrigger, $comparisonResult",
                $"jmpif $comparisonResult, @mintHandler",

                $"jmp @end",

                $"@sendHandler: throw",

                $"@receiveHandler: throw",

                $"@burnHandler: throw",

                $"@mintHandler: load r11 0x{addressStr}",
                $"push r11",
                $@"extcall ""Address()""",
                $"pop r11",

                $"load r10, {(int)EventKind.Custom}",
                $@"load r12, ""{message}""",

                $"push r10",
                $"push r11",
                $"push r12",
                $@"extcall ""Runtime.Event""",

                $"@end: ret"
            };

            var script = AssemblerUtils.BuildScript(scriptString);

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, target.Address, simulator.Nexus.RootChain, "KCAL", 100000);
            simulator.GenerateCustomTransaction(target,
                () => ScriptUtils.BeginScript().AllowGas(target.Address, Address.Null, 1, 9999)
                    .CallContract("account", "RegisterScript", target.Address, script).SpendGas(target.Address)
                    .EndScript());
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateToken(target, symbol, $"{symbol}Token", Nexus.PlatformName, Hash.FromString(symbol), 1000000000, 3, flags);
            var tx = simulator.MintTokens(target, symbol, 1000);
            simulator.EndBlock();

            var accountScript = simulator.Nexus.LookUpAddressScript(target.Address);
            Assert.IsTrue(accountScript != null && accountScript.Length > 0);

            var balance = simulator.Nexus.RootChain.GetTokenBalance(symbol, target.Address);
            Assert.IsTrue(balance == 1000);

            var events = simulator.Nexus.FindBlockByTransaction(tx).GetEventsForTransaction(tx.Hash);
            Assert.IsTrue(events.Count(x => x.Kind == EventKind.Custom) == 1);

            var eventData = events.First(x => x.Kind == EventKind.Custom).Data;
            var eventMessage = Encoding.UTF8.GetString(eventData);

            Assert.IsTrue(eventMessage == message);

            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateTransfer(owner, target.Address, simulator.Nexus.RootChain, symbol, 10);
                simulator.EndBlock();
            });

            balance = simulator.Nexus.RootChain.GetTokenBalance(symbol, target.Address);
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

                vm = ExecuteScript(scriptString);

                Assert.IsTrue(vm.Stack.Count == 2);

                var r1obj = vm.Stack.Pop().AsInterop<TestVM.DebugClass>();
                var r2obj = vm.Stack.Pop().AsInterop<TestVM.DebugClass>();

                Assert.IsTrue(ReferenceEquals(r1obj, r2obj));
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

            vm = ExecuteScript(scriptString);

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

            vm = ExecuteScript(scriptString);

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

            vm = ExecuteScript(scriptString);

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

            vm = ExecuteScript(scriptString);

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
                $@"load r1, {initVal}",
                @"call @label",
                @"push r1",
                @"ret",
                $"@label: inc r1",
                $"ret"
            };

            var vm = ExecuteScript(scriptString);

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

                vm = ExecuteScript(scriptString);

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

                vm = ExecuteScript(scriptString);

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

                vm = ExecuteScript(scriptString);

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
            TestVM vm;

            var args = new List<List<bool>>()
            {
                new List<bool>() {true, true},
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                var r1 = argsLine[0];
                var target = argsLine[1];

                scriptString = new string[]
                {
                    $"load r1, {r1}",
                    $"push r1",
                    $"throw",
                    $"not r1, r1",
                    $"pop r2",
                    $"push r1",
                    $"ret"
                };

                vm = ExecuteScript(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsBool();
                Assert.IsTrue(result == target, "Opcode JmpNot isn't working correctly");

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

            var vm = ExecuteScript(scriptString);

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
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
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

                vm = ExecuteScript(scriptString);

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
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
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

                vm = ExecuteScript(scriptString);

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
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
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

                vm = ExecuteScript(scriptString);

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
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
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

                vm = ExecuteScript(scriptString);

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

                vm = ExecuteScript(scriptString);

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
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
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

                vm = ExecuteScript(scriptString);

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
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
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

                vm = ExecuteScript(scriptString);

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
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
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

                vm = ExecuteScript(scriptString);

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
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
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

                vm = ExecuteScript(scriptString);

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
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
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

                vm = ExecuteScript(scriptString);

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
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
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

                vm = ExecuteScript(scriptString);

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
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
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

                vm = ExecuteScript(scriptString);

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
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
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

                vm = ExecuteScript(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $"load r1, \\\"abc\\\"",
                @"abs r1, r2",
                @"push r2",
                @"ret"
            };

            try
            {
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
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

                vm = ExecuteScript(scriptString);

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
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
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

                vm = ExecuteScript(scriptString);

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
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
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

                vm = ExecuteScript(scriptString);

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
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
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

                vm = ExecuteScript(scriptString);

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
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
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

                vm = ExecuteScript(scriptString);

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
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
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

                vm = ExecuteScript(scriptString);

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
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
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

                vm = ExecuteScript(scriptString);

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
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
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

                vm = ExecuteScript(scriptString);

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
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
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

                vm = ExecuteScript(scriptString);

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
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
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

                vm = ExecuteScript(scriptString);

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

                vm = ExecuteScript(scriptString);

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

            var randomKey = KeyPair.Generate();

            var demoValue = new TestInteropStruct()
            {
                ID = 1234,
                name = "monkey",
                address = randomKey.Address
            };

            var hexStr = Base16.Encode(demoValue.address.PublicKey);

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

            vm = ExecuteScript(scriptString, (_vm) =>
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
                        Address obj = new Address((byte[])input);
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

            vm = ExecuteScript(script);

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

                var vm = ExecuteScript(scriptString);

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
                var vm2 = ExecuteScript(scriptString2);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
                return;
            }

            throw new Exception("VM did not throw exception when trying to cat a string and a non-string object, and it should");
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
        

                var vm = ExecuteScript(scriptString);

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
                var vm2 = ExecuteScript(scriptString2);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
                return;
            }

            throw new Exception("VM did not throw exception when trying to cat a string and a non-string object, and it should");
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


                var vm = ExecuteScript(scriptString);

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
                var vm2 = ExecuteScript(scriptString2);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
                return;
            }

            throw new Exception("VM did not throw exception when trying to cat a string and a non-string object, and it should");
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


                var vm = ExecuteScript(scriptString);

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
                var vm2 = ExecuteScript(scriptString2);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
                return;
            }

            throw new Exception("VM did not throw exception when trying to cat a string and a non-string object, and it should");
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

            var calls = DisasmUtils.ExtractMethodCalls(script);

            Assert.IsTrue(calls.Count() == 1);
            Assert.IsTrue(calls.First().MethodName == methodName);
        }
        #endregion

        #region AuxFunctions
        private TestVM ExecuteScript(IEnumerable<string> scriptString, Action<TestVM> beforeExecute = null)
        {
            var owner = KeyPair.Generate();
            var script = AssemblerUtils.BuildScript(scriptString);

            var keys = KeyPair.Generate();
            var nexus = new Nexus(new ConsoleLogger());
            nexus.CreateGenesisBlock("asmnet", owner, Timestamp.Now);
            var tx = new Transaction(nexus.Name, nexus.RootChain.Name, script, 0);

            var vm = new TestVM(tx.Script);
            vm.ThrowOnFault = true;

            beforeExecute?.Invoke(vm);

            vm.Execute();

            return vm;
        }

        private TestVM ExecuteScript(IEnumerable<string> scriptString, out Transaction tx, Action<TestVM> beforeExecute = null)
        {
            var owner = KeyPair.Generate();
            var script = AssemblerUtils.BuildScript(scriptString);

            var keys = KeyPair.Generate();
            var nexus = new Nexus(new ConsoleLogger());
            nexus.CreateGenesisBlock("asmnet", owner, Timestamp.Now);
            tx = new Transaction(nexus.Name, nexus.RootChain.Name, script, 0);

            var vm = new TestVM(tx.Script);
            vm.ThrowOnFault = true;

            beforeExecute?.Invoke(vm);

            vm.Execute();

            return vm;
        }

        #endregion

    }
}
