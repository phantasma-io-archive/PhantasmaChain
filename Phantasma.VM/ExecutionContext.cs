using System;
using System.Collections.Generic;

namespace Phantasma.VM
{
    public enum ExecutionState
    {
        Running,
        Break,
        Fault,
        Halt
    }

    public abstract class ExecutionContext
    {
        public abstract bool Admin { get; }
        public abstract int GetSize();

        public abstract ExecutionState Execute(ExecutionFrame frame, Stack<VMObject> stack);
    }
}
