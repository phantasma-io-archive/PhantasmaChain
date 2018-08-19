using System;
using System.Collections.Generic;
using Phantasma.Mathematics;
using Phantasma.Utils;
using Phantasma.VM.Types;

namespace Phantasma.VM
{
    public abstract class VirtualMachine
    {
        public const int DefaultRegisterCount = 4;
        public const int MaxRegisterCount = 32;

        public readonly Stack<VMObject> stack = new Stack<VMObject>();

        public readonly byte[] entryScript;
        public Address entryAddress { get; private set; }

        public readonly ExecutionContext entryContext;
        public ExecutionContext currentContext { get; private set; }

        private Dictionary<Address, ExecutionContext> _contextList = new Dictionary<Address, ExecutionContext>();

        public readonly Stack<ExecutionFrame> frames = new Stack<ExecutionFrame>();
        public ExecutionFrame currentFrame { get; private set; }

        public BigInteger gas { get; private set; }

        public VirtualMachine(byte[] script)
        {
            this.entryContext = new ScriptContext(script);
            this.entryAddress = script.ScriptToPublicKey();
            _contextList[this.entryAddress] = this.currentContext;

            this.gas = 0;
            this.entryScript = script;
        }

        public abstract ExecutionState ExecuteInterop(string method);
        public abstract ExecutionContext LoadContext(Address address);

        public ExecutionState Execute()
        {
            return SwitchContext(entryAddress);
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

        internal ExecutionContext FindContext(Address address)
        {
            if (_contextList.ContainsKey(address))
            {
                return _contextList[address];
            }

            var result = LoadContext(address);
            _contextList[address] = result;

            return result;
        }

        internal ExecutionState SwitchContext(Address address)
        {
            if (!_contextList.ContainsKey(address))
            {
                return ExecutionState.Fault;
            }

            var context = _contextList[address];
            this.currentContext = context;
            PushFrame(context, 0, DefaultRegisterCount);
            return context.Execute(this.currentFrame, this.stack);
        }

        #endregion

    }
}
