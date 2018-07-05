using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Phantasma.Utils
{
    /// <summary>
    /// Represents the kind of the log message.
    /// </summary>
    public enum LogEntryKind
    {
        Debug,
        Message,
        Warning,
        Error
    }

    /// <summary>
    /// Keeps information related to a single log message.
    /// </summary>
    public struct LogEntryInfo
    {
        public LogEntryKind Kind;
        public string Message;
        public string File;
    }

    public interface ILog
    {
        LogEntryKind Level { get; set; }
        void Write(LogEntryInfo info);
    }

    public static class Logger
    {
        public static ILog Implementation { get; set; } = new ConsoleLog(LogEntryKind.Debug);

        public static LogEntryKind Level
        {
            get { return Implementation.Level; }
            set { Implementation.Level = value; }
        }

        public static void Debug(string msg, params object[] args)
        {
            var diagInfo = new LogEntryInfo
            {
                Kind = LogEntryKind.Debug,
                Message = args.Any() ? string.Format(msg, args) : msg
            };

            Implementation.Write(diagInfo);
        }

        public static void Message(string msg, params object[] args)
        {
            var diagInfo = new LogEntryInfo
            {
                Kind = LogEntryKind.Message,
                Message = args.Any() ? string.Format(msg, args) : msg
            };

            Implementation.Write(diagInfo);
        }

        public static void Warning(string msg, params object[] args)
        {
            var diagInfo = new LogEntryInfo
            {
                Kind = LogEntryKind.Warning,
                Message = args.Any() ? string.Format(msg, args) : msg
            };

            Implementation.Write(diagInfo);
        }

        public static void Error(string msg, params object[] args)
        {
            var diagInfo = new LogEntryInfo
            {
                Kind = LogEntryKind.Error,
                Message = args.Any() ? string.Format(msg, args) : msg
            };

            Implementation.Write(diagInfo);
        }

        public static void Exception(Exception ex)
        {
            var diagInfo = new LogEntryInfo
            {
                Kind = LogEntryKind.Error,
                Message = ex.ToString()
            };

            Implementation.Write(diagInfo);
        }
    }

    public class ConsoleLog : ILog
    {
        public LogEntryKind Level { get; set; }

        public ConsoleLog()
        {
            Level = LogEntryKind.Message;
        }

        public ConsoleLog(LogEntryKind level)
        {
            Level = level;
        }

        public void Write(LogEntryInfo info)
        {
            if (info.Kind < Level)
                return;

            Console.WriteLine(info.Message);
            Debug.WriteLine(info.Message);
        }
    }

    public class FileLog : ILog
    {
        public LogEntryKind Level { get; set; }

        readonly string fileName;

        public FileLog(string file)
        {
            fileName = file;
        }

        public void Write(LogEntryInfo info)
        {
            File.AppendAllLines(fileName, new string[] { info.Message });
            Debug.WriteLine(info.Message);
        }
    }
}
