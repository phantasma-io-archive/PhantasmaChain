using System;
using System.Collections.Generic;

namespace Phantasma.VM.Debug
{
    public class DebugHost
    {
        private List<Breakpoint> breakpoints = new List<Breakpoint>();

        public bool Paused { get; private set; }

        private Queue<DebugCommand> _commands = new Queue<DebugCommand>();

        public DebugHost()
        {

        }

        public void OnStep(ExecutionFrame frame, Stack<VMObject> stack)
        {
            foreach (var bp in breakpoints)
            {
                if (bp.contextName == frame.Context.Name && bp.offset == frame.Offset)
                {
                    Paused = true;
                    break;
                }
            }

            do
            {
                lock (_commands)
                {
                    if (_commands.Count > 0)
                    {
                        var cmd = _commands.Dequeue();
                        ExecuteCommand(cmd);
                    }
                }
            } while (Paused);
        }

        public void OnLog(string msg)
        {

        }

        private void ExecuteCommand(DebugCommand cmd)
        {
            switch (cmd.Opcode)
            {

            }
            throw new NotImplementedException();
        }
    }
}
