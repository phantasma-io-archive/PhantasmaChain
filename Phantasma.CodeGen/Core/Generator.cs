using System;
using System.Collections.Generic;
using Phantasma.CodeGen.Core.Nodes;
using Phantasma.Numerics;
using Phantasma.VM.Utils;

namespace Phantasma.CodeGen.Core
{
    public class ByteCodeGenerator
    {
        private ModuleNode tree;

        private ScriptBuilder _output = new ScriptBuilder();

        private Dictionary<string, byte> _registerTable = new Dictionary<string, byte>();

        public byte[] Script { get; private set; }
        
        public ByteCodeGenerator(ModuleNode tree, List<Instruction> instructions)
        {
            this.tree = tree;

            /*MethodNode main = FindMethod("Main");

            foreach (var arg in main.arguments)
            {
                stack.Insert(0, arg.decl.identifier);
                important.Add(arg.decl.identifier);
            }*/
            
            foreach (var i in instructions)
            {
                TranslateInstruction(i);
            }

            /*foreach (var i in _output)
            {
                switch (i.Opcode)
                {
                    case Opcode.CALL:
                        {
                            i.data = BitConverter.GetBytes(i.target.offset);
                            break;
                        }

                    case Opcode.JMP:
                    case Opcode.JMPIF:
                    case Opcode.JMPNOT:
                        {
                            short offset = (short)(i.target.offset - i.offset);
                            i.data = BitConverter.GetBytes(offset);
                            break;
                        }
                }
            }*/

            Script = _output.ToScript();
        }

        private MethodNode FindMethod(string name)
        {
            foreach (var entry in tree.classes)
            {
                foreach (var method in entry.methods)
                {
                    if (method.name == name)
                    {
                        return method;
                    }
                }
            }

            return null;
        }

        private byte FetchRegister(string name)
        {
            if (_registerTable.ContainsKey(name))
            {
                return _registerTable[name];
            }

            var register = (byte) _registerTable.Count;
            _registerTable[name] = register;
            return register;
        }

        private void InsertJump(Instruction i, VM.Opcode opcode)
        {
            byte reg;

            // for conditional jumps, fetch the appropriate register for the conditional value
            if (opcode != VM.Opcode.JMP)
            {
                reg = FetchRegister(i.a.target);
            }
            else
            {
                reg = 0;
            }

            _output.EmitJump(opcode, i.b.target, reg);
        }

        private void InsertOp(Instruction i, VM.Opcode Opcode)
        {
            if (i.b != null)
            {
                var a = FetchRegister(i.a.target);
                var b = FetchRegister(i.b.target);
                var dst = FetchRegister(i.target);

                _output.Emit(Opcode, new byte[] { a, b, dst });
            }
            else
            {
                var src = FetchRegister(i.a.target);
                var dst = FetchRegister(i.target);

                _output.Emit(Opcode, new byte[] { src, dst });
            }
        }

        public void TranslateInstruction(Instruction i)
        {
            switch (i.op)
            {
                case Instruction.Opcode.Label:
                    {
                        _output.EmitLabel(i.target);
                        break;
                    }

                case Instruction.Opcode.Push:
                    {
                        var reg = FetchRegister(i.target);
                        _output.EmitPush(reg);
                        break;
                    }

                case Instruction.Opcode.Pop:
                    {
                        var reg = FetchRegister(i.target);
                        _output.Emit(VM.Opcode.POP, new byte[] { reg });
                        break;
                    }

                case Instruction.Opcode.Assign:
                    {
                        if (i.literal != null)
                        {
                            switch (i.literal.kind)
                            {
                                case LiteralKind.String:
                                    {
                                        var reg = FetchRegister(i.target);
                                        _output.EmitLoad(reg, (string)i.literal.value);
                                        break;
                                    }

                                case LiteralKind.Boolean:
                                    {
                                        var reg = FetchRegister(i.target);
                                        _output.EmitLoad(reg, (bool)i.literal.value);
                                        break;

                                    }
                                case LiteralKind.Integer:
                                    {
                                        var reg = FetchRegister(i.target);
                                        BigInteger val;

                                        if (i.literal.value is BigInteger)
                                        {
                                            val = (BigInteger)i.literal.value;
                                        }
                                        else 
                                        if (i.literal.value is int)
                                        {
                                            val = new BigInteger((int)i.literal.value);
                                        }
                                        else
                                        {
                                            throw new Exception($"Could not convert {i.literal.value.GetType().Name} to BigInteger");
                                        }

                                        _output.EmitLoad(reg, val);
                                        break;
                                    }

                                default: throw new Exception("Unsuported " + i.literal.kind);
                            }
                        }
                        else
                        {
                            var src = i.varName != null ? FetchRegister(i.varName) : FetchRegister(i.a.target);
                            var dst = FetchRegister(i.target);
                            _output.EmitMove(src, dst);
                        }
                        break;

                    }

                case Instruction.Opcode.Add: { InsertOp(i, VM.Opcode.ADD); break; }
                case Instruction.Opcode.Sub: { InsertOp(i, VM.Opcode.SUB); break; }
                case Instruction.Opcode.Mul: { InsertOp(i, VM.Opcode.MUL); break; }
                case Instruction.Opcode.Div: { InsertOp(i, VM.Opcode.DIV); break; }
                case Instruction.Opcode.Mod: { InsertOp(i, VM.Opcode.MOD); break; }
                case Instruction.Opcode.Shr: { InsertOp(i, VM.Opcode.SHR); break; }
                case Instruction.Opcode.Shl: { InsertOp(i, VM.Opcode.SHL); break; }
                case Instruction.Opcode.Equals: { InsertOp(i, VM.Opcode.EQUAL); break; }
                case Instruction.Opcode.LessThan: { InsertOp(i, VM.Opcode.LT); break; }
                case Instruction.Opcode.GreaterThan: { InsertOp(i, VM.Opcode.GT); break; }
                case Instruction.Opcode.LessOrEqualThan: { InsertOp(i, VM.Opcode.LTE); break; }
                case Instruction.Opcode.GreaterOrEqualThan: { InsertOp(i, VM.Opcode.GTE); break; }


                case Instruction.Opcode.Jump: InsertJump(i, VM.Opcode.JMP); break;
                case Instruction.Opcode.JumpIfFalse: InsertJump(i, VM.Opcode.JMPNOT); break;
                case Instruction.Opcode.JumpIfTrue: InsertJump(i, VM.Opcode.JMPIF); break;

                case Instruction.Opcode.Call:
                    _output.EmitCall(i.target, 8); // TODO remove hardcoded register count
                    break;

                case Instruction.Opcode.Return:
                    _output.Emit(VM.Opcode.RET);
                    break;

                case Instruction.Opcode.Negate:
                    {
                        var src = FetchRegister(i.a.target);
                        var dst = FetchRegister(i.target);
                        _output.Emit(VM.Opcode.NEGATE, new byte[] { src, dst }); break;
                    }

                case Instruction.Opcode.Not:
                    {
                        var src = FetchRegister(i.a.target);
                        var dst = FetchRegister(i.target);
                        _output.Emit(VM.Opcode.NOT, new byte[] { src, dst }); break;
                    }

                default: throw new Exception("Unsupported Opcode: "+ i.op);
            }
        }

        private string ExportType(string name)
        {
            switch (name.ToLower())
            {
                case "byte[]": return "ByteArray";
                case "uint":
                case "int": return "Integer";
                default: return name;
            }
        }

        /*        private void ExportABI(string name)
                {
                    var root = DataNode.CreateObject();
                    root.AddField("hash", "0xca960c410849c55ed7a172ebc0f14ac8151f3f08");
                    root.AddField("entrypoint", "Main");

                    var functions = DataNode.CreateArray("functions");
                    var events = DataNode.CreateArray("events");

                    foreach (var entry in tree.classes)
                    {
                        foreach (var method in entry.methods)
                        {
                            if (method.visibility != Visibility.Public)
                            {
                                continue;
                            }

                            var node = DataNode.CreateObject();
                            functions.AddNode(node);

                            node.AddField("name", method.name);
                            node.AddField("returntype", ExportType(method.returnType));

                            var args = DataNode.CreateArray("parameters");
                            node.AddNode(args);

                            foreach (var argument in method.arguments)
                            {
                                var arg = DataNode.CreateObject();
                                arg.AddField("name", argument.decl.identifier);
                                arg.AddField("type", ExportType(argument.decl.typeName));
                                args.AddNode(arg);
                            }


                            functions.AddNode(node);
                        }
                    }

                    if (functions.ChildCount > 0)
                    {
                        root.AddNode(functions);
                    }

                    if (events.ChildCount > 0)
                    {
                        root.AddNode(events);
                    }

                    var json = JSONWriter.WriteToString(root);
                    File.WriteAllText(name + ".abi.json", json);
                }

                public void Export(string name)
                {
                    File.WriteAllBytes(name+".avm", _script);

                    ExportABI(name);
                }
                */
    }
}
