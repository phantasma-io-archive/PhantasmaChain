namespace Phantasma.Core.Log
{
    public class DummyLogger : Logger
    {
        public static readonly DummyLogger Instance = new DummyLogger();

        public override void Write(LogEntryKind kind, string msg)
        {
        }
    }
}
