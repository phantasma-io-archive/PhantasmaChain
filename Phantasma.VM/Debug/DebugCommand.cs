namespace Phantasma.VM.Debug
{
    public enum DebugOpcode
    {
        Log,
        Add,
        Delete,
        Step,
        Resume,
    }

    public struct DebugCommand
    {
        public readonly DebugOpcode Opcode;
        public readonly byte[] Data;

        public DebugCommand(DebugOpcode opcode, byte[] data)
        {
            Opcode = opcode;
            Data = data;
        }
    }
}
