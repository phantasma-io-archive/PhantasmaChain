using System.Collections.Generic;
using Phantasma.Numerics;
using Phantasma.Core;
using Phantasma.Cryptography;
#if DEBUG
using System;
using System.Linq;
using System.IO;
#endif

namespace Phantasma.VM
{
#if DEBUG
    public class VMDebugException : Exception
    {
        public ExecutionFrame frame;
        public Stack<VMObject> stack;

        public VMDebugException(ExecutionFrame frame, Stack<VMObject> stack, string msg) : base(msg)
        {
            this.frame = frame;
            this.stack = stack;
            var temp = new Disassembler(frame.VM.entryScript);
            var disasm = string.Join("\n", temp.Instructions.Select(inst => inst.ToString()));
            File.WriteAllText("vm_dump.txt", disasm);
        }
    }
#endif

    public abstract class VirtualMachine
    {
        public const int DefaultRegisterCount = 32; // TODO temp hack, this should be 4
        public const int MaxRegisterCount = 32;

        public readonly Stack<VMObject> Stack = new Stack<VMObject>();

        public readonly byte[] entryScript;
        public Address entryAddress { get; private set; }

        public readonly ExecutionContext entryContext;
        public ExecutionContext currentContext { get; private set; }

        private Dictionary<string, ExecutionContext> _contextList = new Dictionary<string, ExecutionContext>();

        public readonly Stack<ExecutionFrame> frames = new Stack<ExecutionFrame>();
        public ExecutionFrame currentFrame { get; private set; }

        public BigInteger usedGas { get; internal set; }
        public abstract BigInteger gasLimit { get; }

        public VirtualMachine(byte[] script)
        {
            Throw.IfNull(script, nameof(script));

            this.entryAddress = Address.FromScript(script);
            this.entryContext = new ScriptContext(script);
            RegisterContext("entry", this.entryContext); // TODO this should be a constant

            this.usedGas = 0;
            this.entryScript = script;
        }

        internal void RegisterContext(string contextName, ExecutionContext context)
        {
            _contextList[contextName] = context;
        }

        public abstract ExecutionState ExecuteInterop(string method);
        public abstract ExecutionContext LoadContext(string contextName);

        public ExecutionState Execute()
        {
            return SwitchContext(entryContext);
        }

        #region FRAMES
        internal void PushFrame(ExecutionContext context, uint instructionPointer,  int registerCount)
        {
            var frame = new ExecutionFrame(this, instructionPointer, context, registerCount);
            frames.Push(frame);
            this.currentFrame = frame;
        }

        internal uint PopFrame()
        {
            Throw.If(frames.Count < 2, "Not enough frames available");

            frames.Pop();
            var instructionPointer = currentFrame.Offset;

            this.currentFrame = frames.Peek();
            this.currentContext = currentFrame.Context;

            return instructionPointer;
        }

        internal ExecutionContext FindContext(string contextName)
        {
            if (_contextList.ContainsKey(contextName))
            {
                return _contextList[contextName];
            }

            var result = LoadContext(contextName);
            if (result == null)
            {
                return null;
            }

            _contextList[contextName] = result;

            return result;
        }

        internal ExecutionState SwitchContext(ExecutionContext context)
        {
            this.currentContext = context;
            PushFrame(context, 0, DefaultRegisterCount);
            return context.Execute(this.currentFrame, this.Stack);
        }

        #endregion

#if DEBUG
        public virtual ExecutionState HandleException(VMDebugException ex)
        {
            throw ex;
        }
#endif
    }
}
