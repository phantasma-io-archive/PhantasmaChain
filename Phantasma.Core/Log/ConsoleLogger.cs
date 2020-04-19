using System;

namespace Phantasma.Core.Log
{
    public class ConsoleLogger : Logger
    {
        private static object _lock = new object();

        public override void Write(LogEntryKind kind, string msg)
        {
            lock (_lock)
            {
                var color = Console.ForegroundColor;
                switch (kind)
                {
                    case LogEntryKind.Error: Console.ForegroundColor = ConsoleColor.Red; break;
                    case LogEntryKind.Warning: Console.ForegroundColor = ConsoleColor.Yellow; break;
                    case LogEntryKind.Message: Console.ForegroundColor = ConsoleColor.Gray; break;
                    case LogEntryKind.Sucess: Console.ForegroundColor = ConsoleColor.Green; break;
                    case LogEntryKind.Debug: Console.ForegroundColor = ConsoleColor.Cyan; break;
                    default: return;
                }
                Console.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " +msg);
                Console.ForegroundColor = color;
            }
        }
    }
}
