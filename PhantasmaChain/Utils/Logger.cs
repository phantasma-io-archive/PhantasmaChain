using System;
using System.IO;

namespace Phantasma.Utils
{
    /// <summary>
    /// Represents the kind of the log message.
    /// </summary>
    public enum LogEntryKind
    {
        None,
        Message,
        Warning,
        Error,
        Debug,
    }

    public abstract class Logger
    {
        public LogEntryKind Level = LogEntryKind.Debug;

        public abstract void Write(LogEntryKind kind, string msg);

        public void Message(string msg)
        {
            if (this.Level < LogEntryKind.Message)
            {
                return;
            }

            Write(LogEntryKind.Message, msg);
        }

        public void Debug(string msg)
        {
            if (this.Level < LogEntryKind.Debug)
            {
                return;
            }

            Write(LogEntryKind.Debug, msg);
        }

        internal static Logger Init(Logger log)
        {
            if (log == null)
            {
                return DummyLogger.Instance;
            }

            return log;
        }

        public void Warning(string msg)
        {
            if (this.Level < LogEntryKind.Warning)
            {
                return;
            }

            Write(LogEntryKind.Warning, msg);
        }

        public void Error(string msg)
        {
            if (this.Level < LogEntryKind.Error)
            {
                return;
            }

            Write(LogEntryKind.Error, msg);
        }


        public void Exception(Exception ex)
        {
            Error(ex.ToString());
        }
    }

    public class DummyLogger: Logger
    {
        public static readonly DummyLogger Instance = new DummyLogger();

        public override void Write(LogEntryKind kind, string msg)
        {
        }
    }

    public class ConsoleLog : Logger
    {
        private static object _lock = new object();

        public override void Write(LogEntryKind kind, string msg)
        {
            lock (_lock) {
                var color = Console.ForegroundColor;
                switch (kind) {
                    case LogEntryKind.Error: Console.ForegroundColor = ConsoleColor.Red; break;
                    case LogEntryKind.Warning: Console.ForegroundColor = ConsoleColor.Yellow; break;
                    case LogEntryKind.Message: Console.ForegroundColor = ConsoleColor.Gray; break;
                    case LogEntryKind.Debug: Console.ForegroundColor = ConsoleColor.Cyan; break;
                    default: return;
                }
                Console.WriteLine(msg);
                Console.ForegroundColor = color;
            }
        }
    }

    public class FileLog : Logger
    {
        readonly string fileName;

        public FileLog(string file)
        {
            fileName = file;
        }

        public override void Write(LogEntryKind kind, string msg)
        {
            File.AppendAllLines(fileName, new string[] { msg });
        }
    }
}
