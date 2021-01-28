using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;

// copied from https://github.com/CityOfZion/neo-plugins-coz/tree/master/LocalDebugger
namespace Phantasma.Neo.Core
{
    public class DisassembleEntry
    {
        public int startOfs;
        public int endOfs;
        public string name;
        public OpCode opcode;
        public byte[] data;
        public string comment;
    }
    
    public struct DisassembleLocation
    {
        public string fileName;
        public int line;
    }
    
    public class DisassembleException : Exception
    {
        public DisassembleException(string msg) : base(msg)
        {
    
        }
    }
    
    public class AVMDisassemble
    {
        private List<DisassembleEntry> _lines;
        public IEnumerable<DisassembleEntry> lines { get { return _lines; } }
    
        public AVMDisassemble(List<DisassembleEntry> lines)
        {
            this._lines = lines;
        }
    
        public int ResolveLine(int ofs)
        {
            int i = 0;
            foreach (var line in lines)
            {
                if (ofs >= line.startOfs && ofs <= line.endOfs)
                {
                    return i;
                }
    
                i++;
            }
    
            throw new Exception("Offset cannot be mapped");
        }
    
        public int ResolveOffset(int line)
        {
            if (line < 0 || line >= _lines.Count)
            {
                throw new Exception("Line cannot be mapped");
            }
    
            return _lines[line].startOfs;
        }
    }
    
    public static class NeoDisassembler
    {
        private static readonly object _lockObj = new object();
        private static Dictionary<OpCode, string> hints = null;
    
        private static void InitHints()
        {

            if (hints != null)
            {
                return;
            }
    
            lock (_lockObj)
            {

                hints = new Dictionary<OpCode, string>();
                // Constants
                hints[OpCode.PUSH0] = "An empty array of bytes is pushed onto the stack.";
                hints[OpCode.PUSHF] = hints[OpCode.PUSH0];
                hints[OpCode.PUSHBYTES1] = /*0x01-0x4B*/ "The next opcode bytes is data to be pushed onto the stack";
                hints[OpCode.PUSHBYTES75] = "";
                hints[OpCode.PUSHDATA1] = "The next byte contains the number of bytes to be pushed onto the stack.";
                hints[OpCode.PUSHDATA2] = "The next two bytes contain the number of bytes to be pushed onto the stack.";
                hints[OpCode.PUSHDATA4] = "The next four bytes contain the number of bytes to be pushed onto the stack.";
                hints[OpCode.PUSHM1] = "The number -1 is pushed onto the stack.";
                hints[OpCode.PUSH1] = "The number 1 is pushed onto the stack.";
                hints[OpCode.PUSHT] = hints[OpCode.PUSH1];
                hints[OpCode.PUSH2] = "The number 2 is pushed onto the stack.";
                hints[OpCode.PUSH3] = "The number 3 is pushed onto the stack.";
                hints[OpCode.PUSH4] = "The number 4 is pushed onto the stack.";
                hints[OpCode.PUSH5] = "The number 5 is pushed onto the stack.";
                hints[OpCode.PUSH6] = "The number 6 is pushed onto the stack.";
                hints[OpCode.PUSH7] = "The number 7 is pushed onto the stack.";
                hints[OpCode.PUSH8] = "The number 8 is pushed onto the stack.";
                hints[OpCode.PUSH9] = "The number 9 is pushed onto the stack.";
                hints[OpCode.PUSH10] = "The number 10 is pushed onto the stack.";
                hints[OpCode.PUSH11] = "The number 11 is pushed onto the stack.";
                hints[OpCode.PUSH12] = "The number 12 is pushed onto the stack.";
                hints[OpCode.PUSH13] = "The number 13 is pushed onto the stack.";
                hints[OpCode.PUSH14] = "The number 14 is pushed onto the stack.";
                hints[OpCode.PUSH15] = "The number 15 is pushed onto the stack.";
                hints[OpCode.PUSH16] = "The number 16 is pushed onto the stack.";
    
    
                // Flow control
                hints[OpCode.NOP] = "Does nothing.";
                hints[OpCode.JMP] = "";
                hints[OpCode.JMPIF] = "";
                hints[OpCode.JMPIFNOT] = "";
                hints[OpCode.CALL] = "";
                hints[OpCode.RET] = "";
                hints[OpCode.APPCALL] = "";
                hints[OpCode.SYSCALL] = "";
                hints[OpCode.TAILCALL] = "";
    
    
                // Stack
                hints[OpCode.DUPFROMALTSTACK] = "";
                hints[OpCode.TOALTSTACK] = "Puts the input onto the top of the alt stack. Removes it from the main stack.";
                hints[OpCode.FROMALTSTACK] = "Puts the input onto the top of the main stack. Removes it from the alt stack.";
                hints[OpCode.XDROP] = "";
                hints[OpCode.XSWAP] = "";
                hints[OpCode.XTUCK] = "";
                hints[OpCode.DEPTH] = "Puts the number of stack items onto the stack.";
                hints[OpCode.DROP] = "Removes the top stack item.";
                hints[OpCode.DUP] = "Duplicates the top stack item.";
                hints[OpCode.NIP] = "Removes the second-to-top stack item.";
                hints[OpCode.OVER] = "Copies the second-to-top stack item to the top.";
                hints[OpCode.PICK] = "The item in back in the stack is copied to the top.";
                hints[OpCode.ROLL] = "The item in back in the stack is moved to the top.";
                hints[OpCode.ROT] = "The top three items on the stack are rotated to the left.";
                hints[OpCode.SWAP] = "The top two items on the stack are swapped.";
                hints[OpCode.TUCK] = "The item at the top of the stack is copied and inserted before the second-to-top item.";
    
    
                // Splice
                hints[OpCode.CAT] = "Concatenates two strings.";
                hints[OpCode.SUBSTR] = "Returns a section of a string.";
                hints[OpCode.LEFT] = "Keeps only characters left of the specified point in a string.";
                hints[OpCode.RIGHT] = "Keeps only characters right of the specified point in a string.";
                hints[OpCode.SIZE] = "Returns the length of the input string.";
    
    
                // Bitwise logic
                hints[OpCode.INVERT] = "Flips all of the bits in the input.";
                hints[OpCode.AND] = "Boolean and between each bit in the inputs.";
                hints[OpCode.OR] = "Boolean or between each bit in the inputs.";
                hints[OpCode.XOR] = "Boolean exclusive or between each bit in the inputs.";
                hints[OpCode.EQUAL] = "Returns 1 if the inputs are exactly equal, 0 otherwise.";
                //OP_EQUALVERIFY = 0x88, // Same as OP_EQUAL, but runs OP_VERIFY afterward.
                //OP_RESERVED1 = 0x89, // Transaction is invalid unless occuring in an unexecuted OP_IF branch
                //OP_RESERVED2 = 0x8A, // Transaction is invalid unless occuring in an unexecuted OP_IF branch
    
    
                // Arithmetic
                // Note: Arithmetic inputs are limited to signed 32-bit integers, but may overflow their output.
                hints[OpCode.INC] = "1 is added to the input.";
                hints[OpCode.DEC] = "1 is subtracted from the input.";
                hints[OpCode.SIGN] = "";
                hints[OpCode.NEGATE] = "The sign of the input is flipped.";
                hints[OpCode.ABS] = "The input is made positive.";
                hints[OpCode.NOT] = "If the input is 0 or 1, it is flipped. Otherwise the output will be 0.";
                hints[OpCode.NZ] = "Returns 0 if the input is 0. 1 otherwise.";
                hints[OpCode.ADD] = "a is added to b.";
                hints[OpCode.SUB] = "b is subtracted from a.";
                hints[OpCode.MUL] = "a is multiplied by b.";
                hints[OpCode.DIV] = "a is divided by b.";
                hints[OpCode.MOD] = "Returns the remainder after dividing a by b.";
                hints[OpCode.SHL] = "Shifts a left b bits, preserving sign.";
                hints[OpCode.SHR] = "Shifts a right b bits, preserving sign.";
                hints[OpCode.BOOLAND] = "If both a and b are not 0, the output is 1. Otherwise 0.";
                hints[OpCode.BOOLOR] = "If a or b is not 0, the output is 1. Otherwise 0.";
                hints[OpCode.NUMEQUAL] = "Returns 1 if the numbers are equal, 0 otherwise.";
                hints[OpCode.NUMNOTEQUAL] = "Returns 1 if the numbers are not equal, 0 otherwise.";
                hints[OpCode.LT] = "Returns 1 if a is less than b, 0 otherwise.";
                hints[OpCode.GT] = "Returns 1 if a is greater than b, 0 otherwise.";
                hints[OpCode.LTE] = "Returns 1 if a is less than or equal to b, 0 otherwise.";
                hints[OpCode.GTE] = "Returns 1 if a is greater than or equal to b, 0 otherwise.";
                hints[OpCode.MIN] = "Returns the smaller of a and b.";
                hints[OpCode.MAX] = "Returns the larger of a and b.";
                hints[OpCode.WITHIN] = "Returns 1 if x is within the specified range (left-inclusive), 0 otherwise.";
    
    
                // Crypto
                //hints[OpCode.RIPEMD160 = 0xA6, // The input is hashed using RIPEMD-160.
                hints[OpCode.SHA1] = ""; // The input is hashed using SHA-1.
                hints[OpCode.SHA256] = ""; // The input is hashed using SHA-256.
                hints[OpCode.HASH160] = "";
                hints[OpCode.HASH256] = "";
                hints[OpCode.CHECKSIG] = "";
                hints[OpCode.CHECKMULTISIG] = "";
    
    
                // Array
                hints[OpCode.ARRAYSIZE] = "";
                hints[OpCode.PACK] = "";
                hints[OpCode.UNPACK] = "";
                hints[OpCode.PICKITEM] = "Pops index and array from stack, then push the item in the array index to the stack";
                hints[OpCode.SETITEM] = "Pops newItem, index and array from stack, then copy the newItem to the array value at index";
                hints[OpCode.NEWARRAY] = "Pops size from stack and creates a new array with that size, and pushes the array into the stack";
                hints[OpCode.NEWSTRUCT] = "用作值類型";
    
    
                // Exceptions
                hints[OpCode.THROW] = "";
                hints[OpCode.THROWIFNOT] = "";
            }
        }
    
        public static byte[] ReadVarBytes(this BinaryReader reader, int max = 0X7fffffc7)
        {
            return reader.ReadBytes((int)reader.ReadVarInt((ulong)max));
        }
    
    
        public static ulong ReadVarInt(this BinaryReader reader, ulong max = ulong.MaxValue)
        {
            byte fb = reader.ReadByte();
            ulong value;
            if (fb == 0xFD)
                value = reader.ReadUInt16();
            else if (fb == 0xFE)
                value = reader.ReadUInt32();
            else if (fb == 0xFF)
                value = reader.ReadUInt64();
            else
                value = fb;
            if (value > max) throw new FormatException();
            return value;
        }
    
        public static AVMDisassemble Disassemble(byte[] script,bool breakAtRet = false)
        {
            InitHints();
            var output = new List<DisassembleEntry>();
    
            using (var stream = new MemoryStream(script))
            {
                using (var reader = new BinaryReader(stream))
                {
                    while (reader.BaseStream.Position != reader.BaseStream.Length)
                    {
                        var entry = new DisassembleEntry();
                        output.Add(entry);
    
                        entry.startOfs = (int)reader.BaseStream.Position;
    
                        var opcode = (OpCode)reader.ReadByte();
    
                        entry.opcode = opcode;
                        entry.name = opcode.ToString();
    
                        entry.comment = hints.ContainsKey(opcode) ? hints[opcode] : "";
    
                        if (opcode >= OpCode.PUSHBYTES1 && opcode <= OpCode.PUSHBYTES75)
                        {
                            var len = (byte)opcode;
                            entry.data = reader.ReadBytes(len);
                            entry.name = "PUSHBYTES" + len;
                            entry.comment = "Pushes " + len + " bytes into the stack: $$";
                        }
                        else
                            switch (opcode)
                            {
                                // Push value
                                //case OpCode.PUSH0:
                                case OpCode.RET:
                                    if(breakAtRet)
                                    {
                                        reader.BaseStream.Position = reader.BaseStream.Length;
                                    }
                                    break;
    
                                case OpCode.PUSHDATA1:
                                    {
                                        var len = reader.ReadByte();
                                        entry.data = reader.ReadBytes(len);
                                        entry.comment = "Pushes " + len + " bytes into the stack: $$";
                                        break;
                                    }
    
                                case OpCode.PUSHDATA2:
                                    {
                                        var len = reader.ReadUInt16();
                                        entry.data = reader.ReadBytes(len);
                                        entry.comment = "Pushes " + len + " bytes into the stack: $$";
                                        break;
                                    }
    
    
                                case OpCode.PUSHDATA4:
                                    {
                                        var len = reader.ReadInt32();
                                        entry.data = reader.ReadBytes(len);
                                        entry.comment = "Pushes " + len + " bytes into the stack: $$";
                                        break;
                                    }
    
                                case OpCode.CALL:
                                case OpCode.CALL_I:
                                    {
                                        int other = reader.ReadInt16();
                                        int offset = reader.ReadInt16();
                                        break;
                                    }
                                case OpCode.CALL_E:
                                case OpCode.CALL_ET:
                                    {
                                        int other = reader.ReadInt16();
                                        byte[] script_hash = reader.ReadBytes(20);
                                        entry.data = script_hash.Reverse().ToArray();
                                        entry.comment = "NEP8-calls script with hash: $XX";
                                        break;
                                    }
    
                                case OpCode.CALL_ED:
                                case OpCode.CALL_EDT:
                                case OpCode.JMP:
                                case OpCode.JMPIF:
                                case OpCode.JMPIFNOT:
                                    {
                                        int offset = reader.ReadInt16();
                                        //offset = context.InstructionPointer + offset - 3;
                                        break;
                                    }
    
                                case OpCode.APPCALL:
                                case OpCode.TAILCALL:
                                    {
                                        byte[] script_hash = reader.ReadBytes(20);
                                        entry.data = script_hash.Reverse().ToArray();
                                        entry.comment = "Calls script with hash: $XX";
                                        break;
                                    }
    
                                case OpCode.SYSCALL:
                                    {
                                        entry.data = reader.ReadVarBytes(252);
                                        entry.comment = "System call with data: $$";
                                        break;
                                    }
    
                                default:
                                    {
                                        /*if (!Enum.IsDefined(typeof(OpCode), opcode))
                                        {
                                            var s = ((byte)opcode).ToString();
                                            Console.WriteLine($"Invalid opcode {s} at {entry.startOfs}");
                                        }*/
    
                                        break;
                                    }
                            }
    
                        entry.endOfs = (int)(reader.BaseStream.Position-1);
                        if(opcode == OpCode.RET && breakAtRet)
                        {
                            break;
                        }

                    }
                }
            }
    
            return new AVMDisassemble(output);
        }
    }
}
