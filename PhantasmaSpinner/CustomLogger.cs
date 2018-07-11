using Phantasma.Utils;
using System.Collections.Generic;

namespace PhantasmaSpinner
{
    public struct LogEntry {
        public LogEntryKind kind;
        public string message;
    }

    class CustomLogger : Logger
    {
        public List<LogEntry> entries = new List<LogEntry>();

        public override void Write(LogEntryKind kind, string msg)
        {
            entries.Add(new LogEntry() { kind = kind, message = msg });
        }
    }
}
