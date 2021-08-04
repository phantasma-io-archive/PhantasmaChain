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
using Phantasma.Storage;
using Phantasma.Storage.Utils;
using System.IO;

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

        public TestVM(byte[] script, uint offset) : base(script, offset, null)
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

                contexts.Add(contextName, new ScriptContext("test", byteScript, 0));

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

        [TestMethod]
        public void DecodeStruct()
        {
            var bytes = Base16.Decode("010E04076372656174656405C95AC15F040763726561746F720823220100279FB052FA82D619FB33581321E3A5F592507EAC995907B504876ABF6F62421F0409726F79616C746965730302160004046E616D65041C61736461736461617364617364616173646173646161736461736461040B6465736372697074696F6E041C61736461736461617364617364616173646173646161736461736461040474797065030202000408696D61676555524C04096173646173646173640407696E666F55524C0400040E61747472696275746554797065310400040F61747472696275746556616C7565310400040E61747472696275746554797065320400040F61747472696275746556616C7565320400040E61747472696275746554797065330400040F61747472696275746556616C7565330400");

            var obj = VMObject.FromBytes(bytes);

            Assert.IsTrue(obj.Type == VMType.Struct);
        }

        [TestMethod]
        public void ArrayObjects()
        {
            var array = new BigInteger[] { 10, 50, 120, 250 };

            var obj = VMObject.FromArray(array);

            Assert.IsTrue(obj.Type == VMType.Struct);

            var temp = obj.ToArray<BigInteger>();

            Assert.IsTrue(temp.Length == array.Length);

            for (int i=0; i<array.Length; i++)
            {
                Assert.IsTrue(array[i] == temp[i]);
            }
        }

        public struct TestStruct : ISerializable
        {
            public string Name;
            public BigInteger Number;

            public TestStruct(string name, BigInteger number)
            {
                Name = name;
                Number = number;
            }

            public void SerializeData(BinaryWriter writer)
            {
                writer.Write(Name);
                writer.WriteBigInteger(Number);
                //writer.Close();
            }

            public void UnserializeData(BinaryReader reader)
            {
                Name = reader.ReadString();
                Number = reader.ReadBigInteger();
                //reader.Close();
            }
        }

        [TestMethod]
        public void EncodeDecodeStruct()
        {
            BigInteger number = 123;
            string name = "my_test";

            TestStruct test = new TestStruct(name, number);

            Assert.IsTrue(test.Name == name);
            Assert.IsTrue(test.Number == number);

            var vmStruct = VMObject.FromStruct(test);

            var backFromStruct = vmStruct.AsStruct<TestStruct>();

            Assert.IsTrue(backFromStruct.Name == name);
            Assert.IsTrue(backFromStruct.Number == number);

            var vmSerialize = vmStruct.Serialize();
            var backFromSerialize = VMObject.FromBytes(vmSerialize);
            var backToStruct = backFromSerialize.AsStruct<TestStruct>();

            Assert.IsTrue(backToStruct.Name == name);
            Assert.IsTrue(backToStruct.Number == number);

        }


        public struct TestAddressStruct : ISerializable
        {
            public string Name;
            public Address Owner;

            public TestAddressStruct(string Name, Address Owner)
            {
                this.Name = Name;
                this.Owner = Owner;
            }

            public void SerializeData(BinaryWriter writer)
            {
                writer.Write(Name);
                writer.Write(Owner.Text);
                //writer.Close();
            }

            public void UnserializeData(BinaryReader reader)
            {
                Name = reader.ReadString();
                Owner = Address.FromText(reader.ReadString());
                //reader.Close();
            }
        }

        [TestMethod]
        public void EncodeWithAddress()
        {
            Address addr = Address.FromText("P2K6Sm1bUYGsFkxuzHPhia1AbANZaHBJV54RgtQi5q8oK34");
            string name = "my_test";

            TestAddressStruct test = new TestAddressStruct(name, addr);

            Assert.IsTrue(test.Name == name);
            Assert.IsTrue(test.Owner == addr);

            var vmStruct = VMObject.FromStruct(test);

            var backFromStruct = vmStruct.AsStruct<TestAddressStruct>();

            Assert.IsTrue(backFromStruct.Name == name);
            Assert.IsTrue(backFromStruct.Owner == addr);

            var vmSerialize = vmStruct.Serialize();
            var backFromSerialize = VMObject.FromBytes(vmSerialize);
            var backToStruct = backFromSerialize.AsStruct<TestAddressStruct>();

            Assert.IsTrue(backToStruct.Name == name);
            Assert.IsTrue(backToStruct.Owner == addr);
        }


        public struct MyMultiStruct : ISerializable
        {
            public TestAddressStruct One;
            public TestStruct Two;
            public bool Three;

            public MyMultiStruct(TestAddressStruct One, TestStruct Two, bool Three = false)
            {
                this.One = One;
                this.Two = Two;
                this.Three = Three;
            }

            public void SerializeData(BinaryWriter writer)
            {
                One.SerializeData(writer);
                Two.SerializeData(writer);
                writer.Write(Three);
                writer.Close();
            }

            public void UnserializeData(BinaryReader reader)
            {
                One.UnserializeData(reader);
                Two.UnserializeData(reader);
                Three = reader.ReadBoolean();
                reader.Close();
            }
        }

        [TestMethod]
        public void EncodeDecodeWithMultipleStructures()
        {
            Address addr = Address.FromText("P2K6Sm1bUYGsFkxuzHPhia1AbANZaHBJV54RgtQi5q8oK34");
            string name_2 = "my_test_2";
            TestAddressStruct One = new TestAddressStruct(name_2, addr);

            BigInteger number = 123;
            string name = "my_test";
            TestStruct Two = new TestStruct(name, number);

            bool Three = true;

            MyMultiStruct multi = new MyMultiStruct(One, Two, Three);

            // Test One
            Assert.IsTrue(One.Name == name_2);
            Assert.IsTrue(One.Owner == addr);

            var vmStruct = VMObject.FromStruct(One);

            var backFromStruct = vmStruct.AsStruct<TestAddressStruct>();

            Assert.IsTrue(backFromStruct.Name == name_2);
            Assert.IsTrue(backFromStruct.Owner == addr);

            var vmSerialize = vmStruct.Serialize();
            var backFromSerialize = VMObject.FromBytes(vmSerialize);
            var backToStruct = backFromSerialize.AsStruct<TestAddressStruct>();

            Assert.IsTrue(backToStruct.Name == name_2);
            Assert.IsTrue(backToStruct.Owner == addr);

            // Test Two
            Assert.IsTrue(Two.Name == name);
            Assert.IsTrue(Two.Number == number);

            var vmStruct_Two = VMObject.FromStruct(Two);

            var backFromStruct_Two = vmStruct_Two.AsStruct<TestStruct>();

            Assert.IsTrue(backFromStruct_Two.Name == name);
            Assert.IsTrue(backFromStruct_Two.Number == number);

            var vmSerialize_Two = vmStruct_Two.Serialize();
            var backFromSerialize_Two = VMObject.FromBytes(vmSerialize_Two);
            var backToStruct_Two = backFromSerialize_Two.AsStruct<TestStruct>();

            Assert.IsTrue(backToStruct_Two.Name == name);
            Assert.IsTrue(backToStruct_Two.Number == number);

            // Test Multi
            Assert.IsTrue(multi.One.Name == One.Name);
            Assert.IsTrue(multi.One.Owner == One.Owner);
            Assert.IsTrue(multi.Two.Name == Two.Name);
            Assert.IsTrue(multi.Two.Number == Two.Number);
            Assert.IsTrue(multi.Three == Three);


            var vmStruct_multi = VMObject.FromStruct(multi);
            var backFromStruct_multi = vmStruct_multi.AsStruct<MyMultiStruct>();

            Assert.IsTrue(backFromStruct_multi.One.Name == One.Name);
            Assert.IsTrue(backFromStruct_multi.One.Owner == One.Owner);
            Assert.IsTrue(backFromStruct_multi.Two.Name == Two.Name);
            Assert.IsTrue(backFromStruct_multi.Two.Number == Two.Number);
            Assert.IsTrue(backFromStruct_multi.Three == Three);

            var vmSerialize_multi = vmStruct_multi.Serialize();
            var backFromSerialize_multi = VMObject.FromBytes(vmSerialize_multi);
            var backToStruct_multi = backFromSerialize_multi.AsStruct<MyMultiStruct>();

            Assert.IsTrue(backToStruct_multi.One.Name == One.Name);
            Assert.IsTrue(backToStruct_multi.One.Owner == One.Owner);
            Assert.IsTrue(backToStruct_multi.Two.Name == Two.Name);
            Assert.IsTrue(backToStruct_multi.Two.Number == Two.Number);
            Assert.IsTrue(backToStruct_multi.Three == Three);
        }

        // Test Specific cases
        public enum CharacterState
        {
            None = 0,
            Ingame = 1,
            Battle = 2,
            Team = 3
        }

        public enum ElementType
        {
            Normal = 0,
            Fire = 1,
            Poison = 2,
            Water = 3,
            Grass = 4,
            Steel = 5,
            Eletric = 6,
            Wind = 7
        }

        public struct CharacterROM
        {
            public string Name;
            public BigInteger Seed;
            public BigInteger Health;
            public BigInteger Mana;
            public BigInteger Attack;
            public BigInteger Defense;
            public BigInteger Speed;
            public ElementType Element;

            public CharacterROM(string Name, BigInteger Seed, BigInteger Health, BigInteger Mana, BigInteger Attack, BigInteger Defense, BigInteger Speed, ElementType Element)
            {
                this.Name = Name;
                this.Seed = Seed;
                this.Health = Health;
                this.Mana = Mana;
                this.Attack = Attack;
                this.Defense = Defense;
                this.Speed = Speed;
                this.Element = Element;
            }
        }

        public struct CharacterRAM
        {
            public BigInteger XP;
            public BigInteger Level;
            public CharacterState State;

            public CharacterRAM(BigInteger XP, BigInteger Level, CharacterState State)
            {
                this.XP = XP;
                this.Level = Level;
                this.State = State;
            }
        }

        public struct CharacterSetup
        {
            public CharacterROM Rom;
            public CharacterRAM Ram;
            public BigInteger IsBot;

            public CharacterSetup(CharacterROM Rom, CharacterRAM Ram, BigInteger IsBot)
            {
                this.Rom = Rom;
                this.Ram = Ram;
                this.IsBot = IsBot;
            }
        }

        public struct Team
        {
            public BigInteger TeamID;
            public Address Player;
            public CharacterSetup Character1;
            public CharacterSetup Character2;
            public CharacterSetup Character3;
            
            public Team(BigInteger TeamID, Address Player, CharacterSetup Character1, CharacterSetup Character2, CharacterSetup Character3)
            {
                this.TeamID = TeamID;
                this.Player = Player;
                this.Character1 = Character1;
                this.Character2 = Character2;
                this.Character3 = Character3;
            }
        }

        [TestMethod]
        public void TestTeam()
        {
            var name = "test";
            var seed = 0;
            var health = 0;
            var mana = 0;
            var attack = 0;
            var defense = 0;
            var speed = 0;
            var element = ElementType.Normal;
            var xp = 0;
            var level = 1;
            var state = CharacterState.Team;
            var isBot = 0;
            var teamID = 0;

            var rom = new CharacterROM(name, seed, health, mana, attack, defense, speed, element);
            var ram = new CharacterRAM(xp, level, state);
            var cSetup = new CharacterSetup(rom, ram, isBot);
            var team = new Team(teamID, Address.Null, cSetup, cSetup, cSetup);

            // Test ROM
            Assert.IsTrue(rom.Name == name);
            Assert.IsTrue(rom.Seed == seed);
            Assert.IsTrue(rom.Health == health);
            Assert.IsTrue(rom.Mana == mana);
            Assert.IsTrue(rom.Attack == attack);
            Assert.IsTrue(rom.Defense == defense);
            Assert.IsTrue(rom.Speed == speed);
            Assert.IsTrue(rom.Element == element);

            var romVMStruct = VMObject.FromStruct(rom);
            var romBackFromStruct = romVMStruct.AsStruct<CharacterROM>();

            Assert.IsTrue(romBackFromStruct.Name == name);
            Assert.IsTrue(romBackFromStruct.Seed == seed);
            Assert.IsTrue(romBackFromStruct.Health == health);
            Assert.IsTrue(romBackFromStruct.Mana == mana);
            Assert.IsTrue(romBackFromStruct.Attack == attack);
            Assert.IsTrue(romBackFromStruct.Defense == defense);
            Assert.IsTrue(romBackFromStruct.Speed == speed);
            Assert.IsTrue(romBackFromStruct.Element == element);

            var romVMSerialize = romVMStruct.Serialize();
            var romBackFromSerialize = VMObject.FromBytes(romVMSerialize);
            var romBackToStruct = romBackFromSerialize.AsStruct<CharacterROM>();

            Assert.IsTrue(romBackToStruct.Name == name);
            Assert.IsTrue(romBackToStruct.Seed == seed);
            Assert.IsTrue(romBackToStruct.Health == health);
            Assert.IsTrue(romBackToStruct.Mana == mana);
            Assert.IsTrue(romBackToStruct.Attack == attack);
            Assert.IsTrue(romBackToStruct.Defense == defense);
            Assert.IsTrue(romBackToStruct.Speed == speed);
            Assert.IsTrue(romBackToStruct.Element == element);

            // Test RAM
            Assert.IsTrue(ram.XP == xp);
            Assert.IsTrue(ram.Level == level);
            Assert.IsTrue(ram.State == state);

            var ramVMStruct = VMObject.FromStruct(ram);
            var ramBackFromStruct = ramVMStruct.AsStruct<CharacterRAM>();

            Assert.IsTrue(ramBackFromStruct.XP == xp);
            Assert.IsTrue(ramBackFromStruct.Level == level);
            Assert.IsTrue(ramBackFromStruct.State == state);

            var ramVMSerialize = ramVMStruct.Serialize();
            var ramBackFromSerialize = VMObject.FromBytes(ramVMSerialize);
            var ramBackToStruct = ramBackFromSerialize.AsStruct<CharacterRAM>();

            Assert.IsTrue(ramBackToStruct.XP == xp);
            Assert.IsTrue(ramBackToStruct.Level == level);
            Assert.IsTrue(ramBackToStruct.State == state);

            // Test Character
            Assert.IsTrue(cSetup.IsBot == isBot);
            Assert.IsTrue(cSetup.Rom.Name == name);
            Assert.IsTrue(cSetup.Rom.Seed == seed);
            Assert.IsTrue(cSetup.Rom.Health == health);
            Assert.IsTrue(cSetup.Rom.Mana == mana);
            Assert.IsTrue(cSetup.Rom.Attack == attack);
            Assert.IsTrue(cSetup.Rom.Defense == defense);
            Assert.IsTrue(cSetup.Rom.Speed == speed);
            Assert.IsTrue(cSetup.Rom.Element == element);
            Assert.IsTrue(cSetup.Ram.XP == xp);
            Assert.IsTrue(cSetup.Ram.Level == level);
            Assert.IsTrue(cSetup.Ram.State == state);


            var characterVMStruct = VMObject.FromStruct(cSetup);
            var characterBackFromStruct = characterVMStruct.AsStruct<CharacterSetup>();

            Assert.IsTrue(characterBackFromStruct.IsBot == isBot);
            Assert.IsTrue(characterBackFromStruct.Rom.Name == name);
            Assert.IsTrue(characterBackFromStruct.Rom.Seed == seed);
            Assert.IsTrue(characterBackFromStruct.Rom.Health == health);
            Assert.IsTrue(characterBackFromStruct.Rom.Mana == mana);
            Assert.IsTrue(characterBackFromStruct.Rom.Attack == attack);
            Assert.IsTrue(characterBackFromStruct.Rom.Defense == defense);
            Assert.IsTrue(characterBackFromStruct.Rom.Speed == speed);
            Assert.IsTrue(characterBackFromStruct.Rom.Element == element);
            Assert.IsTrue(characterBackFromStruct.Ram.XP == xp);
            Assert.IsTrue(characterBackFromStruct.Ram.Level == level);
            Assert.IsTrue(characterBackFromStruct.Ram.State == state);


            var characterVMSerialize = characterVMStruct.Serialize();
            var characterBackFromSerialize = VMObject.FromBytes(characterVMSerialize);
            var characterBackToStruct = characterBackFromSerialize.AsStruct<CharacterSetup>();

            Assert.IsTrue(characterBackToStruct.IsBot == isBot);
            Assert.IsTrue(characterBackToStruct.Rom.Name == name);
            Assert.IsTrue(characterBackToStruct.Rom.Seed == seed);
            Assert.IsTrue(characterBackToStruct.Rom.Health == health);
            Assert.IsTrue(characterBackToStruct.Rom.Mana == mana);
            Assert.IsTrue(characterBackToStruct.Rom.Attack == attack);
            Assert.IsTrue(characterBackToStruct.Rom.Defense == defense);
            Assert.IsTrue(characterBackToStruct.Rom.Speed == speed);
            Assert.IsTrue(characterBackToStruct.Rom.Element == element);
            Assert.IsTrue(characterBackToStruct.Ram.XP == xp);
            Assert.IsTrue(characterBackToStruct.Ram.Level == level);
            Assert.IsTrue(characterBackToStruct.Ram.State == state);

            // Test Team
            Assert.IsTrue(team.TeamID == teamID);
            Assert.IsTrue(team.Player == Address.Null);
            Assert.IsTrue(team.Character1.IsBot == isBot);
            Assert.IsTrue(team.Character1.Rom.Name == name);
            Assert.IsTrue(team.Character1.Rom.Seed == seed);
            Assert.IsTrue(team.Character1.Rom.Health == health);
            Assert.IsTrue(team.Character1.Rom.Mana == mana);
            Assert.IsTrue(team.Character1.Rom.Attack == attack);
            Assert.IsTrue(team.Character1.Rom.Defense == defense);
            Assert.IsTrue(team.Character1.Rom.Speed == speed);
            Assert.IsTrue(team.Character1.Rom.Element == element);
            Assert.IsTrue(team.Character1.Ram.XP == xp);
            Assert.IsTrue(team.Character1.Ram.Level == level);
            Assert.IsTrue(team.Character1.Ram.State == state);
            Assert.IsTrue(team.Character2.IsBot == isBot);
            Assert.IsTrue(team.Character2.Rom.Name == name);
            Assert.IsTrue(team.Character2.Rom.Seed == seed);
            Assert.IsTrue(team.Character2.Rom.Health == health);
            Assert.IsTrue(team.Character2.Rom.Mana == mana);
            Assert.IsTrue(team.Character2.Rom.Attack == attack);
            Assert.IsTrue(team.Character2.Rom.Defense == defense);
            Assert.IsTrue(team.Character2.Rom.Speed == speed);
            Assert.IsTrue(team.Character2.Rom.Element == element);
            Assert.IsTrue(team.Character2.Ram.XP == xp);
            Assert.IsTrue(team.Character2.Ram.Level == level);
            Assert.IsTrue(team.Character2.Ram.State == state);
            Assert.IsTrue(team.Character3.IsBot == isBot);
            Assert.IsTrue(team.Character3.Rom.Name == name);
            Assert.IsTrue(team.Character3.Rom.Seed == seed);
            Assert.IsTrue(team.Character3.Rom.Health == health);
            Assert.IsTrue(team.Character3.Rom.Mana == mana);
            Assert.IsTrue(team.Character3.Rom.Attack == attack);
            Assert.IsTrue(team.Character3.Rom.Defense == defense);
            Assert.IsTrue(team.Character3.Rom.Speed == speed);
            Assert.IsTrue(team.Character3.Rom.Element == element);
            Assert.IsTrue(team.Character3.Ram.XP == xp);
            Assert.IsTrue(team.Character3.Ram.Level == level);
            Assert.IsTrue(team.Character3.Ram.State == state);

            var teamVMStruct = VMObject.FromStruct(team);
            var teamBackFromStruct = teamVMStruct.AsStruct<Team>();

            Assert.IsTrue(teamBackFromStruct.TeamID == teamID);
            Assert.IsTrue(teamBackFromStruct.Player == Address.Null);
            Assert.IsTrue(teamBackFromStruct.Character1.IsBot == isBot);
            Assert.IsTrue(teamBackFromStruct.Character1.Rom.Name == name);
            Assert.IsTrue(teamBackFromStruct.Character1.Rom.Seed == seed);
            Assert.IsTrue(teamBackFromStruct.Character1.Rom.Health == health);
            Assert.IsTrue(teamBackFromStruct.Character1.Rom.Mana == mana);
            Assert.IsTrue(teamBackFromStruct.Character1.Rom.Attack == attack);
            Assert.IsTrue(teamBackFromStruct.Character1.Rom.Defense == defense);
            Assert.IsTrue(teamBackFromStruct.Character1.Rom.Speed == speed);
            Assert.IsTrue(teamBackFromStruct.Character1.Rom.Element == element);
            Assert.IsTrue(teamBackFromStruct.Character1.Ram.XP == xp);
            Assert.IsTrue(teamBackFromStruct.Character1.Ram.Level == level);
            Assert.IsTrue(teamBackFromStruct.Character1.Ram.State == state);
            Assert.IsTrue(teamBackFromStruct.Character2.IsBot == isBot);
            Assert.IsTrue(teamBackFromStruct.Character2.Rom.Name == name);
            Assert.IsTrue(teamBackFromStruct.Character2.Rom.Seed == seed);
            Assert.IsTrue(teamBackFromStruct.Character2.Rom.Health == health);
            Assert.IsTrue(teamBackFromStruct.Character2.Rom.Mana == mana);
            Assert.IsTrue(teamBackFromStruct.Character2.Rom.Attack == attack);
            Assert.IsTrue(teamBackFromStruct.Character2.Rom.Defense == defense);
            Assert.IsTrue(teamBackFromStruct.Character2.Rom.Speed == speed);
            Assert.IsTrue(teamBackFromStruct.Character2.Rom.Element == element);
            Assert.IsTrue(teamBackFromStruct.Character2.Ram.XP == xp);
            Assert.IsTrue(teamBackFromStruct.Character2.Ram.Level == level);
            Assert.IsTrue(teamBackFromStruct.Character2.Ram.State == state);
            Assert.IsTrue(teamBackFromStruct.Character3.IsBot == isBot);
            Assert.IsTrue(teamBackFromStruct.Character3.Rom.Name == name);
            Assert.IsTrue(teamBackFromStruct.Character3.Rom.Seed == seed);
            Assert.IsTrue(teamBackFromStruct.Character3.Rom.Health == health);
            Assert.IsTrue(teamBackFromStruct.Character3.Rom.Mana == mana);
            Assert.IsTrue(teamBackFromStruct.Character3.Rom.Attack == attack);
            Assert.IsTrue(teamBackFromStruct.Character3.Rom.Defense == defense);
            Assert.IsTrue(teamBackFromStruct.Character3.Rom.Speed == speed);
            Assert.IsTrue(teamBackFromStruct.Character3.Rom.Element == element);
            Assert.IsTrue(teamBackFromStruct.Character3.Ram.XP == xp);
            Assert.IsTrue(teamBackFromStruct.Character3.Ram.Level == level);
            Assert.IsTrue(teamBackFromStruct.Character3.Ram.State == state);


            var teamVMSerialize = teamVMStruct.Serialize();
            var teamBackFromSerialize = VMObject.FromBytes(teamVMSerialize);
            var teamBackToStruct = teamBackFromSerialize.AsStruct<Team>();

            Assert.IsTrue(teamBackToStruct.TeamID == teamID);
            Assert.IsTrue(teamBackToStruct.Player == Address.Null);
            Assert.IsTrue(teamBackToStruct.Character1.IsBot == isBot);
            Assert.IsTrue(teamBackToStruct.Character1.Rom.Name == name);
            Assert.IsTrue(teamBackToStruct.Character1.Rom.Seed == seed);
            Assert.IsTrue(teamBackToStruct.Character1.Rom.Health == health);
            Assert.IsTrue(teamBackToStruct.Character1.Rom.Mana == mana);
            Assert.IsTrue(teamBackToStruct.Character1.Rom.Attack == attack);
            Assert.IsTrue(teamBackToStruct.Character1.Rom.Defense == defense);
            Assert.IsTrue(teamBackToStruct.Character1.Rom.Speed == speed);
            Assert.IsTrue(teamBackToStruct.Character1.Rom.Element == element);
            Assert.IsTrue(teamBackToStruct.Character1.Ram.XP == xp);
            Assert.IsTrue(teamBackToStruct.Character1.Ram.Level == level);
            Assert.IsTrue(teamBackToStruct.Character1.Ram.State == state);
            Assert.IsTrue(teamBackToStruct.Character2.IsBot == isBot);
            Assert.IsTrue(teamBackToStruct.Character2.Rom.Name == name);
            Assert.IsTrue(teamBackToStruct.Character2.Rom.Seed == seed);
            Assert.IsTrue(teamBackToStruct.Character2.Rom.Health == health);
            Assert.IsTrue(teamBackToStruct.Character2.Rom.Mana == mana);
            Assert.IsTrue(teamBackToStruct.Character2.Rom.Attack == attack);
            Assert.IsTrue(teamBackToStruct.Character2.Rom.Defense == defense);
            Assert.IsTrue(teamBackToStruct.Character2.Rom.Speed == speed);
            Assert.IsTrue(teamBackToStruct.Character2.Rom.Element == element);
            Assert.IsTrue(teamBackToStruct.Character2.Ram.XP == xp);
            Assert.IsTrue(teamBackToStruct.Character2.Ram.Level == level);
            Assert.IsTrue(teamBackToStruct.Character2.Ram.State == state);
            Assert.IsTrue(teamBackToStruct.Character3.IsBot == isBot);
            Assert.IsTrue(teamBackToStruct.Character3.Rom.Name == name);
            Assert.IsTrue(teamBackToStruct.Character3.Rom.Seed == seed);
            Assert.IsTrue(teamBackToStruct.Character3.Rom.Health == health);
            Assert.IsTrue(teamBackToStruct.Character3.Rom.Mana == mana);
            Assert.IsTrue(teamBackToStruct.Character3.Rom.Attack == attack);
            Assert.IsTrue(teamBackToStruct.Character3.Rom.Defense == defense);
            Assert.IsTrue(teamBackToStruct.Character3.Rom.Speed == speed);
            Assert.IsTrue(teamBackToStruct.Character3.Rom.Element == element);
            Assert.IsTrue(teamBackToStruct.Character3.Ram.XP == xp);
            Assert.IsTrue(teamBackToStruct.Character3.Ram.Level == level);
            Assert.IsTrue(teamBackToStruct.Character3.Ram.State == state);

            // Encode With Base16 and decode
            var teamEncode = Base16.Encode(VMObject.FromStruct(team).Serialize());
            var teamBytes = Base16.Decode(teamEncode);
            var teamDecode = VMObject.FromBytes(teamBytes).AsStruct<Team>();

            Assert.IsTrue(teamDecode.TeamID == teamID);
            Assert.IsTrue(teamDecode.Player == Address.Null);
            Assert.IsTrue(teamDecode.Character1.IsBot == isBot);
            Assert.IsTrue(teamDecode.Character1.Rom.Name == name);
            Assert.IsTrue(teamDecode.Character1.Rom.Seed == seed);
            Assert.IsTrue(teamDecode.Character1.Rom.Health == health);
            Assert.IsTrue(teamDecode.Character1.Rom.Mana == mana);
            Assert.IsTrue(teamDecode.Character1.Rom.Attack == attack);
            Assert.IsTrue(teamDecode.Character1.Rom.Defense == defense);
            Assert.IsTrue(teamDecode.Character1.Rom.Speed == speed);
            Assert.IsTrue(teamDecode.Character1.Rom.Element == element);
            Assert.IsTrue(teamDecode.Character1.Ram.XP == xp);
            Assert.IsTrue(teamDecode.Character1.Ram.Level == level);
            Assert.IsTrue(teamDecode.Character1.Ram.State == state);
            Assert.IsTrue(teamDecode.Character2.IsBot == isBot);
            Assert.IsTrue(teamDecode.Character2.Rom.Name == name);
            Assert.IsTrue(teamDecode.Character2.Rom.Seed == seed);
            Assert.IsTrue(teamDecode.Character2.Rom.Health == health);
            Assert.IsTrue(teamDecode.Character2.Rom.Mana == mana);
            Assert.IsTrue(teamDecode.Character2.Rom.Attack == attack);
            Assert.IsTrue(teamDecode.Character2.Rom.Defense == defense);
            Assert.IsTrue(teamDecode.Character2.Rom.Speed == speed);
            Assert.IsTrue(teamDecode.Character2.Rom.Element == element);
            Assert.IsTrue(teamDecode.Character2.Ram.XP == xp);
            Assert.IsTrue(teamDecode.Character2.Ram.Level == level);
            Assert.IsTrue(teamDecode.Character2.Ram.State == state);
            Assert.IsTrue(teamDecode.Character3.IsBot == isBot);
            Assert.IsTrue(teamDecode.Character3.Rom.Name == name);
            Assert.IsTrue(teamDecode.Character3.Rom.Seed == seed);
            Assert.IsTrue(teamDecode.Character3.Rom.Health == health);
            Assert.IsTrue(teamDecode.Character3.Rom.Mana == mana);
            Assert.IsTrue(teamDecode.Character3.Rom.Attack == attack);
            Assert.IsTrue(teamDecode.Character3.Rom.Defense == defense);
            Assert.IsTrue(teamDecode.Character3.Rom.Speed == speed);
            Assert.IsTrue(teamDecode.Character3.Rom.Element == element);
            Assert.IsTrue(teamDecode.Character3.Ram.XP == xp);
            Assert.IsTrue(teamDecode.Character3.Ram.Level == level);
            Assert.IsTrue(teamDecode.Character3.Ram.State == state);
        }
    }
}
