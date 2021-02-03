namespace Phantasma.Core.Log
{
    public class DebugLogger : Logger
    {
        public DebugLogger(LogLevel level = LogLevel.Maximum) : base(level)
        {

        }

        protected override void Write(LogLevel kind, string msg)
        {
            System.Diagnostics.Debug.WriteLine(msg);
        }
    }
}
