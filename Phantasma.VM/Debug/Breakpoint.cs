namespace Phantasma.VM.Debug
{
    public struct Breakpoint
    {
        public readonly string contextName;
        public readonly uint offset;

        public Breakpoint(string contextName, uint offset)
        {
            this.contextName = contextName;
            this.offset = offset;
        }
    }
}
