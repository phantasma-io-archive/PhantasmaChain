namespace Phantasma.VM
{
    public class ExecutionContext : IInteropObject
    {
        public byte[] Script { get; private set; }

        public ExecutionContext(byte[] script)
        {
            this.Script = script;
        }

        public int GetSize()
        {
            return this.Script.Length;
        }
    }
}
