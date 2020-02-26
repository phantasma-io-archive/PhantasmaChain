using System.Collections.Generic;
using Phantasma.Core;
using Phantasma.Core.Performance;
using Phantasma.Cryptography;
using System;
using System.Linq;
using System.IO;

namespace Phantasma.VM
{
    public class VMException : Exception
    {
        public VirtualMachine vm;

        public static string Header(string s)
        {
            return $"*********{s}*********";
        }

        public VMException(VirtualMachine vm, string msg) : base(msg)
        {
            this.vm = vm;

            var fileName = vm.GetDumpFileName();
            if (fileName != null)
            {
                DumpToFile(fileName);
            }
        }

        private void DumpToFile(string fileName)
        {
            var temp = new Disassembler(vm.entryScript);

            var lines = new List<string>();

            lines.Add("Exception: "+this.Message);

            if (vm.CurrentContext is ScriptContext sc)
            {
                lines.Add(Header("CURRENT OFFSET"));
                lines.Add(sc.InstructionPointer.ToString());
                lines.Add("");
            }

            lines.Add(Header("STACK"));
            var stack = vm.Stack.ToArray();
            for (int i = 0; i < stack.Length; i++)
            {
                lines.Add(stack[i].ToString());
            }
            lines.Add("");

            lines.Add(Header("FRAMES"));
            int ct = 0;
            var frames = vm.frames.ToArray();
            foreach (var frame in frames)
            {
                if (ct > 0)
                {
                    lines.Add("");
                }

                lines.Add("Active = " + (frame == vm.CurrentFrame).ToString());
                lines.Add("Entry Offset = " + frame.Offset.ToString());
                lines.Add("Registers:");
                int ri = 0;
                foreach (var reg in frame.Registers)
                {
                    if (reg.Type != VMType.None)
                    {
                        lines.Add($"\tR{ri} = {reg}");
                    }

                    ri++;
                }
                ct++;
            }
            lines.Add("");

            var disasm = temp.Instructions.Select(inst => inst.ToString());
            lines.Add(Header("DISASM"));
            lines.AddRange(disasm);
            lines.Add("");

            vm.DumpData(lines);

            var dirName = Directory.GetCurrentDirectory() + "/Dumps/";
            Directory.CreateDirectory(dirName);

            var path = dirName + fileName;
            System.Diagnostics.Debug.WriteLine("Dumped VM data: " + path);
            File.WriteAllLines(path, lines.ToArray());
        }
    }

    public abstract class VirtualMachine
    {
        public const int DefaultRegisterCount = 32; // TODO temp hack, this should be 4
        public const int MaxRegisterCount = 32;

        public bool ThrowOnFault = false;

        public readonly Stack<VMObject> Stack = new Stack<VMObject>();

        public readonly byte[] entryScript;
        public Address EntryAddress { get; protected set; }

        public readonly ExecutionContext entryContext;
        public ExecutionContext CurrentContext { get; protected set; }

        private Dictionary<string, ExecutionContext> _contextMap = new Dictionary<string, ExecutionContext>();

        public readonly Stack<ExecutionFrame> frames = new Stack<ExecutionFrame>();
        public ExecutionFrame CurrentFrame { get; protected set; }

        public VirtualMachine(byte[] script)
        {
            Throw.IfNull(script, nameof(script));

            this.EntryAddress = Address.FromHash(script);
            this.entryContext = new ScriptContext("entry", script);
            RegisterContext("entry", this.entryContext); // TODO this should be a constant

            this.entryScript = script;
        }

        internal void RegisterContext(string contextName, ExecutionContext context)
        {
            _contextMap[contextName] = context;
        }

        public abstract ExecutionState ExecuteInterop(string method);
        public abstract ExecutionContext LoadContext(string contextName);

        public virtual ExecutionState Execute()
        {
            return SwitchContext(entryContext, 0);
        }

        #region FRAMES

        // instructionPointer is the location to jump after the frame is popped!
        internal void PushFrame(ExecutionContext context, uint instructionPointer,  int registerCount)
        {
            var frame = new ExecutionFrame(this, instructionPointer, context, registerCount);
            frames.Push(frame);
            this.CurrentFrame = frame;
        }

        internal uint PopFrame()
        {
            Throw.If(frames.Count < 2, "Not enough frames available");

            frames.Pop();
            var instructionPointer = CurrentFrame.Offset;

            this.CurrentFrame = frames.Peek();
            this.CurrentContext = CurrentFrame.Context;

            return instructionPointer;
        }

        internal ExecutionFrame PeekFrame()
        {
            Throw.If(frames.Count < 2, "Not enough frames available");

            // TODO do this without pop/push
            var temp = frames.Pop();
            var result = frames.Peek();
            frames.Push(temp);

            return result;
        }

        internal ExecutionContext FindContext(string contextName)
        {
            if (_contextMap.ContainsKey(contextName))
            {
                return _contextMap[contextName];
            }

            var result = LoadContext(contextName);
            if (result == null)
            {
                return null;
            }

            _contextMap[contextName] = result;

            return result;
        }

        public virtual ExecutionState ValidateOpcode(Opcode opcode)
        {
            return ExecutionState.Running;
        }

        internal ExecutionState SwitchContext(ExecutionContext context, uint instructionPointer)
        {
            using (var m = new ProfileMarker("SwitchContext"))
            {
                this.CurrentContext = context;
                PushFrame(context, instructionPointer, DefaultRegisterCount);
                return context.Execute(this.CurrentFrame, this.Stack);
            }
        }
        #endregion

        public virtual string GetDumpFileName()
        {
            return "vm.txt";
        }

        public abstract void DumpData(List<string> lines);
    }
}
