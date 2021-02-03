namespace Phantasma.Core.Log
{
    public class DummyLogger : Logger
    {
        public static readonly DummyLogger Instance = new DummyLogger();

        public DummyLogger() : base(LogLevel.None)
        {

        }

        protected override void Write(LogLevel kind, string msg)
        {
        }
    }
}
